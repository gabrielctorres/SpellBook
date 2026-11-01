using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Time;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Abilities;
namespace SpellBook.GAS.Core
{
    [RequireComponent(typeof(AttributeSet))]
    [HelpURL(DocUrls.Gas + "#abilitysystemcomponent")]
    public class AbilitySystemComponent : MonoBehaviour
    {
        [SerializeField] private AttributeSet attributeSet;
        [Tooltip("Habilidades concedidas automaticamente no Awake.")]
        [SerializeField] private List<GameplayAbility> startingAbilities = new List<GameplayAbility>();
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

        public event Action<GameplayTag, bool> OnTagChanged;
        public event Action<AbilitySpec> OnAbilityActivated;
        public event Action<AbilitySpec, bool> OnAbilityEnded; // spec, cancelled
        public event Action<GameplayAbility, AbilityActivationResult> OnAbilityFailed;

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
        /// Concede uma habilidade ao ASC. Se já concedida, retorna o spec existente.
        /// </summary>
        public AbilitySpec GiveAbility(GameplayAbility ability, int level = 1)
        {
            if (ability == null) return null;

            var existing = FindSpec(ability);
            if (existing != null) return existing;

            var spec = new AbilitySpec(ability, level);
            _abilities.Add(spec);
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
            _abilities.Remove(spec);
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

        #region Ability Activation
        public AbilityActivationResult GetActivationResult(GameplayAbility ability)
        {
            if (ability == null) return AbilityActivationResult.InvalidAbility;

            var spec = FindSpec(ability);
            if (spec == null) return AbilityActivationResult.NotGranted;
            if (spec.IsActive) return AbilityActivationResult.AlreadyActive;

            if (HasAnyTag(ability.BlockedTags)) return AbilityActivationResult.BlockedByTags;
            if (!HasAllTags(ability.RequiredTags)) return AbilityActivationResult.MissingRequiredTags;
            if (IsBlockedByActiveAbility(ability)) return AbilityActivationResult.BlockedByActiveAbility;
            if (IsOnCooldown(ability.CooldownEffect)) return AbilityActivationResult.OnCooldown;
            if (!CanAffordCost(ability.CostEffect)) return AbilityActivationResult.CannotAffordCost;

            return AbilityActivationResult.Success;
        }

        public AbilityActivationResult TryActivateAbility(GameplayAbility ability, Vector3 targetPos)
        {
            var result = GetActivationResult(ability);
            if (result != AbilityActivationResult.Success)
            {
                OnAbilityFailed?.Invoke(ability, result);
                return result;
            }

            ActivateSpec(FindSpec(ability), targetPos);
            return result;
        }

        public AbilityActivationResult TryActivateAbility(AbilitySpec spec, Vector3 targetPos)
        {
            if (spec == null || !_abilities.Contains(spec))
            {
                var result = spec == null ? AbilityActivationResult.InvalidAbility : AbilityActivationResult.NotGranted;
                OnAbilityFailed?.Invoke(spec?.Ability, result);
                return result;
            }
            return TryActivateAbility(spec.Ability, targetPos);
        }

        public bool IsOnCooldown(GameplayEffect cooldownEffect)
        {
            if (cooldownEffect == null) return false;
            if (HasAnyTag(cooldownEffect.GrantedTags)) return true;
            return GetCooldownTimeRemaining(cooldownEffect) > 0f;
        }

        /// <summary>
        /// Verifica se os modifiers Add negativos do custo deixariam algum atributo abaixo do MinValue.
        /// </summary>
        public bool CanAffordCost(GameplayEffect costEffect)
        {
            if (costEffect == null || costEffect.Modifiers == null) return true;

            foreach (var mod in costEffect.Modifiers)
            {
                if (mod.Attribute == null || mod.Operation != ModifierOperation.Add) continue;

                float value = mod.ValueSource.GetValue(this, this);
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
            if (ability.CooldownEffect != null) ApplyEffect(ability.CooldownEffect, ability.GetCooldownDuration(this));
            if (ability.CostEffect != null) ApplyEffect(ability.CostEffect);

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
        #endregion

        #region Gameplay Effects
        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, AbilitySystemComponent source = null)
        {
            ApplyEffectCore(effect, false, 0f, source, out ActiveEffectInstance instance);
            return instance;
        }

        public ActiveEffectInstance ApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source = null)
        {
            ApplyEffectCore(effect, true, durationOverride, source, out ActiveEffectInstance instance);
            return instance;
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
            return ApplyEffectCore(effect, false, 0f, source, out instance);
        }

        /// <summary>Como <see cref="TryApplyEffect(GameplayEffect, AbilitySystemComponent, out ActiveEffectInstance)"/>, redefinindo a duração.</summary>
        public EffectApplicationResult TryApplyEffect(GameplayEffect effect, float durationOverride, AbilitySystemComponent source, out ActiveEffectInstance instance)
        {
            return ApplyEffectCore(effect, true, durationOverride, source, out instance);
        }

        /// <summary>
        /// Aplicação única de <see cref="ApplyEffect(GameplayEffect, AbilitySystemComponent)"/> e <c>TryApplyEffect</c>. Com
        /// instância ativa, quem passa duração redefine a duração aplicada e quem não passa mantém a da instância.
        /// </summary>
        private EffectApplicationResult ApplyEffectCore(GameplayEffect effect, bool hasOverride, float durationOverride, AbilitySystemComponent source, out ActiveEffectInstance instance)
        {
            instance = null;
            if (effect == null) return EffectApplicationResult.Invalid;
            if (source == null) source = this;

            if (!HasAllTagsInList(effect.ApplicationRequirements)) return Block(effect, source, EffectApplicationResult.MissingRequirements);
            if (HasAnyTagInList(effect.ApplicationImmunityTags)) return Block(effect, source, EffectApplicationResult.BlockedByImmunity);

            if (effect.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(effect, source);
                return EffectApplicationResult.AppliedInstant;
            }

            ActiveEffectInstance existing = FindActiveInstance(effect);
            if (existing == null)
            {
                instance = ApplyDurationEffect(effect, hasOverride ? durationOverride : effect.Duration, source);
                return EffectApplicationResult.Applied;
            }

            instance = existing;
            if (effect.CanStack)
            {
                if (hasOverride && effect.RefreshDurationOnStack) existing.Renew(durationOverride);
                int stacksBefore = existing.CurrentStacks;
                existing.AddStack();
                if (existing.CurrentStacks != stacksBefore) OnEffectStacksChanged?.Invoke(existing, stacksBefore, existing.CurrentStacks);
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

        private void ApplyInstantEffect(GameplayEffect effect, AbilitySystemComponent source)
        {
            // Trigger Application Actions
            if (effect.OnApplication != null)
            {
                foreach (var action in effect.OnApplication) action?.Execute(source, this);
            }

            ApplyModifiersToBase(effect, effect.Modifiers, source);
        }

        private ActiveEffectInstance ApplyDurationEffect(GameplayEffect effect, float duration, AbilitySystemComponent source)
        {
            var instance = new ActiveEffectInstance(effect, duration, source, this);
            _activeEffects.Add(instance);

            AddTags(effect.GrantedTags);

            if (effect.Modifiers != null)
            {
                foreach (var mod in effect.Modifiers)
                {
                    if (!IsModifierValid(effect, mod)) continue;
                    float value = mod.ValueSource.GetValue(source, this);
                    attributeSet.AddModifier(mod.Attribute, new AttributeModifier(value, mod.Operation, instance));
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
            ApplyModifiersToBase(instance.Effect, instance.Effect.PeriodicModifiers, instance.Source);
        }

        /// <summary>
        /// Aplica modifiers diretamente no valor base (efeitos Instant e ticks periódicos).
        /// Add: base + v | Multiply: base * (1 + v) | Override: v
        /// </summary>
        private void ApplyModifiersToBase(GameplayEffect effect, List<AttributeEffectModifier> modifiers, AbilitySystemComponent source)
        {
            if (modifiers == null || attributeSet == null) return;

            foreach (var mod in modifiers)
            {
                if (!IsModifierValid(effect, mod)) continue;
                if (!attributeSet.TryGetState(mod.Attribute, out var state)) continue;

                float value = mod.ValueSource.GetValue(source, this);
                switch (mod.Operation)
                {
                    case ModifierOperation.Add:
                        state.SetBaseValue(state.BaseValue + value);
                        break;
                    case ModifierOperation.Multiply:
                        state.SetBaseValue(state.BaseValue * (1f + value));
                        break;
                    case ModifierOperation.Override:
                        state.SetBaseValue(value);
                        break;
                }
            }
        }

        private static bool IsModifierValid(GameplayEffect effect, AttributeEffectModifier mod)
        {
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

        private void HandleEffectExpired(ActiveEffectInstance instance)
        {
            RemoveEffectCore(instance, EffectRemovalReason.Expired);
        }

        /// <summary>Ponto único de remoção: limpeza, devolução de tags e modificadores e, por último, <see cref="OnEffectRemoved"/>.</summary>
        private void RemoveEffectCore(ActiveEffectInstance instance, EffectRemovalReason reason)
        {
            if (instance == null) return;
            // Sai da lista antes da limpeza: uma remoção repetida ou reentrante cai aqui e não refaz nada
            if (!_activeEffects.Remove(instance)) return;

            instance.IsRemoved = true;
            instance.Cleanup();
            instance.OnEffectExpired -= HandleEffectExpired;
            instance.OnTick -= HandleEffectTick;

            RemoveTags(instance.Effect.GrantedTags);

            if (instance.Effect.Modifiers != null)
            {
                foreach (var mod in instance.Effect.Modifiers)
                {
                    if (attributeSet.TryGetState(mod.Attribute, out var state))
                    {
                        state.RemoveModifiersFromSource(instance);
                    }
                }
            }

            OnEffectRemoved?.Invoke(instance, reason);
        }
        #endregion
    }
}

