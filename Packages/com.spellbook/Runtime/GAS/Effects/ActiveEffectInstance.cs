using System;
using System.Collections.Generic;
using SpellBook.GAS.Attributes;
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

        /// <summary>Repasse de <see cref="GameplayEffect.IsBuff"/>.</summary>
        public bool IsBuff => Effect.IsBuff;

        /// <summary>Repasse de <see cref="GameplayEffect.IsDebuff"/>.</summary>
        public bool IsDebuff => Effect.IsDebuff;

        /// <summary>Repasse de <see cref="GameplayEffect.IsHidden"/>.</summary>
        public bool IsHidden => Effect.IsHidden;

        /// <summary>Repasse de <see cref="GameplayEffect.IsPurgable"/>.</summary>
        public bool IsPurgable => Effect.IsPurgable;

        /// <summary>Duração com que a instância foi aplicada ou renovada pela última vez; a renovação volta a ela.</summary>
        public float AppliedDuration { get; private set; }

        /// <summary>Marcado pelo ASC ao remover a instância, antes da limpeza; daí em diante o <see cref="Update"/> não roda nada.</summary>
        internal bool IsRemoved { get; set; }

        /// <summary>A instância ainda está no ASC alvo; falso depois da remoção.</summary>
        public bool IsActive => !IsRemoved;

        private float _timeSinceLastTick;

        // Último valor de cada modificador periódico que lê o instigador; o tique usa este valor se o instigador foi destruído.
        private readonly float[] _lastTickValues;
        private readonly Dictionary<EffectAction, List<object>> _actionData = new Dictionary<EffectAction, List<object>>();

        public event Action<ActiveEffectInstance> OnEffectExpired;
        public event Action<ActiveEffectInstance> OnTick;

        /// <summary>
        /// Contexto da aplicação (cópia): instigador, alvo, habilidade, nível, ponto de impacto e valores do chamador. Os valores
        /// do chamador que os modificadores do efeito usam são copiados na aplicação; mudar o objeto de quem chamou depois não
        /// muda a instância.
        /// </summary>
        public EffectContext Context { get; private set; }

        public ActiveEffectInstance(GameplayEffect effect, AbilitySystemComponent source, AbilitySystemComponent target)
            : this(effect, effect.Duration.Evaluate(effect.DefaultLevel), source, target) { }

        public ActiveEffectInstance(GameplayEffect effect, float duration, AbilitySystemComponent source, AbilitySystemComponent target)
            : this(effect, duration, new EffectContext(source, target, null, effect.DefaultLevel, false, default, null)) { }

        internal ActiveEffectInstance(GameplayEffect effect, float duration, in EffectContext context)
        {
            Effect = effect;
            Context = context.WithCallerValues(CopyCallerValues(effect, context.CallerValues));
            Source = context.Instigator;
            Target = context.Target;
            AppliedDuration = duration;
            TimeRemaining = duration;
            _timeSinceLastTick = 0;
            _lastTickValues = CaptureTickValues();

            Initialize();
        }

        // Uma alocação por instância, só quando há valores do chamador que os modificadores do efeito leem (ADR 0030, item 3).
        private static SetByCallerValues CopyCallerValues(GameplayEffect effect, IReadOnlySetByCaller values)
        {
            if (values == null || values.Count == 0) return null;
            SetByCallerValues copy = null;
            CopyCallerValues(effect.Modifiers, values, ref copy);
            CopyCallerValues(effect.PeriodicModifiers, values, ref copy);
            return copy;
        }

        private static void CopyCallerValues(List<AttributeEffectModifier> modifiers, IReadOnlySetByCaller values, ref SetByCallerValues copy)
        {
            if (modifiers == null) return;
            foreach (AttributeEffectModifier mod in modifiers)
            {
                ModifierValueSource source = mod?.ValueSource;
                if (source == null || source.Type != ModifierValueSource.SourceType.SetByCaller) continue;
                if (!values.TryGet(source.CallerName, out float value)) continue;
                copy ??= new SetByCallerValues();
                copy.Set(source.CallerName, value);
            }
        }

        // O valor na aplicação é o primeiro valor conhecido de cada modificador periódico que lê o instigador.
        private float[] CaptureTickValues()
        {
            List<AttributeEffectModifier> modifiers = Effect.PeriodicModifiers;
            if (Effect.Period <= 0 || modifiers == null || modifiers.Count == 0) return null;

            var values = new float[modifiers.Count];
            EffectContext context = Context;
            for (int i = 0; i < modifiers.Count; i++)
            {
                ModifierValueSource source = modifiers[i]?.ValueSource;
                if (source != null && source.ReadsInstigator) values[i] = source.Evaluate(in context);
            }
            return values;
        }

        /// <summary>
        /// Valor do modificador periódico <paramref name="index"/> no tique, com o contexto da instância. Se ele lê o instigador
        /// e o instigador foi destruído, vale o último valor conhecido (P8), sem exceção.
        /// </summary>
        internal float EvaluateTickModifier(int index, ModifierValueSource source)
        {
            if (source == null) return 0f;
            EffectContext context = Context;
            if (_lastTickValues == null || index >= _lastTickValues.Length || !source.ReadsInstigator) return source.Evaluate(in context);
            if (context.Instigator == null) return _lastTickValues[index];

            float value = source.Evaluate(in context);
            _lastTickValues[index] = value;
            return value;
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

        // Atalhos: a regra e os eventos ficam no ASC alvo. Instância sem alvo ou removida devolve false.

        /// <summary>Atalho de <see cref="AbilitySystemComponent.ExtendEffectDuration"/> no <see cref="Target"/>.</summary>
        public bool ExtendDuration(float seconds) => IsActive && Target != null && Target.ExtendEffectDuration(this, seconds);

        /// <summary>Atalho de <see cref="AbilitySystemComponent.ReduceEffectDuration"/> no <see cref="Target"/>.</summary>
        public bool ReduceDuration(float seconds) => IsActive && Target != null && Target.ReduceEffectDuration(this, seconds);

        /// <summary>Atalho de <see cref="AbilitySystemComponent.RestartEffectDuration"/> no <see cref="Target"/>.</summary>
        public bool RestartDuration() => IsActive && Target != null && Target.RestartEffectDuration(this);

        /// <summary>Atalho de <see cref="AbilitySystemComponent.SetEffectStacks"/> no <see cref="Target"/>.</summary>
        public bool SetStacks(int count) => IsActive && Target != null && Target.SetEffectStacks(this, count);

        /// <summary>Atalho de <see cref="AbilitySystemComponent.RemoveEffectStacks"/> no <see cref="Target"/>.</summary>
        public bool RemoveStacks(int count) => IsActive && Target != null && Target.RemoveEffectStacks(this, count);

        internal void SetTimeRemaining(float seconds) => TimeRemaining = seconds;

        internal void SetStackCount(int count) => CurrentStacks = count;

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

