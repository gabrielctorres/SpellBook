using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
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

        [Tooltip("Cargas: cada ativação gasta uma, e elas voltam uma por vez. Desligadas por padrão.")]
        [SerializeField] private ChargesConfig charges = new ChargesConfig();

        [Tooltip("Alcance da habilidade por nível, lido pela CastRangeCondition. 0 ou menos: sem limite.")]
        [SerializeField] private ScalableFloat castRange = ScalableFloat.Constant(0f);

        [Tooltip("Regras avaliadas na ordem, depois das tags, da recarga e do custo. A primeira que falha decide o motivo.")]
        [SerializeField] private List<ActivationRule> activationConditions = new List<ActivationRule>();

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
        [FormerlySerializedAs("Actions")]
        [ActionEvent("Ao ativar", DefaultTarget.EventTargets, true)]
        public List<GameplayAction> OnActivate = new List<GameplayAction>();

        /// <summary>
        /// Verifica se a habilidade pode ser ativada pelo ASC, informando o motivo em caso de falha.
        /// </summary>
        public bool CanActivate(AbilitySystemComponent asc, out AbilityActivationResult reason)
        {
            reason = asc != null ? asc.GetActivationResult(this) : AbilityActivationResult.InvalidAbility;
            return reason == AbilityActivationResult.Success;
        }

        public bool CanActivate(AbilitySystemComponent asc) => CanActivate(asc, out _);

        /// <summary>
        /// Como <see cref="CanActivate(AbilitySystemComponent, out AbilityActivationResult)"/>, com o alvo das regras de
        /// ativação e o motivo detalhado da falha. Mesmo caminho de <see cref="AbilitySystemComponent.TryActivateAbility(GameplayAbility, in AbilityTarget)"/>.
        /// </summary>
        public bool CanActivate(AbilitySystemComponent asc, in AbilityTarget target, out ActivationFailure failure)
        {
            if (asc == null)
            {
                failure = new ActivationFailure(AbilityActivationResult.InvalidAbility);
                return false;
            }
            return asc.GetActivationResult(this, in target, out failure) == AbilityActivationResult.Success;
        }

        /// <summary>Regras de ativação, avaliadas na ordem depois do custo. Entradas nulas ou sem condição são puladas.</summary>
        public List<ActivationRule> ActivationConditions => activationConditions ??= new List<ActivationRule>();

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

        /// <summary>
        /// Alcance por nível, avaliado com a tabela <see cref="Values"/> (fonte única do alcance). 0 ou menos é "sem limite".
        /// Atribuir nulo volta ao alcance 0.
        /// </summary>
        public ScalableFloat CastRange
        {
            get => castRange ??= ScalableFloat.Constant(0f);
            set => castRange = value ?? ScalableFloat.Constant(0f);
        }

        /// <summary>Cargas da habilidade (desligadas por padrão). O estado de cada personagem fica no <see cref="AbilitySpec"/>.</summary>
        public ChargesConfig Charges => charges ??= new ChargesConfig();

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

            return ApplyCooldownReduction(CooldownEffect.Duration.Evaluate(level, values), asc);
        }

        // Conta única da redução (R17): a recarga do CooldownEffect e a recarga das cargas passam por aqui
        internal float ApplyCooldownReduction(float baseDuration, AbilitySystemComponent asc)
        {
            if (CooldownReductionAttribute == null || asc == null) return baseDuration;

            float cdr = asc.GetAttributeValue(CooldownReductionAttribute);
            return cooldownReductionPolicy != null
                ? cooldownReductionPolicy.Apply(baseDuration, cdr)
                : CooldownReductionPolicy.ApplyDefault(baseDuration, cdr);
        }

        /// <summary>
        /// Acusa <c>MaxLevel</c> menor que 1, a tabela de valores (nomes e valores), a política de redução sem atributo de
        /// redução, cada nome do modo Table que um efeito usado pela habilidade lê e não está na tabela (recarga, custo e
        /// <see cref="GameplayAction.CollectEffects"/>), e <c>MaxLevel</c> acima do tamanho de um array da tabela (aviso).
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
            ValidateActivationRules(result);
            charges?.Validate(result, nameof(charges), values);

            if (OnActivate == null) return;

            for (int i = 0; i < OnActivate.Count; i++)
            {
                OnActivate[i]?.Validate(result);
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
            if (OnActivate != null)
            {
                for (int i = 0; i < OnActivate.Count; i++)
                {
                    OnActivate[i]?.CollectEffects(effects);
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

        private void ValidateActivationRules(ValidationResult result)
        {
            if (activationConditions == null) return;

            bool usesCastRange = false;
            for (int i = 0; i < activationConditions.Count; i++)
            {
                string path = $"{nameof(activationConditions)}.Array.data[{i}]";
                ActivationRule rule = activationConditions[i];
                if (rule == null)
                {
                    result.AddError("Regra de ativação vazia: escolha a Condition ou remova a regra.", path);
                    continue;
                }

                rule.Validate(result, path);
                usesCastRange |= UsesCastRange(rule.Condition);
            }

            if (usesCastRange) ValidateCastRange(result);
        }

        // Procura a CastRangeCondition também dentro das composições do GAS
        private static bool UsesCastRange(Condition<ConditionContext> condition)
        {
            switch (condition)
            {
                case CastRangeCondition _:
                    return true;
                case NotCondition not:
                    return UsesCastRange(not.Condition);
                case AndCondition and:
                    return AnyUsesCastRange(and.Conditions);
                case OrCondition or:
                    return AnyUsesCastRange(or.Conditions);
                default:
                    return false;
            }
        }

        private static bool AnyUsesCastRange(List<Condition<ConditionContext>> conditions)
        {
            if (conditions == null) return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (UsesCastRange(conditions[i])) return true;
            }

            return false;
        }

        // Alcance negativo em algum nível é erro; alcance 0 em todos é "sem limite", e a CastRangeCondition sempre passa
        private void ValidateCastRange(ValidationResult result)
        {
            ScalableFloat range = CastRange;
            // Nome fora da tabela vale o fallback; avaliar sem a tabela evita o aviso no Console
            bool hasName = range.Mode == ScalableFloat.ScalableMode.Table && values != null && values.IndexOf(range.TableName) >= 0;
            AbilityValues table = hasName ? values : null;

            bool limited = false;
            for (int level = 1; level <= Mathf.Max(1, maxLevel); level++)
            {
                float value = range.Evaluate(level, table);
                if (value < 0f)
                {
                    result.AddError($"Cast Range é {value} no nível {level}: o alcance não pode ser negativo.", nameof(castRange));
                    return;
                }

                limited |= value > 0f;
            }

            if (!limited)
            {
                result.AddWarning("CastRangeCondition com Cast Range 0: sem limite, a condição sempre passa.", nameof(castRange));
            }
        }

        public string GetDescription(AbilitySystemComponent source = null)
        {
            if (Description == null) return string.Empty;
            return Description.GetFullDescription(this, source);
        }
    }
}
