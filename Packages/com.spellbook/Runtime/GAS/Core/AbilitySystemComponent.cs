using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.Tags;
using SpellBook.Time;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
namespace SpellBook.GAS.Core
{
    [RequireComponent(typeof(AttributeSet))]
    [HelpURL(DocUrls.Gas + "#abilitysystemcomponent")]
    public class AbilitySystemComponent : MonoBehaviour, IValidatable
    {
        [SerializeField] private AttributeSet attributeSet;
        [Tooltip("Habilidades concedidas automaticamente no Awake.")]
        [SerializeField] private List<GameplayAbility> startingAbilities = new List<GameplayAbility>();
        [Tooltip("Efeitos aplicados no Awake, na ordem da lista, depois do AttributeSet e das habilidades, com o próprio ASC como origem.")]
        [SerializeField] private List<GameplayEffect> startingEffects = new List<GameplayEffect>();
        [Tooltip("Fonte de tempo de efeitos e recargas. Vazio usa o tempo escalado da Unity.")]
        [SerializeField] private TimeSourceAsset timeSource;
        [Tooltip("Avança sozinho no Update pela fonte de tempo. Desligado, só avança por Tick.")]
        [SerializeField] private bool autoTick = true;

        private ITimeSource _timeSourceOverride;
        private double _timeCursor;
        private double _tickedTime;

        // Contagem de referência: várias fontes podem conceder a mesma tag
        private readonly Dictionary<GameplayTag, int> _activeTags = new Dictionary<GameplayTag, int>();
        private readonly List<ActiveEffectInstance> _activeEffects = new List<ActiveEffectInstance>();
        private readonly List<AbilitySpec> _abilities = new List<AbilitySpec>();
        private readonly HashSet<GameplayAbility> _warnedAboveMaxLevel = new HashSet<GameplayAbility>();

        public event Action<GameplayTag, bool> OnTagChanged;
        public event Action<AbilitySpec> OnAbilityActivated;
        public event Action<AbilitySpec, bool> OnAbilityEnded; // spec, cancelled
        /// <summary>
        /// Ativação recusada por <c>TryActivateAbility</c>: a habilidade e a <see cref="ActivationFailure"/> da checagem, igual à
        /// de <see cref="GetActivationResult(GameplayAbility, in AbilityTarget, out ActivationFailure)"/>. A consulta não dispara.
        /// </summary>
        public event Action<GameplayAbility, ActivationFailure> OnAbilityFailed;

        /// <summary>Instância nova de efeito com duração, depois de tags e modificadores. Instantâneo, renovação e pilha não disparam.</summary>
        public event Action<ActiveEffectInstance> OnEffectApplied;

        /// <summary>Instância saiu do ASC, depois de devolver tags e modificadores. Dispara uma vez por instância.</summary>
        public event Action<ActiveEffectInstance, EffectRemovalReason> OnEffectRemoved;

        /// <summary>Contagem de pilhas mudou de fato (instância, antes, depois).</summary>
        public event Action<ActiveEffectInstance, int, int> OnEffectStacksChanged;

        public AttributeSet AttributeSet => attributeSet;
        public IReadOnlyList<AbilitySpec> Abilities => _abilities;

        /// <summary>Instâncias de efeito ativas, na ordem de aplicação, sem cópia.</summary>
        public IReadOnlyList<ActiveEffectInstance> ActiveEffects => _activeEffects;

        /// <summary>
        /// Fonte de tempo em uso: a dada por <see cref="SetTimeSource"/>, senão a do Inspector, senão
        /// <see cref="TimeSourceAsset.Scaled"/>.
        /// </summary>
        public ITimeSource TimeSource
        {
            get
            {
                if (_timeSourceOverride != null) return _timeSourceOverride;
                return timeSource != null ? timeSource : TimeSourceAsset.Scaled;
            }
        }

        /// <summary>Relógio próprio do ASC: a soma de todos os <see cref="Tick"/> com delta positivo, em segundos.</summary>
        public double TickedTime => _tickedTime;

        private void Awake()
        {
            if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();

            if (startingAbilities != null)
            {
                foreach (var ability in startingAbilities) GiveAbility(ability);
            }

            ApplyStartingEffects();
        }

        // Ouvintes externos ainda não existem aqui: quem assina depois lê ActiveEffects.
        private void ApplyStartingEffects()
        {
            if (startingEffects == null) return;
            if (attributeSet != null) attributeSet.EnsureInitialized();

            for (int i = 0; i < startingEffects.Count; i++)
            {
                GameplayEffect effect = startingEffects[i];
                if (effect == null) continue;

                EffectApplicationResult result = TryApplyEffect(effect, this, out _);
                bool failed = result == EffectApplicationResult.BlockedByImmunity
                    || result == EffectApplicationResult.MissingRequirements
                    || result == EffectApplicationResult.Invalid;
                if (failed && Debug.isDebugBuild)
                {
                    Debug.LogWarning($"[GAS] Efeito de partida '{effect.name}' de '{name}' não foi aplicado: {result}.", this);
                }
            }
        }

        public void Validate(ValidationResult result)
        {
            if (startingEffects == null) return;
            for (int i = 0; i < startingEffects.Count; i++)
            {
                if (startingEffects[i] == null)
                {
                    result.AddError($"Starting Effects tem uma entrada vazia (elemento {i}).", $"{nameof(startingEffects)}.Array.data[{i}]");
                }
            }
        }

        private void OnEnable()
        {
            OnTagChanged += HandleTagChangedForCancellation;
            _timeCursor = TimeSource.ElapsedTime;
        }

        private void OnDisable()
        {
            OnTagChanged -= HandleTagChangedForCancellation;
            CancelAllAbilities();
            // Ligações ao vivo em outro ASC (o instigador) prenderiam este ASC e o recalculariam depois de destruído
            for (int i = 0; i < _activeEffects.Count; i++) _activeEffects[i].DisposeLiveBindings();
        }

        private void Update()
        {
            // Consumo por diferença (ADR 0014): o cursor anda mesmo sem autoTick, para não saltar ao religar
            double now = TimeSource.ElapsedTime;
            float deltaTime = (float)(now - _timeCursor);
            _timeCursor = now;
            if (autoTick) Tick(deltaTime);
        }

        /// <summary>
        /// Troca a fonte de tempo por código (por exemplo, um <see cref="ManualTimeSource"/> num teste).
        /// <c>null</c> volta à fonte do Inspector. O tempo passado na fonte antiga não é contado.
        /// </summary>
        public void SetTimeSource(ITimeSource source)
        {
            _timeSourceOverride = source;
            _timeCursor = TimeSource.ElapsedTime;
        }

        /// <summary>
        /// Avança efeitos e recargas em <paramref name="deltaTime"/> segundos e soma em <see cref="TickedTime"/>.
        /// Ignora delta zero ou negativo. Com <c>autoTick</c> ligado, o próprio ASC chama a cada quadro.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f)) return;

            _tickedTime += deltaTime;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                // Uma remoção no Update de uma instância pode tirar outras da lista
                if (i >= _activeEffects.Count) continue;
                _activeEffects[i].Update(deltaTime);
            }

