using System;
using System.Collections.Generic;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Effects
{
    public class ActiveEffectInstance
    {
        public GameplayEffect Effect { get; private set; }
        public float TimeRemaining { get; private set; }
        public int CurrentStacks { get; private set; } = 1;
        public AbilitySystemComponent Target { get; private set; }
        public AbilitySystemComponent Source { get; private set; }

        /// <summary>Duração com que a instância foi aplicada ou renovada pela última vez; a renovação volta a ela.</summary>
        public float AppliedDuration { get; private set; }

        /// <summary>Marcado pelo ASC ao remover a instância, antes da limpeza; daí em diante o <see cref="Update"/> não roda nada.</summary>
        internal bool IsRemoved { get; set; }

        /// <summary>A instância ainda está no ASC alvo; falso depois da remoção.</summary>
        public bool IsActive => !IsRemoved;

        private float _timeSinceLastTick;
        private readonly Dictionary<EffectAction, List<object>> _actionData = new Dictionary<EffectAction, List<object>>();

        public event Action<ActiveEffectInstance> OnEffectExpired;
        public event Action<ActiveEffectInstance> OnTick;

        public ActiveEffectInstance(GameplayEffect effect, AbilitySystemComponent source, AbilitySystemComponent target)
            : this(effect, effect.Duration, source, target) { }

        public ActiveEffectInstance(GameplayEffect effect, float duration, AbilitySystemComponent source, AbilitySystemComponent target)
        {
            Effect = effect;
            Source = source;
            Target = target;
            AppliedDuration = duration;
            TimeRemaining = duration;
            _timeSinceLastTick = 0;

            Initialize();
        }

        private void Initialize()
        {
            // Trigger Application Actions
            if (Effect.OnApplication != null)
            {
                foreach (var action in Effect.OnApplication) action?.Execute(Source, Target, this);
            }
        }

        public void AddActionData(EffectAction action, object data)
        {
            if (!_actionData.ContainsKey(action)) _actionData[action] = new List<object>();
            _actionData[action].Add(data);
        }

        public bool TryGetActionData<T>(EffectAction action, out List<T> data)
        {
            if (_actionData.TryGetValue(action, out var list))
            {
                data = list.ConvertAll(x => (T)x);
                return true;
            }
            data = null;
            return false;
        }

        internal void AddStack()
        {
            if (Effect.CanStack && CurrentStacks < Effect.MaxStacks)
            {
                CurrentStacks++;
            }

            if (Effect.RefreshDurationOnStack)
            {
                TimeRemaining = AppliedDuration;
            }
        }

        /// <summary>Volta o tempo restante para <paramref name="appliedDuration"/>, que passa a ser a duração aplicada. Não mexe em pilhas, no período nem roda ações.</summary>
        internal void Renew(float appliedDuration)
        {
            AppliedDuration = appliedDuration;
            TimeRemaining = appliedDuration;
        }

        internal void Update(float deltaTime)
        {
            if (IsRemoved) return;

            bool hasDuration = Effect.DurationPolicy == EffectDurationPolicy.HasDuration;

            // O tique que cabe dentro da duração acontece antes da expiração; o que sobra do quadro depois do fim não gera tique
            if (Effect.Period > 0)
            {
                _timeSinceLastTick += hasDuration ? Math.Min(deltaTime, Math.Max(TimeRemaining, 0f)) : deltaTime;
                if (_timeSinceLastTick >= Effect.Period)
                {
                    _timeSinceLastTick -= Effect.Period;
                    OnTick?.Invoke(this);

                    // Trigger Periodic Actions
                    if (Effect.OnTickActions != null)
                    {
                        foreach (var action in Effect.OnTickActions)
                        {
                            if (IsRemoved) break;
                            action?.Execute(Source, Target, this);
                        }
                    }

                    // Um ouvinte ou uma ação do tique removeu a instância
                    if (IsRemoved) return;
                }
            }

            if (hasDuration)
            {
                TimeRemaining -= deltaTime;
                if (TimeRemaining <= 0)
                {
                    OnEffectExpired?.Invoke(this);
                }
            }
        }

        public void Cleanup()
        {
            // Cleanup state for all actions that were executed
            if (Effect.OnApplication != null)
            {
                foreach (var action in Effect.OnApplication) action?.Cleanup(Source, Target, this);
            }

            if (Effect.OnTickActions != null)
            {
                foreach (var action in Effect.OnTickActions) action?.Cleanup(Source, Target, this);
            }

            // Trigger Removal Actions
            if (Effect.OnRemoval != null)
            {
                foreach (var action in Effect.OnRemoval) action?.Execute(Source, Target, this);
            }
        }
    }
}

