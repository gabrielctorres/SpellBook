using System;
using SpellBook.Core;
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
    public sealed class AbilitySystemEventRelay : MonoBehaviour
    {
        [SerializeField, Tooltip("ASC observado. Vazio: o do mesmo objeto.")]
        private AbilitySystemComponent abilitySystem;

        [Header("Efeitos")]
        [SerializeField] private RelayPair<EffectEventData> effectApplied = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectRemoved = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectStacksChanged = new RelayPair<EffectEventData>();
        [SerializeField] private RelayPair<EffectEventData> effectBlocked = new RelayPair<EffectEventData>();

        [Header("Bloqueio")]
        [SerializeField] private RelayPair<BlockedEventData> blocked = new RelayPair<BlockedEventData>();

        private static readonly EffectValues EffectSelector = new EffectValues();
        private static readonly BlockedValues BlockedSelector = new BlockedValues();

        private AbilitySystemComponent _subscribed;
        private bool _warnedMissingAsc;
        private Action<ActiveEffectInstance> _onEffectApplied;
        private Action<ActiveEffectInstance, EffectRemovalReason> _onEffectRemoved;
        private Action<ActiveEffectInstance, int, int> _onEffectStacksChanged;
        private Action<GameplayEffect, AbilitySystemComponent, EffectApplicationResult> _onEffectBlocked;
        private Action<GameplayTag, AbilitySystemComponent> _onBlocked;

        private void Awake()
        {
            _onEffectApplied = HandleEffectApplied;
            _onEffectRemoved = HandleEffectRemoved;
            _onEffectStacksChanged = HandleEffectStacksChanged;
            _onEffectBlocked = HandleEffectBlocked;
            _onBlocked = HandleBlocked;
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
        }

        private void HandleEffectApplied(ActiveEffectInstance instance)
        {
            EffectEventData data = EffectData(instance, instance.CurrentStacks, 0);
            data.Result = EffectApplicationResult.Applied;
            effectApplied.Raise(in data, this, EffectSelector);
        }

        private void HandleEffectRemoved(ActiveEffectInstance instance, EffectRemovalReason reason)
        {
            EffectEventData data = EffectData(instance, 0, instance.CurrentStacks);
            data.RemovalReason = reason;
            effectRemoved.Raise(in data, this, EffectSelector);
        }

        private void HandleEffectStacksChanged(ActiveEffectInstance instance, int before, int after)
        {
            EffectEventData data = EffectData(instance, after, before);
            effectStacksChanged.Raise(in data, this, EffectSelector);
        }

        private void HandleEffectBlocked(GameplayEffect effect, AbilitySystemComponent source, EffectApplicationResult result)
        {
            var data = new EffectEventData { Owner = _subscribed, Source = source, Effect = effect, Result = result };
            effectBlocked.Raise(in data, this, EffectSelector);
        }

        private void HandleBlocked(GameplayTag reason, AbilitySystemComponent source)
        {
            var data = new BlockedEventData { Owner = _subscribed, Reason = reason, Source = source };
            blocked.Raise(in data, this, BlockedSelector);
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
