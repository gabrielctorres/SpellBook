using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using UnityEngine;
using UnityEngine.Serialization;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Triggers;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Abilities
{
    /// <summary>Quando a habilidade cobra a recarga, o custo e a carga.</summary>
    public enum CommitMode
    {
        /// <summary>Na ativação, antes da primeira ação.</summary>
        OnStart,
        /// <summary>Quando uma <see cref="CommitAction"/> roda. Cancelar antes dela não cobra nada.</summary>
        Manual,
    }

    /// <summary>
    /// Dados de uma habilidade. A execução é feita pelo AbilitySystemComponent (TryActivateAbility).
    /// </summary>
    [CreateAssetMenu(fileName = "New Ability", menuName = "SpellBook/GAS/Ability")]
    [HelpURL(DocUrls.Gas + "#id-1.4-gameplay-abilities-ga")]
    public class GameplayAbility : ScriptableObject, ISerializationCallbackReceiver, IValidatable
    {
        [Header("Identidade")]
        public Sprite Icon;

        [Tooltip("Nome exibido da habilidade, com chave opcional para o provedor de texto.")]
        [SerializeField] private LocalizedText localizedName;

        // Só leitura do formato 2.x ("DisplayName: texto"); convertido em OnAfterDeserialize (ADR 0009). Sai antes da 3.0 final.
        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName;

        /// <summary>Nome exibido; resolva com <see cref="LocalizedText.Resolve"/> ou leia o texto padrão em <see cref="LocalizedText.Text"/>.</summary>
        public LocalizedText DisplayName
        {
            get => localizedName;
            set => localizedName = value;
        }

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

        [Tooltip("Quando cobrar a recarga, o custo e a carga: na ativação, ou quando uma ação Commit rodar.")]
        [SerializeField] private CommitMode commitMode = CommitMode.OnStart;

        [Tooltip("Instant termina ao fim de \"Ao ativar\". Passive ativa na concessão e fica ativa. Toggle liga e desliga a cada ativação. Channel canaliza pela duração.")]
        [SerializeField] private AbilityBehavior behavior = AbilityBehavior.Instant;

        [Tooltip("Passive: tenta ativar de novo a cada Tick enquanto estiver inativa (falhou na concessão ou foi cancelada).")]
        [SerializeField] private bool reactivatePassive;

        [Tooltip("Channel: duração da canalização por nível. 0: canaliza até ser cancelada.")]
        [SerializeField] private ScalableFloat channelDuration = ScalableFloat.Constant(0f);

        [Tooltip("Channel: segundos entre os passos de \"A cada passo\". 0: sem passos.")]
        [SerializeField, Min(0f)] private float channelTickInterval;

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

        /// <summary>Roda quando a lista "Ao ativar" acaba, antes de a habilidade devolver as tags e terminar.</summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("Ao terminar", DefaultTarget.EventTargets, true)]
        public List<GameplayAction> OnEnd = new List<GameplayAction>();

        /// <summary>Roda quando a habilidade é cancelada, depois do cancelamento das ações em curso e antes do fim.</summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("Ao ser cancelada", DefaultTarget.EventTargets, true)]
        public List<GameplayAction> OnCancel = new List<GameplayAction>();

        /// <summary>Roda a cada passo de uma habilidade <see cref="AbilityBehavior.Channel"/>, no intervalo dela.</summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("A cada passo", DefaultTarget.EventTargets, true)]
        public List<GameplayAction> OnChannelTick = new List<GameplayAction>();

        /// <summary>Roda quando a habilidade é concedida, depois de <c>OnAbilityGranted</c>. Conceder de novo não roda.</summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("Ao ser concedida", DefaultTarget.Owner, false)]
        public List<GameplayAction> OnGranted = new List<GameplayAction>();

        /// <summary>Roda quando a habilidade é retirada, depois de <c>OnAbilityRemoved</c>.</summary>
        [SerializeReference]
        [TypePicker]
        [ActionEvent("Ao ser retirada", DefaultTarget.Owner, false)]
        public List<GameplayAction> OnRemoved = new List<GameplayAction>();

        [Header("Gatilhos")]
        [Tooltip("Interruptor que liga os gatilhos \"só com autocast\". Só em Instant e Channel.")]
        [SerializeField] private AutocastConfig autocast = new AutocastConfig();

        [Tooltip("Causas que tentam ativar a habilidade sozinha, pelas mesmas checagens da ativação manual.")]
        [SerializeReference, TypePicker] private List<AbilityTrigger> triggers = new List<AbilityTrigger>();

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

        /// <summary>
        /// Gatilhos da habilidade. O ASC os lê na concessão: mudar a lista depois só vale para a próxima concessão.
        /// </summary>
        public List<AbilityTrigger> Triggers => triggers ??= new List<AbilityTrigger>();

        /// <summary>Autocast da habilidade (não permitido por padrão). O estado de cada personagem fica no <see cref="AbilitySpec"/>.</summary>
        public AutocastConfig Autocast => autocast ??= new AutocastConfig();

        /// <summary>
        /// <see cref="AbilityBehavior.Passive"/>: tenta ativar de novo a cada <c>Tick</c> enquanto inativa, em modo
        /// silencioso. O ASC lê na concessão, como os <see cref="Triggers"/>.
        /// </summary>
        public bool ReactivatePassive
        {
            get => reactivatePassive;
            set => reactivatePassive = value;
        }

        /// <summary>Quando a habilidade cobra a recarga, o custo e a carga. O padrão é <see cref="CommitMode.OnStart"/>.</summary>
        public CommitMode CommitMode
        {
            get => commitMode;
            set => commitMode = value;
        }

        /// <summary>Como a ativação dura. O padrão é <see cref="AbilityBehavior.Instant"/>.</summary>
        public AbilityBehavior Behavior
        {
            get => behavior;
            set => behavior = value;
        }

        /// <summary>
        /// Duração da canalização por nível, avaliada com a tabela <see cref="Values"/> na ativação. 0 ou menos canaliza
        /// até ser cancelada. Atribuir nulo volta a 0.
        /// </summary>
        public ScalableFloat ChannelDuration
        {
            get => channelDuration ??= ScalableFloat.Constant(0f);
            set => channelDuration = value ?? ScalableFloat.Constant(0f);
        }

        /// <summary>Segundos entre os passos de <see cref="OnChannelTick"/>. 0 é sem passos; negativo vira 0.</summary>
        public float ChannelTickInterval
        {
            get => channelTickInterval;
            set => channelTickInterval = Mathf.Max(0f, value);
        }

        // A ativação não termina ao fim de OnActivate: fica até ser desligada, retirada, cancelada ou até o fim da canalização
        internal bool IsSustained => behavior != AbilityBehavior.Instant;

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
        /// <see cref="CollectActionEffects"/>), e <c>MaxLevel</c> acima do tamanho de um array da tabela (aviso).
        /// Valida as seis listas de ações e as filhas, o comportamento, o commit, os gatilhos e o autocast (Design 9 do B-012).
        /// Valida também a <see cref="Description"/> e os elementos dela (Design 7 do B-008).
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
            Description?.Validate(result, nameof(Description), this);

            var commits = new List<string>();
            var ownerEvents = new List<GameplayTag>();
            ActionVisitor visitor = (action, path, targetsOwner) =>
            {
                if (action is CommitAction) commits.Add(path);
                else if (targetsOwner && action is SendGameplayEventAction send && send.Tag.IsValid) ownerEvents.Add(send.Tag);
            };
            ActionLists.Validate(this, nameof(OnActivate), OnActivate, result, visitor);
            ActionLists.Validate(this, nameof(OnEnd), OnEnd, result, visitor);
            ActionLists.Validate(this, nameof(OnCancel), OnCancel, result, visitor);
            ActionLists.Validate(this, nameof(OnChannelTick), OnChannelTick, result, visitor);
            ActionLists.Validate(this, nameof(OnGranted), OnGranted, result, visitor);
            ActionLists.Validate(this, nameof(OnRemoved), OnRemoved, result, visitor);

            ValidateBehavior(result);
            ValidateCommit(result, commits);
            ValidateTriggers(result, ownerEvents);
        }

        /// <summary>Aviso para a <see cref="TagQueryCondition"/> com sujeito <see cref="ConditionSubject.TargetUnit"/> fora de um filtro de candidato.</summary>
        internal const string TargetUnitQueryWarning =
            "TagQueryCondition com Target Unit fora de um filtro de alvo: aqui não há candidato, use Owner ou o filtro do seletor.";

        /// <summary>Se a <paramref name="condition"/>, ou uma condição dentro de And, Or e Not, é uma <see cref="TagQueryCondition"/> com <see cref="ConditionSubject.TargetUnit"/>.</summary>
        internal static bool ContainsTargetUnitQuery(Condition<ConditionContext> condition)
        {
            return ContainsCondition(condition, c => c is TagQueryCondition query && query.Subject == ConditionSubject.TargetUnit);
        }

        private void ValidateBehavior(ValidationResult result)
        {
            if (behavior != AbilityBehavior.Channel && OnChannelTick != null && OnChannelTick.Count > 0)
            {
                result.AddWarning($"{OnChannelTick.Count} ações em \"A cada passo\" não vão rodar: a habilidade não é Channel.", nameof(OnChannelTick));
            }

            if (behavior == AbilityBehavior.Passive)
            {
                if (CostEffect != null) result.AddWarning("Passive com custo: o custo é cobrado em cada ativação automática.", nameof(CostEffect));
                if (CooldownEffect != null) result.AddWarning("Passive com recarga: a recarga é cobrada em cada ativação automática.", nameof(CooldownEffect));
            }

            if (behavior == AbilityBehavior.Channel && !PositiveAtSomeLevel(ChannelDuration)
                && (CancelledByTags == null || CancelledByTags.Count == 0))
            {
                result.AddWarning("Channel sem duração e sem Cancelled By Tags: só termina cancelada por código.", nameof(channelDuration));
            }

            if (autocast == null || !autocast.Allowed) return;

            if (behavior == AbilityBehavior.Passive || behavior == AbilityBehavior.Toggle)
            {
                result.AddError($"Autocast permitido em {behavior}: só vale em Instant e Channel.", nameof(autocast) + ".allowed");
            }
            else if (!HasAutocastTrigger())
            {
                result.AddWarning("Autocast permitido sem WhenReadyTrigger nem gatilho \"só com autocast\": o interruptor não faz nada.",
                    nameof(autocast) + ".allowed");
            }
        }

        private bool HasAutocastTrigger()
        {
            if (triggers == null) return false;

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] is WhenReadyTrigger || (triggers[i] != null && triggers[i].OnlyWithAutocast)) return true;
            }

            return false;
        }

        private void ValidateCommit(ValidationResult result, List<string> commits)
        {
            if (commitMode == CommitMode.Manual && commits.Count == 0)
            {
                result.AddError("Commit Mode Manual sem ação Commit: a habilidade nunca cobra recarga, custo nem carga.", nameof(commitMode));
            }

            if (commitMode != CommitMode.OnStart) return;

            for (int i = 0; i < commits.Count; i++)
            {
                result.AddWarning("Ação Commit com Commit Mode On Start: a ativação já foi cobrada, e a ação não faz nada.", commits[i]);
            }
        }

        private void ValidateTriggers(ValidationResult result, List<GameplayTag> ownerEvents)
        {
            if (triggers == null) return;

            for (int i = 0; i < triggers.Count; i++)
            {
                string path = $"{nameof(triggers)}.Array.data[{i}]";
                AbilityTrigger trigger = triggers[i];
                trigger?.Validate(result, path);

                if (trigger is GameplayEventTrigger byEvent && byEvent.Tag.IsValid && SendsToOwner(ownerEvents, byEvent.Tag))
                {
                    result.AddWarning("A própria habilidade envia este evento ao dono: a trava evita o laço, e ela nunca se redispara.",
                        path + ".tag");
                }
                else if (trigger is WhenReadyTrigger whenReady && whenReady.Selector != null && !PositiveAtSomeLevel(CastRange))
                {
                    result.AddWarning("WhenReadyTrigger com seletor e Cast Range 0: o alvo pode estar a qualquer distância.", path + ".selector");
                }
            }
        }

        private static bool SendsToOwner(List<GameplayTag> ownerEvents, GameplayTag triggerTag)
        {
            for (int i = 0; i < ownerEvents.Count; i++)
            {
                if (ownerEvents[i].Matches(triggerTag)) return true;
            }

            return false;
        }

        /// <summary>
        /// Junta em <paramref name="results"/> os efeitos que as ações de "Ao ativar", "Ao terminar", "Ao ser cancelada",
        /// "A cada passo", "Ao ser concedida" e "Ao ser retirada" aplicam, pelo <see cref="GameplayAction.CollectEffects"/> de cada uma.
        /// </summary>
        public void CollectActionEffects(List<GameplayEffect> results)
        {
            CollectEffects(OnActivate, results);
            CollectEffects(OnEnd, results);
            CollectEffects(OnCancel, results);
            CollectEffects(OnChannelTick, results);
            CollectEffects(OnGranted, results);
            CollectEffects(OnRemoved, results);
        }

        private static void CollectEffects(List<GameplayAction> actions, List<GameplayEffect> results)
        {
            if (actions == null) return;
            for (int i = 0; i < actions.Count; i++)
            {
                actions[i]?.CollectEffects(results);
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
            CollectActionEffects(effects);

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
                usesCastRange |= ContainsCondition(rule.Condition, c => c is CastRangeCondition);
                if (ContainsTargetUnitQuery(rule.Condition))
                {
                    result.AddWarning(TargetUnitQueryWarning, path + ".condition");
                }
            }

            if (usesCastRange) ValidateCastRange(result);
        }

        // Procura a condição também dentro das composições do GAS
        private static bool ContainsCondition(Condition<ConditionContext> condition, Predicate<Condition<ConditionContext>> match)
        {
            switch (condition)
            {
                case null:
                    return false;
                case NotCondition not:
                    return match(not) || ContainsCondition(not.Condition, match);
                case AndCondition and:
                    return match(and) || AnyContains(and.Conditions, match);
                case OrCondition or:
                    return match(or) || AnyContains(or.Conditions, match);
                default:
                    return match(condition);
            }
        }

        private static bool AnyContains(List<Condition<ConditionContext>> conditions, Predicate<Condition<ConditionContext>> match)
        {
            if (conditions == null) return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                if (ContainsCondition(conditions[i], match)) return true;
            }

            return false;
        }

        // Positivo em algum nível de 1 a MaxLevel; um nome fora da tabela vale o fallback, sem o aviso no Console
        private bool PositiveAtSomeLevel(ScalableFloat value)
        {
            AbilityValues table = TableFor(value);
            for (int level = 1; level <= Mathf.Max(1, maxLevel); level++)
            {
                if (value.Evaluate(level, table) > 0f) return true;
            }

            return false;
        }

        private AbilityValues TableFor(ScalableFloat value)
        {
            bool hasName = value.Mode == ScalableFloat.ScalableMode.Table && values != null && values.IndexOf(value.TableName) >= 0;
            return hasName ? values : null;
        }

        // Alcance negativo em algum nível é erro; alcance 0 em todos é "sem limite", e a CastRangeCondition sempre passa
        private void ValidateCastRange(ValidationResult result)
        {
            ScalableFloat range = CastRange;
            AbilityValues table = TableFor(range);

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

        /// <summary>
        /// Acrescenta as linhas da <see cref="Description"/> a <c>context.Lines</c>. Monte o contexto com
        /// <see cref="DescriptionContext.Reset"/> (habilidade, nível e ASC de origem) e escreva o texto com o
        /// <see cref="DescriptionWriter"/>.
        /// </summary>
        public void Describe(DescriptionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            Description?.Describe(context);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (string.IsNullOrEmpty(legacyDisplayName)) return;
            localizedName = new LocalizedText(null, legacyDisplayName);
            legacyDisplayName = null;
        }
    }
}
