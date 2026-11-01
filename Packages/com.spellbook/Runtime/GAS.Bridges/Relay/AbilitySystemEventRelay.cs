using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Repassa os eventos C# de um ASC para canais em asset e <c>UnityEvent</c>s locais, no mesmo quadro e sem estado de
    /// gameplay (ADR 0031). Campo vazio usa o ASC do mesmo objeto.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [HelpURL(DocUrls.Events + "#relay-do-gas")]
    public sealed class AbilitySystemEventRelay : MonoBehaviour, IValidatable
    {
        [SerializeField, Tooltip("ASC observado. Vazio: o do mesmo objeto.")]
        private AbilitySystemComponent abilitySystem;

        [Header("Efeitos")]
        [SerializeField] private RelayPair<EffectEventData> effectApplied = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectRemoved = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectStacksChanged = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectBlocked = new RelayPair<EffectEventData>();

        [Header("Habilidades")]
        [SerializeField] private RelayPair<AbilityEventData> abilityActivated = new RelayPair<AbilityEventData>();
        [SerializeField] private RelayPair<AbilityEventData> abilityEnded = new RelayPair<AbilityEventData>();
        [SerializeField] private RelayPair<AbilityEventData> abilityFailed = new RelayPair<AbilityEventData>();
        [SerializeField] private RelayPair<AbilityEventData> abilityGranted = new RelayPair<AbilityEventData>();
        [SerializeField] private RelayPair<AbilityEventData> abilityRemoved = new RelayPair<AbilityEventData>();

        [Header("Atributos")]
        [SerializeField] private RelayPair<AttributeChangedData> attributeChanged = new RelayPair<AttributeChangedData>();
        [SerializeField] private RelayPair<AttributeChangedData> attributeDecreased = new RelayPair<AttributeChangedData>();
        [SerializeField, Tooltip("Só este atributo dispara Attribute Decreased. Vazio: qualquer atributo.")]
        private AttributeDefinition decreasedAttribute;

        [Header("Tags")]
        [SerializeField] private RelayPair<TagChangedData> tagAdded = new RelayPair<TagChangedData>();
        [SerializeField] private RelayPair<TagChangedData> tagRemoved = new RelayPair<TagChangedData>();

        [Header("Bloqueio")]
        [SerializeField] private RelayPair<BlockedEventData> blocked = new RelayPair<BlockedEventData>();

        private static readonly EffectValues EffectSelector = new EffectValues();
        private static readonly AbilityValues AbilitySelector = new AbilityValues();
        private static readonly AttributeValues AttributeSelector = new AttributeValues(false);
        private static readonly AttributeValues DecreasedSelector = new AttributeValues(true);
        private static readonly TagValues TagSelector = new TagValues();
        private static readonly BlockedValues BlockedSelector = new BlockedValues();

        private AbilitySystemComponent _subscribed;
        private bool _warnedMissingAsc;
        private Action<ActiveEffectInstance> _onEffectApplied;
        private Action<ActiveEffectInstance, EffectRemovalReason> _onEffectRemoved;
        private Action<ActiveEffectInstance, int, int> _onEffectStacksChanged;
        private Action<GameplayEffect, AbilitySystemComponent, EffectApplicationResult> _onEffectBlocked;
        private Action<GameplayTag, AbilitySystemComponent> _onBlocked;
        private Action<AbilitySpec> _onAbilityActivated;
        private Action<AbilitySpec, bool> _onAbilityEnded;
        private Action<GameplayAbility, ActivationFailure> _onAbilityFailed;
        private Action<AbilitySpec> _onAbilityGranted;
        private Action<AbilitySpec> _onAbilityRemoved;
        private Action<GameplayTag, bool> _onTagChanged;
        private List<AttributeSubscription> _attributeSubscriptions;

#if UNITY_EDITOR
        /// <summary>Um disparo recente: o par que disparou e o quadro. Só no Editor, para o Inspector.</summary>
        public readonly struct RelayFiring
        {
            public readonly string Pair;
            public readonly int Frame;

            public RelayFiring(string pair, int frame)
            {
                Pair = pair;
                Frame = frame;
            }
        }

        private const int RecentCapacity = 10;
        private readonly RelayFiring[] _recent = new RelayFiring[RecentCapacity];
        private int _recentStart;
        private int _recentCount;

        /// <summary>Quantos disparos recentes estão guardados (até 10).</summary>
        public int RecentFiringCount => _recentCount;

        /// <summary>Disparo recente, do mais antigo (0) ao mais novo.</summary>
        public RelayFiring GetRecentFiring(int index)
        {
            if (index < 0 || index >= _recentCount) throw new ArgumentOutOfRangeException(nameof(index));
            return _recent[(_recentStart + index) % RecentCapacity];
        }

        // Buffer circular: cheio, o novo ocupa o lugar do mais antigo.
        private void RecordFiring(string pair)
        {
            var firing = new RelayFiring(pair, UnityEngine.Time.frameCount);
            if (_recentCount < RecentCapacity)
            {
                _recent[(_recentStart + _recentCount) % RecentCapacity] = firing;
                _recentCount++;
                return;
            }

            _recent[_recentStart] = firing;
            _recentStart = (_recentStart + 1) % RecentCapacity;
        }
#endif

        // Todo par dispara por aqui, para o Editor guardar os últimos disparos.
        private void Raise<T>(RelayPair<T> pair, string name, in T data, RelayValueSelector<T> values)
        {
#if UNITY_EDITOR
            RecordFiring(name);
#endif
            pair.Raise(in data, this, values);
        }

        private void Awake()
        {
            _onEffectApplied = HandleEffectApplied;
            _onEffectRemoved = HandleEffectRemoved;
            _onEffectStacksChanged = HandleEffectStacksChanged;
            _onEffectBlocked = HandleEffectBlocked;
            _onBlocked = HandleBlocked;
            _onAbilityActivated = HandleAbilityActivated;
            _onAbilityEnded = HandleAbilityEnded;
            _onAbilityFailed = HandleAbilityFailed;
            _onAbilityGranted = HandleAbilityGranted;
            _onAbilityRemoved = HandleAbilityRemoved;
            _onTagChanged = HandleTagChanged;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void OnValidate()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _onEffectApplied == null) return;
            if (ReferenceEquals(ResolveAsc(), _subscribed)) return;
            Unsubscribe();
            Subscribe();
        }

        /// <summary>
        /// Acusa relay sem ASC no campo nem no objeto, canal de valor de tipo fora da lista e <c>UnityEvent</c> com
        /// chamada sem alvo ou sem método (R19).
        /// </summary>
        public void Validate(ValidationResult result)
        {
            if (ResolveAsc() == null)
            {
                result.AddError("Relay sem ASC: escolha o Ability System ou adicione um AbilitySystemComponent ao objeto.",
                    nameof(abilitySystem));
            }

            effectApplied.Validate(result, nameof(effectApplied));
            effectRemoved.Validate(result, nameof(effectRemoved));
            effectStacksChanged.Validate(result, nameof(effectStacksChanged));
            effectBlocked.Validate(result, nameof(effectBlocked));
            abilityActivated.Validate(result, nameof(abilityActivated));
            abilityEnded.Validate(result, nameof(abilityEnded));
            abilityFailed.Validate(result, nameof(abilityFailed));
            abilityGranted.Validate(result, nameof(abilityGranted));
            abilityRemoved.Validate(result, nameof(abilityRemoved));
            attributeChanged.Validate(result, nameof(attributeChanged));
            attributeDecreased.Validate(result, nameof(attributeDecreased));
            tagAdded.Validate(result, nameof(tagAdded));
            tagRemoved.Validate(result, nameof(tagRemoved));
            blocked.Validate(result, nameof(blocked));
        }

        private AbilitySystemComponent ResolveAsc() => abilitySystem != null ? abilitySystem : GetComponent<AbilitySystemComponent>();

        private void Subscribe()
        {
            AbilitySystemComponent asc = ResolveAsc();
            if (asc == null)
            {
                if (!_warnedMissingAsc) Debug.LogWarning($"{name}: AbilitySystemEventRelay sem ASC no campo nem no objeto; nada é repassado.", this);
                _warnedMissingAsc = true;
                return;
            }

            _subscribed = asc;
            asc.OnEffectApplied += _onEffectApplied;
            asc.OnEffectRemoved += _onEffectRemoved;
            asc.OnEffectStacksChanged += _onEffectStacksChanged;
            asc.OnEffectBlocked += _onEffectBlocked;
            asc.OnBlocked += _onBlocked;
            asc.OnAbilityActivated += _onAbilityActivated;
            asc.OnAbilityEnded += _onAbilityEnded;
            asc.OnAbilityFailed += _onAbilityFailed;
            asc.OnAbilityGranted += _onAbilityGranted;
            asc.OnAbilityRemoved += _onAbilityRemoved;
            asc.OnTagChanged += _onTagChanged;
            SubscribeAttributes(asc.AttributeSet);
        }

        // Uma lista e um handler por atributo a cada ativação; o estado já entrega o valor antigo e o novo.
        private void SubscribeAttributes(AttributeSet set)
        {
            if (set == null) return;
            IReadOnlyList<AttributeState> states = set.States;
            _attributeSubscriptions = new List<AttributeSubscription>(states.Count);
            for (int i = 0; i < states.Count; i++)
            {
                AttributeDefinition attribute = states[i].Definition;
                Action<float, float> handler = (oldValue, newValue) => HandleAttributeChanged(attribute, oldValue, newValue);
                states[i].OnValueChanged += handler;
                _attributeSubscriptions.Add(new AttributeSubscription(states[i], handler));
            }
        }

        // Idempotente. Compara por referência C#: o ASC destruído ainda tem os eventos e não lança.
        private void Unsubscribe()
        {
            AbilitySystemComponent asc = _subscribed;
            if (ReferenceEquals(asc, null)) return;
            _subscribed = null;
            asc.OnEffectApplied -= _onEffectApplied;
            asc.OnEffectRemoved -= _onEffectRemoved;
            asc.OnEffectStacksChanged -= _onEffectStacksChanged;
            asc.OnEffectBlocked -= _onEffectBlocked;
            asc.OnBlocked -= _onBlocked;
            asc.OnAbilityActivated -= _onAbilityActivated;
            asc.OnAbilityEnded -= _onAbilityEnded;
            asc.OnAbilityFailed -= _onAbilityFailed;
            asc.OnAbilityGranted -= _onAbilityGranted;
            asc.OnAbilityRemoved -= _onAbilityRemoved;
            asc.OnTagChanged -= _onTagChanged;

            if (_attributeSubscriptions == null) return;
            foreach (AttributeSubscription subscription in _attributeSubscriptions)
            {
                subscription.State.OnValueChanged -= subscription.Handler;
            }

            _attributeSubscriptions = null;
        }

        private void HandleEffectApplied(ActiveEffectInstance instance)
        {
            EffectEventData data = EffectData(instance, instance.CurrentStacks, 0);
            data.Result = EffectApplicationResult.Applied;
            Raise(effectApplied, nameof(effectApplied), in data, EffectSelector);
        }

        private void HandleEffectRemoved(ActiveEffectInstance instance, EffectRemovalReason reason)
        {
            EffectEventData data = EffectData(instance, 0, instance.CurrentStacks);
            data.RemovalReason = reason;
            Raise(effectRemoved, nameof(effectRemoved), in data, EffectSelector);
        }

        private void HandleEffectStacksChanged(ActiveEffectInstance instance, int before, int after)
        {
            EffectEventData data = EffectData(instance, after, before);
            Raise(effectStacksChanged, nameof(effectStacksChanged), in data, EffectSelector);
        }

        private void HandleEffectBlocked(GameplayEffect effect, AbilitySystemComponent source, EffectApplicationResult result)
        {
            var data = new EffectEventData { Owner = _subscribed, Source = source, Effect = effect, Result = result };
            Raise(effectBlocked, nameof(effectBlocked), in data, EffectSelector);
        }

        private void HandleBlocked(GameplayTag reason, AbilitySystemComponent source)
        {
            var data = new BlockedEventData { Owner = _subscribed, Reason = reason, Source = source };
            Raise(blocked, nameof(blocked), in data, BlockedSelector);
        }

        private void HandleAbilityActivated(AbilitySpec spec)
        {
            AbilityEventData data = AbilityData(spec.Ability, spec.Level);
            Raise(abilityActivated, nameof(abilityActivated), in data, AbilitySelector);
        }

        private void HandleAbilityEnded(AbilitySpec spec, bool cancelled)
        {
            AbilityEventData data = AbilityData(spec.Ability, spec.Level);
            data.Cancelled = cancelled;
            Raise(abilityEnded, nameof(abilityEnded), in data, AbilitySelector);
        }

        // A falha não traz o spec: o nível é o da concessão, 0 se a habilidade não foi concedida.
        private void HandleAbilityFailed(GameplayAbility ability, ActivationFailure failure)
        {
            AbilityEventData data = AbilityData(ability, _subscribed.GetAbilityLevel(ability));
            data.FailureReason = failure.Result;
            data.FailureTag = failure.ReasonTag;
            data.FailureKey = failure.ReasonKey;
            Raise(abilityFailed, nameof(abilityFailed), in data, AbilitySelector);
        }

        private void HandleAbilityGranted(AbilitySpec spec)
        {
            AbilityEventData data = AbilityData(spec.Ability, spec.Level);
            Raise(abilityGranted, nameof(abilityGranted), in data, AbilitySelector);
        }

        private void HandleAbilityRemoved(AbilitySpec spec)
        {
            AbilityEventData data = AbilityData(spec.Ability, spec.Level);
            Raise(abilityRemoved, nameof(abilityRemoved), in data, AbilitySelector);
        }

        private void HandleAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
        {
            var data = new AttributeChangedData(_subscribed, attribute, oldValue, newValue);
            Raise(attributeChanged, nameof(attributeChanged), in data, AttributeSelector);
            if (newValue >= oldValue) return;
            if (decreasedAttribute != null && decreasedAttribute != attribute) return;
            Raise(attributeDecreased, nameof(attributeDecreased), in data, DecreasedSelector);
        }

        // Contagem lida depois da mudança: 1 ao entrar e 0 ao sair.
        private void HandleTagChanged(GameplayTag tag, bool added)
        {
            var data = new TagChangedData { Owner = _subscribed, Tag = tag, Added = added, Count = _subscribed.GetTagCount(tag) };
            if (added) Raise(tagAdded, nameof(tagAdded), in data, TagSelector);
            else Raise(tagRemoved, nameof(tagRemoved), in data, TagSelector);
        }

        private EffectEventData EffectData(ActiveEffectInstance instance, int stacks, int previousStacks)
        {
            return new EffectEventData
            {
                Owner = _subscribed,
                Source = instance.Source,
                Effect = instance.Effect,
                Stacks = stacks,
                PreviousStacks = previousStacks,
            };
        }

        private AbilityEventData AbilityData(GameplayAbility ability, int level)
        {
            return new AbilityEventData { Owner = _subscribed, Ability = ability, Level = level };
        }

        private readonly struct AttributeSubscription
        {
            public readonly AttributeState State;
            public readonly Action<float, float> Handler;

            public AttributeSubscription(AttributeState state, Action<float, float> handler)
            {
                State = state;
                Handler = handler;
            }
        }

        // Efeito: pilhas, nome do efeito, objeto do alvo.
        private sealed class EffectValues : RelayValueSelector<EffectEventData>
        {
            public override bool TryGetFloat(in EffectEventData data, out float value)
            {
                value = data.Stacks;
                return true;
            }

            public override string GetText(in EffectEventData data) => NameOf(data.Effect);

            public override GameObject GetObject(in EffectEventData data) => ObjectOf(data.Owner);
        }

        // Habilidade: nível, nome da habilidade, objeto do dono.
        private sealed class AbilityValues : RelayValueSelector<AbilityEventData>
        {
            public override bool TryGetFloat(in AbilityEventData data, out float value)
            {
                value = data.Level;
                return true;
            }

            public override string GetText(in AbilityEventData data) => NameOf(data.Ability);

            public override GameObject GetObject(in AbilityEventData data) => ObjectOf(data.Owner);
        }

        // Atributo: valor novo (ou a variação, no par de queda), nome do atributo, objeto do dono.
        private sealed class AttributeValues : RelayValueSelector<AttributeChangedData>
        {
            private readonly bool _delta;

            public AttributeValues(bool delta) => _delta = delta;

            public override bool TryGetFloat(in AttributeChangedData data, out float value)
            {
                value = _delta ? data.Delta : data.NewValue;
                return true;
            }

            public override string GetText(in AttributeChangedData data) => NameOf(data.Attribute);

            public override GameObject GetObject(in AttributeChangedData data) => ObjectOf(data.Owner);
        }

        // Tag: contagem, nome da tag, objeto do dono.
        private sealed class TagValues : RelayValueSelector<TagChangedData>
        {
            public override bool TryGetFloat(in TagChangedData data, out float value)
            {
                value = data.Count;
                return true;
            }

            public override string GetText(in TagChangedData data) => data.Tag.Name;

            public override GameObject GetObject(in TagChangedData data) => ObjectOf(data.Owner);
        }

        // Bloqueio: sem número, nome da tag, objeto do dono.
        private sealed class BlockedValues : RelayValueSelector<BlockedEventData>
        {
            public override bool TryGetFloat(in BlockedEventData data, out float value)
            {
                value = 0f;
                return false;
            }

            public override string GetText(in BlockedEventData data) => data.Reason.Name;

            public override GameObject GetObject(in BlockedEventData data) => ObjectOf(data.Owner);
        }
    }
}
