using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Effects
{
    public enum EffectDurationPolicy
    {
        Instant,
        Infinite,
        HasDuration
    }

    /// <summary>Quando o valor de um modificador persistente é calculado (ADR 0021, item 1).</summary>
    public enum MagnitudeMode
    {
        /// <summary>Uma vez, na aplicação. Padrão.</summary>
        Snapshot,

        /// <summary>De novo a cada mudança do que o valor lê (atributo do instigador ou do alvo, nível), sem polling.</summary>
        Live
    }

    /// <summary>Um modificador do efeito. É classe: entradas nulas nas listas do efeito são ignoradas.</summary>
    [Serializable]
    public class AttributeEffectModifier
    {
        public AttributeDefinition Attribute;
        public ModifierOperation Operation;
        public ModifierValueSource ValueSource = new ModifierValueSource();

        [Tooltip("Snapshot: calculado na aplicação. Live: recalculado quando muda o que o valor lê. Só vale nos Modifiers de efeito com duração ou infinito.")]
        public MagnitudeMode Mode;
    }

    [CreateAssetMenu(fileName = "GE_NewEffect", menuName = "GAS/Gameplay Effect")]
    [HelpURL(DocUrls.Gas + "#gameplayeffect")]
    public class GameplayEffect : ScriptableObject, ISerializationCallbackReceiver, IValidatable
    {
        [Header("Identidade")]
        [Tooltip("Nome exibido do efeito (lista de buffs, tooltip). Vazio por padrão.")]
        [SerializeField] private string displayName;

        [Tooltip("Ícone do efeito. Opcional.")]
        [SerializeField] private Sprite icon;

        /// <summary>Nome exibido; vazio por padrão, nunca nulo.</summary>
        public string DisplayName => displayName ?? string.Empty;

        /// <summary>Ícone do efeito; nulo por padrão.</summary>
        public Sprite Icon => icon;

        [Header("Duration")]
        public EffectDurationPolicy DurationPolicy;
        
        [ShowIf(nameof(DurationPolicy), EffectDurationPolicy.HasDuration)]
        [SerializeField] private ScalableFloat duration = ScalableFloat.Constant(0f);

        // Só leitura do formato 2.x ("Duration: 4"); convertido em OnAfterDeserialize (ADR 0009). Sai antes da 3.0 final.
        [FormerlySerializedAs("Duration")]
        [SerializeField, HideInInspector] private float legacyDuration = float.NaN;

        /// <summary>Duração em segundos por nível (HasDuration). Sem habilidade, vale o nível 1.</summary>
        public ScalableFloat Duration
        {
            get => duration;
            set => duration = value;
        }

        [Tooltip("Nível com que o efeito é avaliado quando aplicado sem habilidade (efeitos de partida, ApplyEffect direto).")]
        [SerializeField, Min(1)] private int defaultLevel = 1;

        /// <summary>Nível da aplicação sem habilidade (pelo menos 1). Sem habilidade não há tabela: o modo Table vale o Fallback.</summary>
        public int DefaultLevel => defaultLevel < 1 ? 1 : defaultLevel;

        [Header("Periodic")]
        [Tooltip("If Period > 0, effect triggers every Period seconds")]
        public float Period = 0f;

        [Header("Instant & Persistent Modifiers")]
        public List<AttributeEffectModifier> Modifiers;

        [Header("Periodic Modifiers (Tick)")]
        [Tooltip("Usado só quando Period > 0")]
        public List<AttributeEffectModifier> PeriodicModifiers;

        [Header("Metadados")]
        [Tooltip("Efeito benéfico para o alvo. Pode coexistir com Is Debuff, com aviso do validador.")]
        [SerializeField] private bool isBuff;

        [Tooltip("Efeito prejudicial para o alvo.")]
        [SerializeField] private bool isDebuff;

        [Tooltip("Não aparece em listas de efeitos da UI.")]
        [SerializeField] private bool isHidden;

        [Tooltip("Pode ser removido por uma ação de purga.")]
        [SerializeField] private bool isPurgable;

        public bool IsBuff => isBuff;
        public bool IsDebuff => isDebuff;
        public bool IsHidden => isHidden;
        public bool IsPurgable => isPurgable;

        [Header("Actions")]
        [SerializeReference]
        [TypePicker]
        public List<EffectAction> OnApplication = new List<EffectAction>();

        [SerializeReference]
        [TypePicker]
        [Tooltip("Usado só quando Period > 0")]
        public List<EffectAction> OnTickActions = new List<EffectAction>();

        [SerializeReference]
        [TypePicker]
        public List<EffectAction> OnRemoval = new List<EffectAction>();

        [Header("Gameplay Tags")]
        [Tooltip("Tipos de dano do efeito. Os processadores dos atributos (resistência, imunidade) casam pela hierarquia: Dano casa com Dano.Fogo.")]
        [SerializeField] private GameplayTagContainer damageTags = new GameplayTagContainer();

        /// <summary>Tipos de dano do efeito, lidos pelos processadores dos atributos (ADR 0033). Nunca nulo.</summary>
        public GameplayTagContainer DamageTags => damageTags ??= new GameplayTagContainer();

        public List<GameplayTag> GrantedTags; 
        public List<GameplayTag> ApplicationRequirements;
        public List<GameplayTag> ApplicationImmunityTags;

        [Header("Stacking Logic")]
        public bool CanStack;
        public int MaxStacks = 1;
        public bool RefreshDurationOnStack = true;

        [Tooltip("Com N pilhas, cada modificador (persistente e do tique) vale N vezes o valor de uma pilha. Desligue quando a pilha só conta.")]
        [SerializeField] private bool stacksMultiplyMagnitude = true;

        /// <summary>
        /// Pilhas multiplicam o valor de cada modificador antes da fórmula (ADR 0029, item 2). Ligado por padrão, também em
        /// assets antigos sem o campo.
        /// </summary>
        public bool StacksMultiplyMagnitude
        {
            get => stacksMultiplyMagnitude;
            set => stacksMultiplyMagnitude = value;
        }

        /// <summary>
        /// Acusa Is Buff com Is Debuff, cada <see cref="ScalableFloat"/> inválido (duração e valores), SetByCaller sem nome
        /// válido (erro) ou sem padrão (aviso), modo Live onde não tem efeito (aviso) e ciclo ao vivo entre atributos (erro).
        /// Os nomes do modo Table são conferidos pela habilidade que usa o efeito.
        /// </summary>
        public void Validate(ValidationResult result)
        {
            if (isBuff && isDebuff)
            {
                result.AddWarning("Efeito marcado como Is Buff e Is Debuff ao mesmo tempo.", nameof(isDebuff));
            }

            if (DurationPolicy == EffectDurationPolicy.HasDuration) duration?.Validate(result, nameof(duration));

            bool liveHasEffect = DurationPolicy != EffectDurationPolicy.Instant;
            ValidateModifiers(result, Modifiers, nameof(Modifiers), liveHasEffect,
                "Modo Live em efeito Instant não tem efeito: o valor é calculado uma vez.");
            ValidateModifiers(result, PeriodicModifiers, nameof(PeriodicModifiers), false,
                "Modo Live em modificador periódico não tem efeito: o tique já calcula o valor de novo.");

            if (liveHasEffect && HasLiveCycle())
            {
                result.AddError(
                    "Os modificadores Live formam um ciclo entre atributos (um depende do outro). Em runtime o ciclo é cortado e mantém o último valor.",
                    nameof(Modifiers));
            }
        }

        private static void ValidateModifiers(ValidationResult result, List<AttributeEffectModifier> modifiers, string listPath,
            bool liveHasEffect, string liveWarning)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                AttributeEffectModifier modifier = modifiers[i];
                if (modifier == null) continue;

                string path = $"{listPath}.Array.data[{i}]";
                ModifierValueSource source = modifier.ValueSource;
                if (source != null && source.Type == ModifierValueSource.SourceType.Value)
                {
                    source.Value?.Validate(result, path + ".ValueSource.Value");
                }
                else if (source != null && source.Type == ModifierValueSource.SourceType.SetByCaller)
                {
                    if (!AbilityValues.IsIdentifier(source.CallerName))
                    {
                        result.AddError($"O nome do SetByCaller \"{source.CallerName}\" é inválido: use só letras, dígitos e _.",
                            path + ".ValueSource.CallerName");
                    }
                    else if (!source.CallerHasDefault)
                    {
                        result.AddWarning($"O SetByCaller \"{source.CallerName}\" não tem padrão: sem o valor do chamador, vale 0.",
                            path + ".ValueSource.CallerHasDefault");
                    }
                }

                if (modifier.Mode == MagnitudeMode.Live && !liveHasEffect)
                {
                    result.AddWarning(liveWarning, path + ".Mode");
                }
            }
        }

        // Ciclo visível no asset: cada modificador Live liga o atributo que altera aos atributos que o valor lê.
        private bool HasLiveCycle()
        {
            if (Modifiers == null) return false;

            var visited = new HashSet<AttributeDefinition>();
            var pending = new Stack<AttributeDefinition>();
            for (int i = 0; i < Modifiers.Count; i++)
            {
                AttributeEffectModifier start = Modifiers[i];
                if (!IsLive(start)) continue;

                visited.Clear();
                pending.Clear();
                PushReadAttributes(pending, start.ValueSource);
                while (pending.Count > 0)
                {
                    AttributeDefinition read = pending.Pop();
                    if (read == start.Attribute) return true;
                    if (!visited.Add(read)) continue;

                    // Quem altera o atributo lido também depende do que o seu valor lê
                    for (int j = 0; j < Modifiers.Count; j++)
                    {
                        if (IsLive(Modifiers[j]) && Modifiers[j].Attribute == read) PushReadAttributes(pending, Modifiers[j].ValueSource);
                    }
                }
            }

            return false;
        }

        private static bool IsLive(AttributeEffectModifier modifier)
        {
            return modifier != null && modifier.Mode == MagnitudeMode.Live && modifier.Attribute != null && modifier.ValueSource != null;
        }

        private static void PushReadAttributes(Stack<AttributeDefinition> pending, ModifierValueSource source)
        {
            switch (source.Type)
            {
                case ModifierValueSource.SourceType.Attribute:
                    if (source.Attribute != null) pending.Push(source.Attribute);
                    break;
                case ModifierValueSource.SourceType.TargetAttribute:
                    if (source.TargetAttributeDefinition != null) pending.Push(source.TargetAttributeDefinition);
                    break;
                case ModifierValueSource.SourceType.Formula:
                    if (source.Formula == null) break;
                    foreach (AttributeDefinition dependency in source.Formula.GetSourceDependencies())
                    {
                        if (dependency != null) pending.Push(dependency);
                    }
                    break;
            }
        }

        /// <summary>
        /// Acusa, no <paramref name="result"/> da habilidade, cada <see cref="ScalableFloat"/> em modo Table usado pelo efeito
        /// (duração, modificadores e periódicos) cujo nome não está na <paramref name="table"/>, citando o efeito e o nome.
        /// </summary>
        internal void ValidateTableNames(ValidationResult result, AbilityValues table, string propertyPath)
        {
            if (DurationPolicy == EffectDurationPolicy.HasDuration) ValidateTableName(result, duration, table, propertyPath);
            ValidateTableNames(result, Modifiers, table, propertyPath);
            if (Period > 0f) ValidateTableNames(result, PeriodicModifiers, table, propertyPath);
        }

        private void ValidateTableNames(ValidationResult result, List<AttributeEffectModifier> modifiers, AbilityValues table,
            string propertyPath)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierValueSource source = modifiers[i]?.ValueSource;
                if (source != null && source.Type == ModifierValueSource.SourceType.Value)
                {
                    ValidateTableName(result, source.Value, table, propertyPath);
                }
            }
        }

        private void ValidateTableName(ValidationResult result, ScalableFloat value, AbilityValues table, string propertyPath)
        {
            if (value == null || value.Mode != ScalableFloat.ScalableMode.Table || string.IsNullOrEmpty(value.TableName)) return;
            if (table != null && table.IndexOf(value.TableName) >= 0) return;

            result.AddError($"O efeito \"{name}\" lê o valor \"{value.TableName}\" da tabela, que não existe nesta habilidade.",
                propertyPath);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyDuration)) return;
            duration = ScalableFloat.Constant(legacyDuration);
            legacyDuration = float.NaN;
        }
    }
}

