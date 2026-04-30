using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Effects
{
    public class ActiveEffectInstance
    {
        public GameplayEffect Effect { get; private set; }
        public float TimeRemaining { get; private set; }
        public int CurrentStacks { get; private set; } = 1;
        public float StartTime { get; private set; }
        public AbilitySystemComponent Target { get; private set; }
        public AbilitySystemComponent Source { get; private set; }

        private float _timeSinceLastTick;
        private readonly Dictionary<EffectAction, List<object>> _actionData = new Dictionary<EffectAction, List<object>>();

        public event Action<ActiveEffectInstance> OnEffectExpired;
        public event Action<ActiveEffectInstance> OnTick;

        public ActiveEffectInstance(GameplayEffect effect, AbilitySystemComponent source, AbilitySystemComponent target)
        {
            Effect = effect;
            Source = source;
            Target = target;
            StartTime = Time.time;
            TimeRemaining = effect.Duration;
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

        public void AddStack()
        {
            if (Effect.CanStack && CurrentStacks < Effect.MaxStacks)
            {
                CurrentStacks++;
            }

            if (Effect.RefreshDurationOnStack)
            {
                TimeRemaining = Effect.Duration;
            }
        }

        public void Update(float deltaTime)
        {
            // Handle Duration
            if (Effect.DurationPolicy == EffectDurationPolicy.HasDuration)
            {
                TimeRemaining -= deltaTime;
                if (TimeRemaining <= 0)
                {
                    OnEffectExpired?.Invoke(this);
                }
            }

            // Handle Periodic Ticks
            if (Effect.Period > 0)
            {
                _timeSinceLastTick += deltaTime;
                if (_timeSinceLastTick >= Effect.Period)
                {
                    _timeSinceLastTick -= Effect.Period;
                    OnTick?.Invoke(this);

                    // Trigger Periodic Actions
                    if (Effect.OnTickActions != null)
                    {
                        foreach (var action in Effect.OnTickActions) action?.Execute(Source, Target, this);
                    }
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

