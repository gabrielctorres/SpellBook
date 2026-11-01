using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Dados de uma habilidade. A execução é feita pelo AbilitySystemComponent (TryActivateAbility).
    /// </summary>
    [CreateAssetMenu(fileName = "GA_NewAbility", menuName = "GAS/Gameplay Ability")]
    [HelpURL(DocUrls.Gas + "#gameplayability")]
    public class GameplayAbility : ScriptableObject, IValidatable
    {
        [Header("Identidade")]
        public Sprite Icon;

        public string DisplayName;

        public GameplayTag AbilityTag;

        public AbilityDescription Description;

        [Header("Níveis")]
        [Tooltip("Nível máximo. O ASC limita o nível concedido a [1, MaxLevel].")]
        [SerializeField, Min(1)] private int maxLevel = 1;

        [Tooltip("Valores nomeados por nível, lidos pelos ScalableFloat em modo Table dos efeitos desta habilidade.")]
        [SerializeField] private AbilityValues values = new AbilityValues();

        [Header("Ativação")]
        public GameplayEffect CooldownEffect;

        public AttributeDefinition CooldownReductionAttribute;

        [Tooltip("Como a redução vira duração (piso, teto e modo). Vazio: piso de 0,1 s e redução limitada a 100 %.")]
        [SerializeField] private CooldownReductionPolicy cooldownReductionPolicy;

        [Tooltip("Efeito Instant aplicado ao ativar (ex: Mana Add -20). A ativação falha se o atributo ficaria abaixo do MinValue.")]
        public GameplayEffect CostEffect;

        [Header("Tags")]
        [Tooltip("Não ativa se o dono tiver alguma destas tags.")]
        public List<GameplayTag> BlockedTags;

        [Tooltip("Só ativa se o dono tiver todas estas tags.")]
        public List<GameplayTag> RequiredTags;

        [Tooltip("Tags concedidas ao dono enquanto a habilidade está ativa (ex: State.Casting).")]
        public List<GameplayTag> ActivationOwnedTags;

        [Tooltip("Ao ativar, cancela habilidades ativas cujo AbilityTag corresponda a uma destas.")]
        public List<GameplayTag> CancelAbilitiesWithTag;

        [Tooltip("Enquanto ativa, impede a ativação de habilidades cujo AbilityTag corresponda a uma destas.")]
        public List<GameplayTag> BlockAbilitiesWithTag;

        [Tooltip("Cancela esta habilidade se o dono receber uma destas tags durante a execução (ex: State.Stunned).")]
        public List<GameplayTag> CancelledByTags;

        [Header("Execução")]
        [SerializeReference]
        [TypePicker]
        public List<AbilityAction> Actions = new List<AbilityAction>();

        /// <summary>
        /// Verifica se a habilidade pode ser ativada pelo ASC, informando o motivo em caso de falha.
        /// </summary>
        public bool CanActivate(AbilitySystemComponent asc, out AbilityActivationResult reason)
        {
            reason = asc != null ? asc.GetActivationResult(this) : AbilityActivationResult.InvalidAbility;
            return reason == AbilityActivationResult.Success;
        }

        public bool CanActivate(AbilitySystemComponent asc) => CanActivate(asc, out _);

        /// <summary>Nível máximo da habilidade (pelo menos 1).</summary>
        public int MaxLevel => maxLevel;

        /// <summary>Tabela de valores nomeados por nível da habilidade.</summary>
        public AbilityValues Values => values;

        /// <summary>Valor <paramref name="name"/> da tabela no <paramref name="level"/>; sem o nome, <c>false</c> e 0, sem aviso.</summary>
        public bool TryGetValue(string name, int level, out float value)
        {
            return values.TryGetValue(name, level, out value);
        }

        protected virtual void OnValidate()
        {
            values?.MarkChanged();
        }

        /// <summary>Política de redução de recarga; nula usa <see cref="CooldownReductionPolicy.ApplyDefault"/>.</summary>
        public CooldownReductionPolicy CooldownReductionPolicy => cooldownReductionPolicy;

        /// <summary>
        /// Duração do cooldown no nível da habilidade no <paramref name="asc"/> (não concedida: nível 1), avaliada
        /// com a tabela <see cref="Values"/>, já considerando o atributo de redução (CDR 0.20 = 20% de redução), pela
        /// <see cref="CooldownReductionPolicy"/> ou, sem ela, pela regra padrão.
        /// </summary>
        public float GetCooldownDuration(AbilitySystemComponent asc)
        {
            return GetCooldownDuration(asc, asc != null ? asc.GetAbilityLevel(this) : 1);
        }

        // Conta única da recarga: usada pelo método público e pela ativação no ASC (EA6)
        internal float GetCooldownDuration(AbilitySystemComponent asc, int level)
        {
            if (CooldownEffect == null) return 0f;

            float duration = CooldownEffect.Duration.Evaluate(level, values);
            if (CooldownReductionAttribute != null && asc != null)
            {
                float cdr = asc.GetAttributeValue(CooldownReductionAttribute);
                duration = cooldownReductionPolicy != null
                    ? cooldownReductionPolicy.Apply(duration, cdr)
                    : CooldownReductionPolicy.ApplyDefault(duration, cdr);
            }
            return duration;
        }

        /// <summary>
        /// Acusa <c>MaxLevel</c> menor que 1, a tabela de valores (nomes e valores), a política de redução sem atributo de
        /// redução, cada nome do modo Table que um efeito usado pela habilidade lê e não está na tabela (recarga, custo e
        /// <see cref="AbilityAction.CollectEffects"/>), e <c>MaxLevel</c> acima do tamanho de um array da tabela (aviso).
        /// Repassa a validação a cada ação da lista; ações vazias são ignoradas.
        /// </summary>
        public virtual void Validate(ValidationResult result)
        {
            if (maxLevel < 1)
            {
                result.AddError($"Max Level é {maxLevel}: o nível mínimo é 1.", nameof(maxLevel));
            }

            if (values != null)
            {
                values.Validate(result, nameof(values));
                ValidateArraySizes(result);
            }

            if (cooldownReductionPolicy != null && CooldownReductionAttribute == null)
            {
                result.AddWarning(
                    "Cooldown Reduction Policy sem Cooldown Reduction Attribute: a política não tem efeito.",
                    nameof(cooldownReductionPolicy));
            }

            ValidateEffectTableNames(result);

            if (Actions == null) return;

            for (int i = 0; i < Actions.Count; i++)
            {
                Actions[i]?.Validate(result);
            }
        }

        private void ValidateArraySizes(ValidationResult result)
        {
            for (int i = 0; i < values.Count; i++)
            {
                AbilityValues.Entry entry = values.GetEntry(i);
                int size = entry?.Value != null ? entry.Value.ArrayLength : 0;
                if (size == 0 || size >= maxLevel) continue;

                result.AddWarning(
                    $"O valor \"{entry.Name}\" tem {size} níveis e o Max Level é {maxLevel}: acima do nível {size}, repete o último.",
                    $"{nameof(values)}.entries.Array.data[{i}].value");
            }
        }

        private void ValidateEffectTableNames(ValidationResult result)
        {
            var effects = new List<GameplayEffect>();
            if (CooldownEffect != null) effects.Add(CooldownEffect);
            if (CostEffect != null) effects.Add(CostEffect);
            if (Actions != null)
            {
                for (int i = 0; i < Actions.Count; i++)
                {
                    Actions[i]?.CollectEffects(effects);
                }
            }

            for (int i = 0; i < effects.Count; i++)
            {
                GameplayEffect effect = effects[i];
                // Um efeito usado em mais de um lugar é conferido uma vez
                if (effect == null || effects.IndexOf(effect) != i) continue;
                effect.ValidateTableNames(result, values, nameof(values));
            }
        }

        public string GetDescription(AbilitySystemComponent source = null)
        {
            if (Description == null) return string.Empty;
            return Description.GetFullDescription(this, source);
        }
    }
}