            for (int i = 0; i < _abilities.Count; i++)
            {
                AbilitySpec spec = _abilities[i];
                if (!HasCharges(spec)) continue;

                // Uma recarga que o máximo novo acabou de começar não desconta este Tick
                bool wasRecharging = spec.ChargeRecharging;
                RefreshMaxCharges(spec);
                if (!wasRecharging || !spec.ChargeRecharging) continue;
                spec.ChargeRechargeRemaining -= deltaTime;
                AdvanceChargeRecharge(spec);
            }
        }

        public float GetAttributeValue(AttributeDefinition def)
        {
            return attributeSet != null ? attributeSet.GetValue(def) : 0f;
        }

        /// <summary>Atalho de <see cref="Attributes.AttributeSet.TryGetValue"/>, sem aviso. Sem <c>AttributeSet</c>: falso e 0.</summary>
        public bool TryGetAttributeValue(AttributeDefinition def, out float value)
        {
            if (attributeSet != null) return attributeSet.TryGetValue(def, out value);
            value = 0f;
            return false;
        }

        /// <summary>Atalho de <see cref="Attributes.AttributeSet.GetValueOrDefault"/>, sem aviso. Sem <c>AttributeSet</c>: <paramref name="fallback"/>.</summary>
        public float GetAttributeValueOrDefault(AttributeDefinition def, float fallback = 0f)
        {
            return attributeSet != null ? attributeSet.GetValueOrDefault(def, fallback) : fallback;
        }

        /// <summary>Atalho de <see cref="Attributes.AttributeSet.HasAttribute"/>. Sem <c>AttributeSet</c>: falso.</summary>
        public bool HasAttribute(AttributeDefinition def)
        {
            return attributeSet != null && attributeSet.HasAttribute(def);
        }

        /// <summary>Atalho de <see cref="Attributes.AttributeSet.GetBaseValue"/>, sem aviso. Sem <c>AttributeSet</c>: 0.</summary>
        public float GetAttributeBaseValue(AttributeDefinition def)
        {
            return attributeSet != null ? attributeSet.GetBaseValue(def) : 0f;
        }

        #region Ability Management
        /// <summary>
        /// Concede uma habilidade ao ASC no nível limitado a [1, MaxLevel], como <see cref="SetAbilityLevel"/>.
        /// Se já concedida, retorna o spec existente sem mudar o nível.
        /// </summary>
        public AbilitySpec GiveAbility(GameplayAbility ability, int level = 1)
        {
            if (ability == null) return null;

            var existing = FindSpec(ability);
            if (existing != null) return existing;

            var spec = new AbilitySpec(ability, ClampLevel(ability, level));
            InitCharges(spec);
            _abilities.Add(spec);
            OnAbilityGranted?.Invoke(spec);
            return spec;
        }

        /// <summary>
        /// Remove uma habilidade concedida, cancelando-a se estiver ativa.
        /// </summary>
        public void RemoveAbility(GameplayAbility ability)
        {
            var spec = FindSpec(ability);
            if (spec == null) return;

            CancelAbility(spec);
            if (_abilities.Remove(spec)) OnAbilityRemoved?.Invoke(spec);
        }

        /// <summary>Habilidade nova concedida, já na lista. Conceder de novo não dispara; as de <c>startingAbilities</c> disparam no <c>Awake</c>.</summary>
        public event Action<AbilitySpec> OnAbilityGranted;

        /// <summary>Habilidade removida, depois do cancelamento e da saída da lista.</summary>
        public event Action<AbilitySpec> OnAbilityRemoved;

        /// <summary>Nível mudou de fato por <see cref="SetAbilityLevel"/>: (spec, antes, depois).</summary>
        public event Action<AbilitySpec, int, int> OnAbilityLevelChanged;

        /// <summary>Nível da habilidade concedida, ou 0 se ela não foi concedida.</summary>
        public int GetAbilityLevel(GameplayAbility ability)
        {
            var spec = FindSpec(ability);
            return spec != null ? spec.Level : 0;
        }

        /// <summary>
        /// Muda o nível da habilidade concedida, limitado a [1, MaxLevel] (acima do máximo, um aviso por habilidade).
        /// Dispara <see cref="OnAbilityLevelChanged"/> só se o nível mudou. Devolve <c>false</c> se não foi concedida.
        /// </summary>
        public bool SetAbilityLevel(GameplayAbility ability, int level)
        {
            var spec = FindSpec(ability);
            if (spec == null) return false;

            int before = spec.Level;
            int after = ClampLevel(ability, level);
            if (after == before) return true;

            spec.Level = after;
            if (HasCharges(spec)) RefreshMaxCharges(spec);
            OnAbilityLevelChanged?.Invoke(spec, before, after);
            return true;
        }

        private int ClampLevel(GameplayAbility ability, int level)
        {
            int max = Mathf.Max(1, ability.MaxLevel);
            if (level > max)
            {
                if (_warnedAboveMaxLevel.Add(ability))
                {
                    Debug.LogWarning($"[GAS] Nível {level} de '{ability.name}' passa do MaxLevel {max} e foi limitado a {max}.", this);
                }

                return max;
            }

            return Mathf.Max(1, level);
        }

        public AbilitySpec FindSpec(GameplayAbility ability)
        {
            if (ability == null) return null;
            for (int i = 0; i < _abilities.Count; i++)
            {
                if (_abilities[i].Ability == ability) return _abilities[i];
            }
            return null;
        }

        public bool IsAbilityActive(GameplayAbility ability)
        {
            var spec = FindSpec(ability);
            return spec != null && spec.IsActive;
        }

        #endregion

        #region Cooldown Control
        /// <summary>
        /// Recarga de uma habilidade concedida começou, mudou por código ou terminou. Só dispara para a recarga aplicada pela
        /// ativação (<see cref="CooldownChange.Started"/>), até ela terminar.
        /// </summary>
        public event Action<AbilitySpec, CooldownChange> OnCooldownChanged;

        /// <summary>
        /// Tira <paramref name="seconds"/> de toda instância que conta para a recarga da habilidade (o próprio
        /// <c>CooldownEffect</c> ou uma tag filha); as que chegam a zero expiram. Com cargas, tira também da recarga da carga
        /// em curso, que pode devolver mais de uma carga. Segundos negativos ou NaN são ignorados com aviso. Habilidade nula,
        /// sem recarga ou pronta: <c>false</c>.
        /// </summary>
        public bool ReduceCooldown(GameplayAbility ability, float seconds)
        {
            bool hasCooldown = HasCooldownInstance(ability);
            AbilitySpec chargeSpec = FindRechargingSpec(ability);
            if (!hasCooldown && chargeSpec == null) return false;
            if (!(seconds >= 0f))
            {
                if (Debug.isDebugBuild) Debug.LogWarning($"[GAS] {nameof(ReduceCooldown)} ignorado: segundos inválidos ({seconds}) em '{ability.name}'.", this);
                return false;
            }

            bool changed = false;
            if (hasCooldown)
            {
                GameplayEffect cooldownEffect = ability.CooldownEffect;
                for (int i = _activeEffects.Count - 1; i >= 0; i--)
                {
                    // Uma expiração pode disparar ouvintes que mexem na lista
                    if (i >= _activeEffects.Count) continue;
                    ActiveEffectInstance instance = _activeEffects[i];
                    if (IsCooldownMatch(instance.Effect, cooldownEffect)) changed |= ReduceEffectDuration(instance, seconds);
                }
                if (changed) RaiseCooldownChanged(ability);
            }

            if (chargeSpec != null && chargeSpec.ChargeRecharging && seconds > 0f)
            {
                chargeSpec.ChargeRechargeRemaining -= seconds;
                AdvanceChargeRecharge(chargeSpec);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Remove toda instância que conta para a recarga da habilidade (motivo <see cref="EffectRemovalReason.Removed"/>).
        /// Uma tag de recarga dada fora de efeito (<see cref="AddTag"/>) continua bloqueando. Com cargas e a recarga da carga
        /// correndo, devolve uma carga e recomeça a próxima com a duração cheia (no máximo, para). Nula, sem recarga ou pronta:
        /// <c>false</c>.
        /// </summary>
        public bool ResetCooldown(GameplayAbility ability)
        {
            bool hasCooldown = HasCooldownInstance(ability);
            AbilitySpec chargeSpec = FindRechargingSpec(ability);
            if (!hasCooldown && chargeSpec == null) return false;

            if (hasCooldown)
            {
                GameplayEffect cooldownEffect = ability.CooldownEffect;
                for (int i = _activeEffects.Count - 1; i >= 0; i--)
                {
                    if (i >= _activeEffects.Count) continue;
                    ActiveEffectInstance instance = _activeEffects[i];
                    if (IsCooldownMatch(instance.Effect, cooldownEffect)) RemoveEffectCore(instance, EffectRemovalReason.Removed);
                }
            }

            if (chargeSpec != null && chargeSpec.ChargeRecharging)
            {
                // Recarga vencida sem sobra: volta uma carga e a próxima começa com a duração cheia
                chargeSpec.ChargeRechargeRemaining = 0f;
                AdvanceChargeRecharge(chargeSpec);
            }
            return true;
        }

        /// <summary>
        /// Volta toda instância que conta para a recarga da habilidade à duração aplicada e, com cargas, a recarga da carga em
        /// curso à duração aplicada dela. Nula, sem recarga ou pronta: <c>false</c>.
        /// </summary>
        public bool RestartCooldown(GameplayAbility ability)
        {
            bool hasCooldown = HasCooldownInstance(ability);
            AbilitySpec chargeSpec = FindRechargingSpec(ability);
            if (!hasCooldown && chargeSpec == null) return false;

            bool changed = false;
            if (hasCooldown)
            {
                GameplayEffect cooldownEffect = ability.CooldownEffect;
                for (int i = _activeEffects.Count - 1; i >= 0; i--)
                {
                    if (i >= _activeEffects.Count) continue;
                    ActiveEffectInstance instance = _activeEffects[i];
                    if (IsCooldownMatch(instance.Effect, cooldownEffect)) changed |= RestartEffectDuration(instance);
                }
                if (changed) RaiseCooldownChanged(ability);
            }

            if (chargeSpec != null && chargeSpec.ChargeRecharging
                && chargeSpec.ChargeRechargeRemaining != chargeSpec.ChargeRechargeDuration)
            {
                chargeSpec.ChargeRechargeRemaining = chargeSpec.ChargeRechargeDuration;
                changed = true;
            }
            return changed;
        }

        /// <summary>Spec concedido da habilidade com a recarga de carga correndo, ou nulo.</summary>
        private AbilitySpec FindRechargingSpec(GameplayAbility ability)
        {
            AbilitySpec spec = FindSpec(ability);
            return HasCharges(spec) && spec.ChargeRecharging ? spec : null;
        }

        private bool HasCooldownInstance(GameplayAbility ability)
        {
            return ability != null && FindCooldownInstance(ability.CooldownEffect, out _) != null;
        }

        /// <summary><see cref="CooldownChange.Changed"/> só se a recarga acompanhada continua; o fim sai por <see cref="RaiseCooldownsEnded"/>.</summary>
        private void RaiseCooldownChanged(GameplayAbility ability)
        {
            AbilitySpec spec = FindSpec(ability);
            if (spec == null || !spec.CooldownTracked || !IsOnCooldown(ability.CooldownEffect)) return;
            OnCooldownChanged?.Invoke(spec, CooldownChange.Changed);
        }

        /// <summary>Depois de cada remoção de efeito: habilidade acompanhada que ficou pronta dispara <see cref="CooldownChange.Ended"/>.</summary>
        private void RaiseCooldownsEnded()
        {
            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                // Um ouvinte pode remover habilidades
                if (i >= _abilities.Count) continue;
                AbilitySpec spec = _abilities[i];
                if (!spec.CooldownTracked || IsOnCooldown(spec.Ability.CooldownEffect)) continue;

                spec.CooldownTracked = false;
                OnCooldownChanged?.Invoke(spec, CooldownChange.Ended);
            }
        }
        #endregion

        #region Charges
        /// <summary>
        /// As cargas de uma habilidade mudaram de fato: (spec, antes, depois). Sai no gasto da ativação, a cada carga que volta
        /// pela recarga (também pelas de <see cref="ReduceCooldown"/> e <see cref="ResetCooldown"/>), em
        /// <see cref="AddCharges"/> e <see cref="RefillCharges"/>, e quando o máximo desce abaixo das cargas.
        /// </summary>
        public event Action<AbilitySpec, int, int> OnChargesChanged;

        /// <summary>Cargas disponíveis do <paramref name="spec"/>; nulo ou sem cargas, 0.</summary>
        public int GetCharges(AbilitySpec spec)
        {
            return HasCharges(spec) ? Mathf.Min(spec.Charges, spec.MaxCharges) : 0;
        }

        /// <summary>Máximo de cargas do <paramref name="spec"/>; nulo ou sem cargas, 0.</summary>
        public int GetMaxCharges(AbilitySpec spec)
        {
            return HasCharges(spec) ? spec.MaxCharges : 0;
        }

        /// <summary>Segundos até a próxima carga; recarga parada, spec nulo ou sem cargas, 0.</summary>
        public float GetChargeRechargeRemaining(AbilitySpec spec)
        {
            return HasCharges(spec) && spec.ChargeRecharging ? Mathf.Max(0f, spec.ChargeRechargeRemaining) : 0f;
        }

        /// <summary>Recarga da carga em curso, de 1 (acabou de começar) a 0, pela duração aplicada; parada, 0.</summary>
        public float GetChargeRechargeNormalized(AbilitySpec spec)
        {
            if (!HasCharges(spec) || !spec.ChargeRecharging || !(spec.ChargeRechargeDuration > 0f)) return 0f;
            return Mathf.Clamp01(spec.ChargeRechargeRemaining / spec.ChargeRechargeDuration);
        }

        /// <summary>
        /// Soma <paramref name="count"/> cargas, limitadas ao máximo; no máximo, a recarga para. Devolve se as cargas mudaram:
        /// <paramref name="count"/> ≤ 0, spec nulo, de outro ASC, sem cargas ou já cheio dão <c>false</c>.
        /// </summary>
        public bool AddCharges(AbilitySpec spec, int count)
        {
            if (count <= 0 || !HasCharges(spec) || !_abilities.Contains(spec)) return false;

            int before = spec.Charges;
            int after = spec.MaxCharges - before < count ? spec.MaxCharges : before + count;
            if (after <= before) return false;

            spec.Charges = after;
            if (after >= spec.MaxCharges) StopChargeRecharge(spec);
            OnChargesChanged?.Invoke(spec, before, after);
            return true;
        }

        /// <summary>Enche as cargas até o máximo e para a recarga. Devolve se as cargas mudaram.</summary>
        public bool RefillCharges(AbilitySpec spec)
        {
            return HasCharges(spec) && AddCharges(spec, spec.MaxCharges - spec.Charges);
        }

        private static bool HasCharges(AbilitySpec spec)
        {
            return spec != null && spec.Ability != null && spec.Ability.Charges.Enabled;
        }

        // Concessão: o máximo é avaliado no nível do spec e as cargas nascem cheias, com a recarga parada
        private void InitCharges(AbilitySpec spec)
        {
            if (!HasCharges(spec)) return;

            spec.MaxCharges = EvaluateMaxCharges(spec);
            spec.Charges = spec.MaxCharges;
            StopChargeRecharge(spec);
        }

        // Máximo no nível do spec, com a tabela da habilidade e o ASC como instigador: inteiro, para baixo, pelo menos 0
        private int EvaluateMaxCharges(AbilitySpec spec)
        {
            var context = new EffectContext(this, this, spec.Ability, spec.Level, false, Vector3.zero, null);
            return Mathf.Max(0, Mathf.FloorToInt(spec.Ability.Charges.MaxCharges.Evaluate(in context)));
        }

        // Reavalia o máximo (mudança de nível e cada Tick). O que desce limita as cargas e, no máximo, para a recarga; o que
        // sobe não enche: abaixo do máximo, a recarga parada começa (R18)
        private void RefreshMaxCharges(AbilitySpec spec)
        {
            int max = EvaluateMaxCharges(spec);
            if (max == spec.MaxCharges) return;

            spec.MaxCharges = max;
            int before = spec.Charges;
            if (before > max) spec.Charges = max;
            if (spec.Charges >= max) StopChargeRecharge(spec);
            else if (!spec.ChargeRecharging) StartChargeRecharge(spec, 0f);
            if (spec.Charges != before) OnChargesChanged?.Invoke(spec, before, spec.Charges);
        }

        // Commit: gasta 1 carga; abaixo do máximo com a recarga parada, a recarga da próxima começa
        private void SpendCharge(AbilitySpec spec)
        {
            int before = spec.Charges;
            spec.Charges = before - 1;
            if (!spec.ChargeRecharging && spec.Charges < spec.MaxCharges) StartChargeRecharge(spec, 0f);
            OnChargesChanged?.Invoke(spec, before, spec.Charges);
        }

        // Repõe uma carga por recarga vencida, passando o excesso adiante (recarga sequencial); no máximo, para
        private void AdvanceChargeRecharge(AbilitySpec spec)
        {
            while (spec.ChargeRecharging && spec.ChargeRechargeRemaining <= 0f)
            {
                if (spec.Charges >= spec.MaxCharges)
                {
                    StopChargeRecharge(spec);
                    return;
                }

                int before = spec.Charges;
                spec.Charges = before + 1;
                if (spec.Charges < spec.MaxCharges) StartChargeRecharge(spec, spec.ChargeRechargeRemaining);
                else StopChargeRecharge(spec);
                OnChargesChanged?.Invoke(spec, before, spec.Charges);
            }
        }

        private void StartChargeRecharge(AbilitySpec spec, float carriedOver)
        {
            GameplayAbility ability = spec.Ability;
            float duration = ability.ApplyCooldownReduction(ability.Charges.RechargeTime.Evaluate(spec.Level, ability.Values), this);
            spec.ChargeRecharging = true;
            spec.ChargeRechargeDuration = duration;
            spec.ChargeRechargeRemaining = duration + carriedOver;
        }

        private static void StopChargeRecharge(AbilitySpec spec)
        {
            spec.ChargeRecharging = false;
            spec.ChargeRechargeRemaining = 0f;
            spec.ChargeRechargeDuration = 0f;
        }
        #endregion

        #region Ability Activation
        /// <summary>Resultado da ativação sem alvo (<see cref="AbilityTarget.None"/>), sem ativar nem mudar estado.</summary>
        public AbilityActivationResult GetActivationResult(GameplayAbility ability)
        {
            return GetActivationResult(ability, AbilityTarget.None, out _);
        }

        /// <summary>
        /// Resultado da ativação com o <paramref name="target"/>, e o motivo em <paramref name="failure"/>, pelo mesmo caminho de
        /// <see cref="TryActivateAbility(GameplayAbility, in AbilityTarget)"/>, sem ativar, sem evento e sem alocar.
        /// </summary>
        public AbilityActivationResult GetActivationResult(GameplayAbility ability, in AbilityTarget target, out ActivationFailure failure)
        {
            CheckActivation(ability != null ? FindSpec(ability) : null, ability, in target, out failure);
            return failure.Result;
        }

        public AbilityActivationResult TryActivateAbility(GameplayAbility ability, Vector3 targetPos)
        {
            return TryActivateAbility(ability, AbilityTarget.AtPoint(targetPos));
        }

        /// <summary>Checa (<see cref="GetActivationResult(GameplayAbility, in AbilityTarget, out ActivationFailure)"/>) e, se passou, ativa.</summary>
        public AbilityActivationResult TryActivateAbility(GameplayAbility ability, in AbilityTarget target)
        {
            return TryActivate(ability != null ? FindSpec(ability) : null, ability, in target);
        }

        public AbilityActivationResult TryActivateAbility(AbilitySpec spec, Vector3 targetPos)
        {
            return TryActivateAbility(spec, AbilityTarget.AtPoint(targetPos));
        }

        /// <summary>Como <see cref="TryActivateAbility(GameplayAbility, in AbilityTarget)"/>, pelo spec; spec de outro ASC dá <c>NotGranted</c>.</summary>
        public AbilityActivationResult TryActivateAbility(AbilitySpec spec, in AbilityTarget target)
        {
            if (spec == null || !_abilities.Contains(spec))
            {
                var failure = new ActivationFailure(spec == null
                    ? AbilityActivationResult.InvalidAbility
                    : AbilityActivationResult.NotGranted);
                OnAbilityFailed?.Invoke(spec?.Ability, failure);
                return failure.Result;
            }
            return TryActivate(spec, spec.Ability, in target);
        }

        private AbilityActivationResult TryActivate(AbilitySpec spec, GameplayAbility ability, in AbilityTarget target)
        {
            if (!CheckActivation(spec, ability, in target, out ActivationFailure failure))
            {
                OnAbilityFailed?.Invoke(ability, failure);
                return failure.Result;
            }

            ActivateSpec(spec, target.Point);
            return AbilityActivationResult.Success;
        }

        // Caminho único das checagens (ADR 0022): pré-requisitos, tags, recarga, custo e regras, nessa ordem. Não escreve estado.
        private bool CheckActivation(AbilitySpec spec, GameplayAbility ability, in AbilityTarget target, out ActivationFailure failure)
        {
            if (ability == null) return Fail(AbilityActivationResult.InvalidAbility, default, out failure);
            if (spec == null) return Fail(AbilityActivationResult.NotGranted, default, out failure);
            if (spec.IsActive) return Fail(AbilityActivationResult.AlreadyActive, default, out failure);

            if (TryFindTag(ability.BlockedTags, true, out GameplayTag blocking))
            {
                return Fail(AbilityActivationResult.BlockedByTags, blocking, out failure);
            }
            if (TryFindTag(ability.RequiredTags, false, out GameplayTag missing))
            {
                return Fail(AbilityActivationResult.MissingRequiredTags, missing, out failure);
            }
            if (IsBlockedByActiveAbility(ability))
            {
                return Fail(AbilityActivationResult.BlockedByActiveAbility, ability.AbilityTag, out failure);
            }
            // Checagem 2: com cargas, "tem carga?" vem antes do Cooldown, que continua valendo entre usos (ADR 0032, item 5)
            if (HasCharges(spec) && Mathf.Min(spec.Charges, spec.MaxCharges) < 1)
            {
                return Fail(AbilityActivationResult.NoCharges, default, out failure);
            }
            if (IsOnCooldown(ability.CooldownEffect)) return Fail(AbilityActivationResult.OnCooldown, default, out failure);
            if (!CanAffordCost(ability.CostEffect, ability, spec.Level))
            {
                return Fail(AbilityActivationResult.CannotAffordCost, default, out failure);
            }

            List<ActivationRule> rules = ability.ActivationConditions;
            if (rules.Count > 0)
            {
                var context = new ConditionContext(this, ability, spec.Level, spec, target, null);
                for (int i = 0; i < rules.Count; i++)
                {
                    ActivationRule rule = rules[i];
                    if (rule?.Condition == null || rule.Condition.Evaluate(in context)) continue;

                    failure = new ActivationFailure(AbilityActivationResult.BlockedByCondition, rule.FailureTag,
                        rule.EffectiveFailureKey, i, rule);
                    return false;
                }
            }

            failure = new ActivationFailure(AbilityActivationResult.Success);
            return true;
        }

        private static bool Fail(AbilityActivationResult result, GameplayTag reasonTag, out ActivationFailure failure)
        {
            failure = new ActivationFailure(result, reasonTag);
            return false;
        }

        // Primeira tag da lista que o ASC tem (held) ou que falta (!held), por índice, sem alocar
        private bool TryFindTag(List<GameplayTag> tags, bool held, out GameplayTag found)
        {
            if (tags != null)
            {
                for (int i = 0; i < tags.Count; i++)
                {
                    if (HasTag(tags[i]) != held) continue;
                    found = tags[i];
                    return true;
                }
            }
            found = default;
            return false;
        }

        public bool IsOnCooldown(GameplayEffect cooldownEffect)
        {
            if (cooldownEffect == null) return false;
            // Sem o boxing do enumerador: a remoção de efeito consulta a recarga das habilidades acompanhadas
            if (HasAnyTagInList(cooldownEffect.GrantedTags)) return true;
            return GetCooldownTimeRemaining(cooldownEffect) > 0f;
        }

        /// <summary>
        /// Verifica se os modifiers Add negativos do custo deixariam algum atributo abaixo do MinValue.
        /// </summary>
        public bool CanAffordCost(GameplayEffect costEffect)
        {
            return CanAffordCost(costEffect, null, costEffect != null ? costEffect.DefaultLevel : 1);
        }

        /// <summary>Como <see cref="CanAffordCost(GameplayEffect)"/>, avaliando o custo no nível e na tabela da habilidade.</summary>
        internal bool CanAffordCost(GameplayEffect costEffect, GameplayAbility ability, int level)
        {
            if (costEffect == null || costEffect.Modifiers == null) return true;

            EffectContext context = EffectSpec.ForAbility(costEffect, this, ability, level).Context.ForApplication(this);
            foreach (var mod in costEffect.Modifiers)
            {
                if (mod == null || mod.Attribute == null || mod.Operation != ModifierOperation.Add) continue;

                float value = mod.ValueSource.Evaluate(in context);
                if (value >= 0f) continue;

                if (attributeSet == null || !attributeSet.TryGetState(mod.Attribute, out var state)) return false;
                if (state.CurrentValue + value < mod.Attribute.MinValue) return false;
            }
            return true;
        }

        private bool IsBlockedByActiveAbility(GameplayAbility ability)
        {
            if (!ability.AbilityTag.IsValid) return false;

            foreach (var spec in _abilities)
            {
                if (!spec.IsActive || spec.Ability.BlockAbilitiesWithTag == null) continue;
                foreach (var tag in spec.Ability.BlockAbilitiesWithTag)
                {
                    if (ability.AbilityTag.Matches(tag)) return true;
                }
            }
            return false;
        }

        private void ActivateSpec(AbilitySpec spec, Vector3 targetPos)
        {
            var ability = spec.Ability;

            // Commit: cooldown e custo
            if (ability.CooldownEffect != null)
            {
                EffectSpec cooldown = EffectSpec.ForAbility(ability.CooldownEffect, this, ability, spec.Level);
                ApplyEffect(cooldown.WithDurationOverride(ability.GetCooldownDuration(this, spec.Level)));
                // Recarga instantânea não deixa instância nem tag: não há o que acompanhar
                if (IsOnCooldown(ability.CooldownEffect))
                {
                    spec.CooldownTracked = true;
                    OnCooldownChanged?.Invoke(spec, CooldownChange.Started);
                }
            }
            if (ability.CostEffect != null) ApplyEffect(EffectSpec.ForAbility(ability.CostEffect, this, ability, spec.Level));
            if (HasCharges(spec)) SpendCharge(spec);

            CancelAbilitiesWithTags(ability.CancelAbilitiesWithTag, spec);

            var context = new AbilityContext(this, targetPos)
            {
                Ability = ability,
                Spec = spec,
                Level = spec.Level
            };

            spec.IsActive = true;
            spec.ActiveContext = context;
            AddTags(ability.ActivationOwnedTags);

            // Dispara eventos DEPOIS de aplicar o cooldown, para que a UI leia o tempo correto
            OnAbilityActivated?.Invoke(spec);

            // A tag concedida acima pode ter cancelado a própria habilidade
            if (!spec.IsActive || spec.ActiveContext != context) return;

            var routine = StartCoroutine(RunAbility(spec, context));
            // Se a habilidade terminou de forma síncrona, não guarda a rotina
            if (spec.IsActive && spec.ActiveContext == context) spec.Routine = routine;
        }

        private IEnumerator RunAbility(AbilitySpec spec, AbilityContext context)
        {
            yield return AbilityAction.RunSequence(spec.Ability.Actions, context);
            if (spec.IsActive && spec.ActiveContext == context) EndAbility(spec, false);
        }
        #endregion

        #region Ability Cancellation
        /// <summary>
        /// Interrompe a habilidade, chamando OnCancel nas ações em execução.
        /// </summary>
        public void CancelAbility(AbilitySpec spec)
        {
            if (spec == null || !spec.IsActive) return;

            var context = spec.ActiveContext;
            context.IsCancelled = true;

            if (spec.Routine != null) StopCoroutine(spec.Routine);
            foreach (var child in spec.ChildRoutines)
            {
                if (child != null) StopCoroutine(child);
            }

            // Ordem reversa: as ações mais internas restauram estado primeiro
            var running = context.RunningActions.ToArray();
            context.RunningActions.Clear();
            for (int i = running.Length - 1; i >= 0; i--)
            {
                try { running[i].OnCancel(context); }
                catch (Exception e) { Debug.LogException(e, this); }
            }

            EndAbility(spec, true);
        }

        public void CancelAbility(GameplayAbility ability) => CancelAbility(FindSpec(ability));

        /// <summary>
        /// Cancela habilidades ativas cujo AbilityTag corresponda a alguma das tags.
        /// </summary>
        public void CancelAbilitiesWithTags(IEnumerable<GameplayTag> tags, AbilitySpec ignore = null)
        {
            if (tags == null) return;

            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i >= _abilities.Count) continue;
                var spec = _abilities[i];
                if (spec == ignore || !spec.IsActive || !spec.Ability.AbilityTag.IsValid) continue;

                foreach (var tag in tags)
                {
                    if (spec.Ability.AbilityTag.Matches(tag))
                    {
                        CancelAbility(spec);
                        break;
                    }
                }
            }
        }

        public void CancelAllAbilities()
        {
            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i < _abilities.Count) CancelAbility(_abilities[i]);
            }
        }

        private void EndAbility(AbilitySpec spec, bool cancelled)
        {
            if (!spec.IsActive) return;

            spec.IsActive = false;
            spec.Routine = null;
            spec.ChildRoutines.Clear();
            spec.ActiveContext = null;

            RemoveTags(spec.Ability.ActivationOwnedTags);
            OnAbilityEnded?.Invoke(spec, cancelled);
        }

        private void HandleTagChangedForCancellation(GameplayTag tag, bool added)
        {
            if (!added) return;

            for (int i = _abilities.Count - 1; i >= 0; i--)
            {
                if (i >= _abilities.Count) continue;
                var spec = _abilities[i];
                if (!spec.IsActive || spec.Ability.CancelledByTags == null) continue;

                foreach (var cancelTag in spec.Ability.CancelledByTags)
                {
                    if (tag.Matches(cancelTag))
                    {
                        CancelAbility(spec);
                        break;
                    }
                }
            }
        }
        #endregion

        #region Tag Management
        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            _activeTags.TryGetValue(tag, out int count);
            _activeTags[tag] = count + 1;
            if (count == 0) OnTagChanged?.Invoke(tag, true);
        }

        public void RemoveTag(GameplayTag tag)
        {
            if (!tag.IsValid) return;
            if (!_activeTags.TryGetValue(tag, out int count)) return;

            if (count <= 1)
            {
                _activeTags.Remove(tag);
                OnTagChanged?.Invoke(tag, false);
            }
            else
            {
                _activeTags[tag] = count - 1;
            }
        }

        public void AddTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags) AddTag(tag);
        }

        public void RemoveTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags) RemoveTag(tag);
        }

        /// <summary>Contagem de referência da tag exata, sem hierarquia. Ausente ou inválida: 0.</summary>
        public int GetTagCount(GameplayTag tag)
        {
            if (!tag.IsValid) return 0;
            return _activeTags.TryGetValue(tag, out int count) ? count : 0;
        }

        /// <summary>Tags concedidas ao ASC com a contagem de referência de cada uma, sem cópia.</summary>
        public IReadOnlyDictionary<GameplayTag, int> TagCounts => _activeTags;

        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            foreach (var activeTag in _activeTags.Keys)
            {
                if (activeTag.Matches(tag)) return true;
            }
            return false;
        }

        public bool HasAllTags(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return true;
            foreach (var tag in tags) if (!HasTag(tag)) return false;
            return true;
        }

        public bool HasAnyTag(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return false;
            foreach (var tag in tags) if (HasTag(tag)) return true;
            return false;
        }

        /// <summary>
        /// As tags ativas satisfazem a <paramref name="query"/> (todas, alguma e nenhuma), com a regra de
        /// <see cref="HasTag"/>: a tag ativa filha satisfaz o pai. Consulta vazia casa; nula não casa. Não aloca.
        /// </summary>
        public bool HasMatching(GameplayTagQuery query)
        {
            if (query == null) return false;
            if (!HasAll(query.All)) return false;
            if (!query.Any.IsEmpty && !HasAny(query.Any)) return false;
            return !HasAny(query.None);
        }

        /// <summary>O ASC tem todas as <paramref name="tags"/> (hierárquico). Nula ou vazia: verdadeiro; tag inválida não casa. Não aloca.</summary>
        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return true;
            for (int i = 0; i < tags.Count; i++) if (!HasTag(tags[i])) return false;
            return true;
        }

        /// <summary>O ASC tem alguma das <paramref name="tags"/> (hierárquico). Nula ou vazia: falso; tag inválida não casa. Não aloca.</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++) if (HasTag(tags[i])) return true;
            return false;
        }
        #endregion

        #region Gameplay Effects
        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, AbilitySystemComponent source = null)
        {
            ApplyEffectCore(EffectSpec.Create(effect, source), out ActiveEffectInstance instance);
            return instance;
        }

        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source = null)
        {
            ApplyEffectCore(EffectSpec.Create(effect, source).WithDurationOverride(durationOverride), out ActiveEffectInstance instance);
            return instance;
        }

        /// <summary>Aplica o <paramref name="spec"/>, avaliando o efeito no nível e na tabela do contexto dele.</summary>
        public ActiveEffectInstance ApplyEffect(in EffectSpec spec)
        {
            ApplyEffectCore(in spec, out ActiveEffectInstance instance);
            return instance;
        }

        /// <summary>
        /// Como <see cref="TryApplyEffect(GameplayEffect, AbilitySystemComponent, out ActiveEffectInstance)"/>, com o nível, a
        /// tabela e a duração do <paramref name="spec"/>. O caminho instantâneo e os bloqueios não alocam.
        /// </summary>
        public EffectApplicationResult TryApplyEffect(in EffectSpec spec, out ActiveEffectInstance instance)
        {
            return ApplyEffectCore(in spec, out instance);
        }

        /// <summary>
        /// Como <see cref="ApplyEffect(GameplayEffect, AbilitySystemComponent)"/>, com o desfecho exposto. A
        /// <paramref name="instance"/> vem preenchida só em <see cref="EffectApplicationResult.Applied"/>,
        /// <see cref="EffectApplicationResult.Stacked"/> e <see cref="EffectApplicationResult.Refreshed"/>. O resultado e o
        /// <c>out</c> não alocam; o caminho instantâneo e os bloqueios não alocam, mas <see cref="EffectApplicationResult.Applied"/>
        /// cria a instância nova, como <see cref="ApplyEffect(GameplayEffect, AbilitySystemComponent)"/>.
        /// </summary>
        public EffectApplicationResult TryApplyEffect(GameplayEffect effect, AbilitySystemComponent source, out ActiveEffectInstance instance)
        {
            return ApplyEffectCore(EffectSpec.Create(effect, source), out instance);
        }

        /// <summary>Como <see cref="TryApplyEffect(GameplayEffect, AbilitySystemComponent, out ActiveEffectInstance)"/>, redefinindo a duração.</summary>
        public EffectApplicationResult TryApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source, out ActiveEffectInstance instance)
        {
            return ApplyEffectCore(EffectSpec.Create(effect, source).WithDurationOverride(durationOverride), out instance);
        }

        /// <summary>
        /// Aplicação única de <see cref="ApplyEffect(GameplayEffect, AbilitySystemComponent)"/> e <c>TryApplyEffect</c>. Com
        /// instância ativa, quem passa duração redefine a duração aplicada e quem não passa mantém a da instância.
        /// </summary>
        private EffectApplicationResult ApplyEffectCore(in EffectSpec spec, out ActiveEffectInstance instance)
        {
            instance = null;
            GameplayEffect effect = spec.Effect;
            if (effect == null) return EffectApplicationResult.Invalid;
            EffectContext context = spec.Context.ForApplication(this);
            AbilitySystemComponent source = context.Instigator;
            bool hasOverride = spec.HasDurationOverride;
            float durationOverride = spec.DurationOverride;

            if (!HasAllTagsInList(effect.ApplicationRequirements)) return Block(effect, source, EffectApplicationResult.MissingRequirements);
            if (HasAnyTagInList(effect.ApplicationImmunityTags)) return Block(effect, source, EffectApplicationResult.BlockedByImmunity);

            if (effect.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(effect, in context);
                return EffectApplicationResult.AppliedInstant;
            }

            ActiveEffectInstance existing = FindActiveInstance(effect);
            if (existing == null)
            {
                float duration = hasOverride ? durationOverride : effect.Duration.Evaluate(context.Level, context.Table);
                instance = ApplyDurationEffect(effect, duration, in context);
                return EffectApplicationResult.Applied;
            }

            instance = existing;
            if (effect.CanStack)
            {
                if (hasOverride && effect.RefreshDurationOnStack) existing.Renew(durationOverride);
                int stacksBefore = existing.CurrentStacks;
                existing.AddStack();
                if (existing.CurrentStacks != stacksBefore)
                {
                    existing.RefreshMagnitudes();
                    OnEffectStacksChanged?.Invoke(existing, stacksBefore, existing.CurrentStacks);
                }
                return EffectApplicationResult.Stacked;
            }

            // Sem CanStack: renova a instância existente, sem reaplicar modificadores, tags nem OnApplication.
            existing.Renew(hasOverride ? durationOverride : existing.AppliedDuration);
            return EffectApplicationResult.Refreshed;
        }

        // Como HasAllTags e HasAnyTag, sem o boxing do enumerador da lista: a aplicação de efeito não aloca.
        private bool HasAllTagsInList(List<GameplayTag> tags)
        {
            if (tags == null) return true;
            for (int i = 0; i < tags.Count; i++) if (!HasTag(tags[i])) return false;
            return true;
        }

        private bool HasAnyTagInList(List<GameplayTag> tags)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++) if (HasTag(tags[i])) return true;
            return false;
        }

        private EffectApplicationResult Block(GameplayEffect effect, AbilitySystemComponent source, EffectApplicationResult reason)
        {
            OnEffectBlocked?.Invoke(effect, source, reason);
            return reason;
        }

        /// <summary>
        /// Efeito bloqueado por requisitos ou imunidade (efeito, origem já resolvida, motivo). Efeito nulo não dispara.
        /// </summary>
        public event Action<GameplayEffect, AbilitySystemComponent, EffectApplicationResult> OnEffectBlocked;

        /// <summary>Bloqueio genérico informado por <see cref="ReportBlocked"/> (motivo, origem).</summary>
        public event Action<GameplayTag, AbilitySystemComponent> OnBlocked;

        /// <summary>
        /// Dispara <see cref="OnBlocked"/> com o <paramref name="reason"/>. Tag inválida é ignorada; origem nula vira este ASC.
        /// </summary>
        public void ReportBlocked(GameplayTag reason, AbilitySystemComponent source = null)
        {
            if (!reason.IsValid) return;
            OnBlocked?.Invoke(reason, source != null ? source : this);
        }

        private ActiveEffectInstance FindActiveInstance(GameplayEffect effect)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Effect == effect) return _activeEffects[i];
            }
            return null;
        }

        /// <summary>
        /// Preenche <paramref name="results"/> (limpa antes) com as instâncias que concedem uma tag igual ou descendente de
        /// <paramref name="tag"/>, na ordem de aplicação, e devolve a quantidade. Tag inválida não casa. Não aloca.
        /// </summary>
        public int FindEffectsWithTag(GameplayTag tag, List<ActiveEffectInstance> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (GrantsTag(_activeEffects[i].Effect, tag)) results.Add(_activeEffects[i]);
            }
            return results.Count;
        }

        /// <summary>
        /// Como <see cref="FindEffectsWithTag"/>, para instâncias que casam com alguma das <paramref name="tags"/>
        /// (conjunto, lista ou array; <c>null</c> conta como vazio).
        /// </summary>
        public int FindEffectsWithAnyTag(IReadOnlyList<GameplayTag> tags, List<ActiveEffectInstance> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (tags == null) return 0;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                for (int t = 0; t < tags.Count; t++)
                {
                    if (!GrantsTag(_activeEffects[i].Effect, tags[t])) continue;
                    results.Add(_activeEffects[i]);
                    break;
                }
            }
            return results.Count;
        }

        /// <summary>
        /// Como <see cref="FindEffectsWithTag"/>, para instâncias cujas tags concedidas satisfazem a <paramref name="query"/>.
        /// Consulta <c>null</c> não casa com nada.
        /// </summary>
        public int FindEffectsMatching(GameplayTagQuery query, List<ActiveEffectInstance> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (query == null) return 0;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (query.Matches(_activeEffects[i].Effect.GrantedTags)) results.Add(_activeEffects[i]);
            }
            return results.Count;
        }

        /// <summary>Como <see cref="FindEffectsWithTag"/>, para as instâncias do asset <paramref name="effect"/>.</summary>
        public int FindEffects(GameplayEffect effect, List<ActiveEffectInstance> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (effect == null) return 0;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Effect == effect) results.Add(_activeEffects[i]);
            }
            return results.Count;
        }

        /// <summary>Alguma instância ativa concede uma tag igual ou descendente de <paramref name="tag"/>. Tag inválida: falso.</summary>
        public bool HasEffectWithTag(GameplayTag tag)
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                if (GrantsTag(_activeEffects[i].Effect, tag)) return true;
            }
            return false;
        }

        /// <summary>O efeito concede uma tag <c>g</c> com <c>g.Matches(tag)</c>, a mesma direção de <see cref="HasTag"/>.</summary>
        private static bool GrantsTag(GameplayEffect effect, GameplayTag tag)
        {
            List<GameplayTag> granted = effect.GrantedTags;
            if (granted == null || !tag.IsValid) return false;
            for (int g = 0; g < granted.Count; g++)
            {
                if (granted[g].Matches(tag)) return true;
            }
            return false;
        }

        public float GetCooldownTimeRemaining(GameplayEffect cooldownEffect)
        {
            FindCooldownInstance(cooldownEffect, out float remaining);
            return remaining;
        }

        /// <summary>Tempo restante dividido pela duração aplicada da instância que define esse tempo; 0 sem recarga ou com duração aplicada não positiva.</summary>
        public float GetCooldownNormalized(GameplayEffect cooldownEffect)
        {
            ActiveEffectInstance instance = FindCooldownInstance(cooldownEffect, out float remaining);
            if (instance == null || instance.AppliedDuration <= 0f) return 0f;
            return Mathf.Clamp01(remaining / instance.AppliedDuration);
        }

        /// <summary>
        /// Instância de maior tempo restante entre a do próprio <paramref name="cooldownEffect"/> e as que concedem uma tag igual
        /// ou descendente de alguma tag dele (mesma direção de <see cref="HasTag"/>). Empate fica com a primeira; sem casamento, null e 0.
        /// </summary>
        private ActiveEffectInstance FindCooldownInstance(GameplayEffect cooldownEffect, out float remaining)
        {
            remaining = 0f;
            if (cooldownEffect == null) return null;

            ActiveEffectInstance longest = null;
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                ActiveEffectInstance instance = _activeEffects[i];
                if (!IsCooldownMatch(instance.Effect, cooldownEffect)) continue;
                if (longest != null && instance.TimeRemaining <= remaining) continue;

                longest = instance;
                remaining = instance.TimeRemaining;
            }
            if (remaining < 0f) remaining = 0f;
            return longest;
        }

        private static bool IsCooldownMatch(GameplayEffect effect, GameplayEffect cooldownEffect)
        {
            if (effect == cooldownEffect) return true;

            List<GameplayTag> granted = effect.GrantedTags;
            List<GameplayTag> cooldownTags = cooldownEffect.GrantedTags;
            if (granted == null || cooldownTags == null) return false;

            for (int g = 0; g < granted.Count; g++)
            {
                for (int c = 0; c < cooldownTags.Count; c++)
                {
                    if (granted[g].Matches(cooldownTags[c])) return true;
                }
            }
            return false;
        }

        private void ApplyInstantEffect(GameplayEffect effect, in EffectContext context)
        {
            // Trigger Application Actions
            if (effect.OnApplication != null)
            {
                foreach (var action in effect.OnApplication) action?.Execute(context.Instigator, this);
            }

            ApplyModifiersToBase(effect, effect.Modifiers, in context);
        }

        private ActiveEffectInstance ApplyDurationEffect(GameplayEffect effect, float duration, in EffectContext context)
        {
            var instance = new ActiveEffectInstance(effect, duration, in context);
            _activeEffects.Add(instance);

            AddTags(effect.GrantedTags);

            if (effect.Modifiers != null)
            {
                foreach (var mod in effect.Modifiers)
                {
                    if (!IsModifierValid(effect, mod)) continue;
                    if (!attributeSet.TryGetState(mod.Attribute, out var state)) continue;
                    // O valor avaliado é o de uma pilha; a instância grava o valor multiplicado pelas pilhas (ADR 0029, item 2)
                    float unitValue = mod.ValueSource.Evaluate(in context);
                    var modifier = new AttributeModifier(unitValue, mod.Operation, instance);
                    int trackedIndex = instance.TrackModifier(modifier, unitValue, state);
                    state.AddModifier(modifier);
                    if (mod.Mode == MagnitudeMode.Live) instance.BindLiveModifier(trackedIndex, mod.ValueSource);
                }
            }

            if (effect.Period > 0)
            {
                instance.OnTick += HandleEffectTick;
            }

            if (effect.DurationPolicy == EffectDurationPolicy.HasDuration)
            {
                instance.OnEffectExpired += HandleEffectExpired;
            }

            OnEffectApplied?.Invoke(instance);
            return instance;
        }

        private void HandleEffectTick(ActiveEffectInstance instance)
        {
            // Periodic modifiers act as instant changes to the base value
            EffectContext context = instance.Context;
            ApplyModifiersToBase(instance.Effect, instance.Effect.PeriodicModifiers, in context, instance);
        }

        /// <summary>
        /// Aplica os modificadores na base (efeitos Instant e tiques periódicos): os que miram o mesmo atributo passam
        /// pela fórmula única uma vez, partindo da base atual, e o resultado vira a nova base (ADR 0029, item 4). No tique,
        /// <paramref name="tickInstance"/> avalia cada modificador e guarda o último valor lido do instigador.
        /// </summary>
        private void ApplyModifiersToBase(
            GameplayEffect effect,
            List<AttributeEffectModifier> modifiers,
            in EffectContext context,
            ActiveEffectInstance tickInstance = null)
        {
            if (modifiers == null || attributeSet == null) return;

            // Laços por índice, sem alocação: cada atributo é tratado no primeiro índice que o mira
            for (int i = 0; i < modifiers.Count; i++)
            {
                AttributeEffectModifier first = modifiers[i];
                if (!IsModifierValid(effect, first)) continue;
                if (IsAttributeHandledBefore(modifiers, i)) continue;
                if (!attributeSet.TryGetState(first.Attribute, out var state)) continue;

                var accumulator = new ModifierAccumulator(state.BaseValue);
                for (int j = i; j < modifiers.Count; j++)
                {
                    AttributeEffectModifier mod = modifiers[j];
                    if (mod == null || !ReferenceEquals(mod.Attribute, first.Attribute)) continue;
                    float value = tickInstance != null
                        ? tickInstance.EvaluateTickModifier(j, mod.ValueSource) * tickInstance.StackFactor
                        : mod.ValueSource.Evaluate(in context);
                    accumulator.Accumulate(mod.Operation, value);
                }
                float newBase = accumulator.Result();
                ProcessChange(state, newBase - state.BaseValue, newBase, effect, in context, null, _changeDepth);
            }
        }

        private const int MaxChangeDepth = 8;

        private AttributeChangeQueue _changeQueue;
        // Atributos com processadores em execução, do mais externo ao atual; cada pipeline aninhado começa um trecho novo
        private readonly AttributeDefinition[] _changeChain = new AttributeDefinition[MaxChangeDepth];
        private int _changeDepth;
        private HashSet<AttributeDefinition> _warnedChangeGuard;
        private bool _hasActiveChange;
        private GameplayEffect _activeChangeEffect;
        private EffectContext _activeChangeContext;

        /// <summary>
        /// Uma alteração da base passou pelo pipeline e foi aplicada (ADR 0033): uma vez por alteração, também a
        /// redirecionada e a anulada, depois do <c>OnValueChanged</c>. Modificadores persistentes não disparam.
        /// </summary>
        public event AttributeChangeHandler OnAttributeChangeApplied;

        /// <summary>
        /// Muda a base de <paramref name="attribute"/> em <paramref name="delta"/> pelo pipeline de processadores, como um
        /// efeito instantâneo faria. Devolve quanto a base mudou de fato; 0 se o ASC não tem o atributo.
        /// </summary>
        public float ApplyAttributeChange(AttributeDefinition attribute, float delta, in EffectContext context = default, GameplayEffect effect = null)
        {
            if (attribute == null || attributeSet == null || !attributeSet.TryGetState(attribute, out var state)) return 0f;
            return ProcessChange(state, delta, state.BaseValue + delta, effect, in context, null, _changeDepth);
        }

        /// <summary>
        /// Efeito e contexto da alteração do pipeline em curso, para quem ouve a mudança de valor. Num pipeline aninhado,
        /// vale o nível mais interno. Fora de uma alteração, devolve falso, com efeito nulo e contexto vazio.
        /// </summary>
        public bool TryGetActiveChangeSource(out GameplayEffect effect, out EffectContext context)
        {
            effect = _activeChangeEffect;
            context = _activeChangeContext;
            return _hasActiveChange;
        }

        /// <summary>
        /// Núcleo do pipeline: roda os processadores da definição na ordem da lista, aplica as alterações redirecionadas,
        /// depois a original, e dispara o relatório de cada uma. <paramref name="requestedBase"/> é a base pedida, usada
        /// sem conta nova quando nada mudou a variação. <paramref name="chainStart"/> é o início do trecho da cadeia deste
        /// pipeline: atributo repetido no trecho ou cadeia com <see cref="MaxChangeDepth"/> atributos recebe direto.
        /// </summary>
        private float ProcessChange(
            AttributeState state,
            float delta,
            float requestedBase,
            GameplayEffect effect,
            in EffectContext context,
            AttributeDefinition redirectedFrom,
            int chainStart)
        {
            AttributeDefinition definition = state.Definition;
            var change = new AttributeChange(definition, delta, effect, redirectedFrom);
            IReadOnlyList<AttributeChangeProcessor> processors = definition.Processors;
            bool processed = processors != null && processors.Count > 0;
            bool guarded = processed && (_changeDepth >= MaxChangeDepth || IsInChangeChain(definition, chainStart));
            if (guarded)
            {
                WarnChangeGuard(definition);
                processed = false;
            }

            bool hadActive = _hasActiveChange;
            GameplayEffect previousEffect = _activeChangeEffect;
            EffectContext previousContext = _activeChangeContext;
            _hasActiveChange = true;
            _activeChangeEffect = effect;
            _activeChangeContext = context;
            if (processed) _changeChain[_changeDepth++] = definition;
            try
            {
                float startBase = state.BaseValue;
                if (processed) RunProcessors(processors, ref change, effect, in context, chainStart);

                // Sem processadores (ou com a guarda), a base pedida vai direto, como antes do pipeline (CA1); com eles,
                // final zero não aplica, e a base pedida só vale se as redirecionadas não mudaram este atributo. O meta
                // descarta a variação final: a base fica e nenhum evento de valor dispara (ADR 0033, item 6)
                float before = state.BaseValue;
                if (definition.IsMeta)
                {
                    // Nada a aplicar: o relatório sai com Final e Applied = 0
                }
                else if (!processed) state.SetBaseValue(requestedBase);
                else if (change.Delta != 0f)
                {
                    bool unchanged = change.Delta == change.Requested && before == startBase;
                    state.SetBaseValue(unchanged ? requestedBase : before + change.Delta);
                }

                var report = new AttributeChangeReport(in change, state.BaseValue - before, in context);
                OnAttributeChangeApplied?.Invoke(in report);
                return report.Applied;
            }
            finally
            {
                if (processed) _changeChain[--_changeDepth] = null;
                _hasActiveChange = hadActive;
                _activeChangeEffect = previousEffect;
                _activeChangeContext = previousContext;
            }
        }

        private void RunProcessors(
            IReadOnlyList<AttributeChangeProcessor> processors,
            ref AttributeChange change,
            GameplayEffect effect,
            in EffectContext context,
            int chainStart)
        {
            AttributeChangeQueue queue = _changeQueue ??= new AttributeChangeQueue(this);
            int queueStart = queue.Count;
            try
            {
                for (int i = 0; i < processors.Count; i++) processors[i]?.Process(ref change, in context, queue);

                // As redirecionadas vêm antes da original (ADR 0033, item 5); as que elas geram ficam depois de queueEnd
                int queueEnd = queue.Count;
                for (int i = queueStart; i < queueEnd; i++)
                {
                    AttributeChangeQueue.Entry entry = queue[i];
                    if (!attributeSet.TryGetState(entry.Target, out var target)) continue;
                    ProcessChange(target, entry.Delta, target.BaseValue + entry.Delta, effect, in context, change.Attribute, chainStart);
                }
            }
            finally
            {
                queue.TrimTo(queueStart);
            }
        }

        private bool IsInChangeChain(AttributeDefinition definition, int chainStart)
        {
            for (int i = chainStart; i < _changeDepth; i++)
            {
                if (ReferenceEquals(_changeChain[i], definition)) return true;
            }
            return false;
        }

        private void WarnChangeGuard(AttributeDefinition definition)
        {
            _warnedChangeGuard ??= new HashSet<AttributeDefinition>();
            if (!_warnedChangeGuard.Add(definition) || !Debug.isDebugBuild) return;
            Debug.LogWarning(
                $"[GAS] Alteração de '{definition.name}' em '{name}' aplicada sem os processadores: o atributo já está na " +
                $"cadeia de redirecionamento ou o pipeline passou de {MaxChangeDepth} níveis.", this);
        }

        private static bool IsAttributeHandledBefore(List<AttributeEffectModifier> modifiers, int index)
        {
            AttributeDefinition attribute = modifiers[index].Attribute;
            for (int k = 0; k < index; k++)
            {
                if (modifiers[k] != null && ReferenceEquals(modifiers[k].Attribute, attribute)) return true;
            }
            return false;
        }

        private static bool IsModifierValid(GameplayEffect effect, AttributeEffectModifier mod)
        {
            // Entrada nula na lista é ignorada sem aviso
            if (mod == null) return false;
            if (mod.Attribute != null) return true;
            Debug.LogWarning($"[GAS] Modifier sem atributo ignorado no efeito '{(effect != null ? effect.name : "null")}'.", effect);
            return false;
        }

        /// <summary>
        /// Remove a instância ativa: roda a limpeza e as ações de remoção e devolve tags e modificadores.
        /// Ignora <c>null</c> e instâncias que não estão ativas neste ASC (já removidas, de outro ASC ou nunca aplicadas).
        /// </summary>
        public void RemoveEffect(ActiveEffectInstance instance)
        {
            RemoveEffectCore(instance, EffectRemovalReason.Removed);
        }

        /// <summary>
        /// Remove as instâncias que concedem uma tag igual ou descendente de <paramref name="tag"/> (motivo
        /// <see cref="EffectRemovalReason.RemovedByTag"/>) e devolve a soma das pilhas removidas. Tag inválida ou nada ativo: 0.
        /// </summary>
        public int RemoveEffectsWithTag(GameplayTag tag)
        {
            int removedStacks = 0;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                // Um ouvinte de OnEffectRemoved pode ter removido outras instâncias
                if (i >= _activeEffects.Count) continue;
                ActiveEffectInstance instance = _activeEffects[i];
                if (!GrantsTag(instance.Effect, tag)) continue;

                removedStacks += instance.CurrentStacks;
                RemoveEffectCore(instance, EffectRemovalReason.RemovedByTag);
            }
            return removedStacks;
        }

        /// <summary>
        /// Remove as instâncias do asset <paramref name="effect"/> (motivo <see cref="EffectRemovalReason.Removed"/>) e
        /// devolve a soma das pilhas removidas. <c>null</c> ou nada ativo: 0.
        /// </summary>
        public int RemoveEffects(GameplayEffect effect)
        {
            if (effect == null) return 0;

            int removedStacks = 0;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (i >= _activeEffects.Count) continue;
                ActiveEffectInstance instance = _activeEffects[i];
                if (instance.Effect != effect) continue;

                removedStacks += instance.CurrentStacks;
                RemoveEffectCore(instance, EffectRemovalReason.Removed);
            }
            return removedStacks;
        }

        /// <summary>
        /// Soma <paramref name="seconds"/> ao tempo restante de um efeito com duração ativo neste ASC. Segundos negativos
        /// ou NaN são rejeitados com aviso. Devolve se algo mudou; não muda a <c>AppliedDuration</c>.
        /// </summary>
        public bool ExtendEffectDuration(ActiveEffectInstance instance, float seconds)
        {
            if (!CanMutateDuration(instance, seconds, nameof(ExtendEffectDuration))) return false;
            instance.SetTimeRemaining(instance.TimeRemaining + seconds);
            return true;
        }

        /// <summary>
        /// Subtrai <paramref name="seconds"/> do tempo restante; se chega a zero, o efeito expira na hora (motivo
        /// <see cref="EffectRemovalReason.Expired"/>). Mesma validação de <see cref="ExtendEffectDuration"/>.
        /// </summary>
        public bool ReduceEffectDuration(ActiveEffectInstance instance, float seconds)
        {
            if (!CanMutateDuration(instance, seconds, nameof(ReduceEffectDuration))) return false;
            instance.SetTimeRemaining(instance.TimeRemaining - seconds);
            if (instance.TimeRemaining <= 0f) RemoveEffectCore(instance, EffectRemovalReason.Expired);
            return true;
        }

        /// <summary>Volta o tempo restante à <c>AppliedDuration</c>, sem mexer no período. Só efeitos com duração.</summary>
        public bool RestartEffectDuration(ActiveEffectInstance instance)
        {
            if (!IsActiveHere(instance) || instance.Effect.DurationPolicy != EffectDurationPolicy.HasDuration) return false;
            instance.SetTimeRemaining(instance.AppliedDuration);
            return true;
        }

        /// <summary>
        /// Grava a contagem de pilhas, limitada a <c>[0, MaxStacks]</c> (<c>MaxStacks</c> menor que 1 vale 1), e dispara
        /// <see cref="OnEffectStacksChanged"/>. Zero remove a instância com <see cref="EffectRemovalReason.StacksDepleted"/>,
        /// depois da limpeza e do evento de pilhas. Tags e modificadores seguem um por instância; com
        /// <see cref="GameplayEffect.StacksMultiplyMagnitude"/>, o valor de cada modificador acompanha as pilhas.
        /// </summary>
        public bool SetEffectStacks(ActiveEffectInstance instance, int count)
        {
            if (!IsActiveHere(instance)) return false;

            int max = Math.Max(1, instance.Effect.MaxStacks);
            int clamped = Math.Min(Math.Max(count, 0), max);
            int before = instance.CurrentStacks;
            if (clamped == before) return false;

            instance.SetStackCount(clamped);
            if (clamped == 0)
            {
                // Remoção sem evento, para disparar as pilhas antes do OnEffectRemoved (estado já final nos dois)
                RemoveEffectCore(instance, EffectRemovalReason.StacksDepleted, raiseRemoved: false);
                OnEffectStacksChanged?.Invoke(instance, before, 0);
                OnEffectRemoved?.Invoke(instance, EffectRemovalReason.StacksDepleted);
                RaiseCooldownsEnded();
                return true;
            }

            instance.RefreshMagnitudes();
            OnEffectStacksChanged?.Invoke(instance, before, clamped);
            return true;
        }

        /// <summary>Tira <paramref name="count"/> pilhas, como <see cref="SetEffectStacks"/>. <paramref name="count"/> menor ou igual a 0: <c>false</c>.</summary>
        public bool RemoveEffectStacks(ActiveEffectInstance instance, int count)
        {
            if (count <= 0 || !IsActiveHere(instance)) return false;
            return SetEffectStacks(instance, instance.CurrentStacks - count);
        }

        private bool IsActiveHere(ActiveEffectInstance instance)
        {
            return instance != null && instance.Target == this && instance.IsActive && _activeEffects.Contains(instance);
        }

        private bool CanMutateDuration(ActiveEffectInstance instance, float seconds, string caller)
        {
            if (!IsActiveHere(instance) || instance.Effect.DurationPolicy != EffectDurationPolicy.HasDuration) return false;
            if (seconds >= 0f) return seconds > 0f;

            // NaN e negativos caem aqui
            if (Debug.isDebugBuild) Debug.LogWarning($"[GAS] {caller} ignorado: segundos inválidos ({seconds}) no efeito '{instance.Effect.name}'.", this);
            return false;
        }

        private void HandleEffectExpired(ActiveEffectInstance instance)
        {
            RemoveEffectCore(instance, EffectRemovalReason.Expired);
        }

        /// <summary>Ponto único de remoção: limpeza, devolução de tags e modificadores e, por último, <see cref="OnEffectRemoved"/>.</summary>
        private void RemoveEffectCore(ActiveEffectInstance instance, EffectRemovalReason reason, bool raiseRemoved = true)
        {
            if (instance == null) return;
            // Sai da lista antes da limpeza: uma remoção repetida ou reentrante cai aqui e não refaz nada
            if (!_activeEffects.Remove(instance)) return;

            instance.IsRemoved = true;
            instance.DisposeLiveBindings();
            instance.Cleanup();
            instance.OnEffectExpired -= HandleEffectExpired;
            instance.OnTick -= HandleEffectTick;

            RemoveTags(instance.Effect.GrantedTags);

            if (instance.Effect.Modifiers != null)
            {
                foreach (var mod in instance.Effect.Modifiers)
                {
                    if (mod != null && attributeSet.TryGetState(mod.Attribute, out var state))
                    {
                        state.RemoveModifiersFromSource(instance);
                    }
                }
            }

            if (!raiseRemoved) return;
            OnEffectRemoved?.Invoke(instance, reason);
            RaiseCooldownsEnded();
        }
        #endregion
    }
}

