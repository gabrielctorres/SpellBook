using System;
using System.Collections.Generic;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Effects
{
    /// <summary>Ação que guarda dados na instância por <see cref="ActiveEffectInstance.AddActionData"/> e os libera na remoção.</summary>
    internal interface IEffectActionCleanup
    {
        void Cleanup(ActiveEffectInstance instance, List<object> data);
    }

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
        private readonly Dictionary<GameplayAction, List<object>> _actionData = new Dictionary<GameplayAction, List<object>>();

        // Modificadores persistentes com o valor de uma pilha; nulo em efeito sem modificador persistente
        private List<TrackedModifier> _trackedModifiers;

        // Ligações dos modificadores ao vivo; nulo sem nenhuma (Snapshot não assina nada)
        private List<LiveModifierBinding> _liveBindings;

        /// <summary>Quantos modificadores ao vivo estão ligados às suas fontes. Zero em Snapshot e depois da remoção.</summary>
        public int LiveBindingCount => _liveBindings?.Count ?? 0;

        private readonly struct TrackedModifier
        {
            public readonly AttributeModifier Modifier;
            public readonly float UnitValue;
            public readonly AttributeState State;

            public TrackedModifier(AttributeModifier modifier, float unitValue, AttributeState state)
            {
                Modifier = modifier;
                UnitValue = unitValue;
                State = state;
            }
        }

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
            RunActions(Effect.OnApplied, EffectEvent.Applied);
        }

        /// <summary>
        /// Roda <paramref name="actions"/> no executor do <see cref="Target"/>, com esta instância como dono: a remoção cancela
        /// as execuções em curso.
        /// </summary>
        private void RunActions(List<GameplayAction> actions, EffectEvent effectEvent)
        {
            RunActions(actions, effectEvent, null, Target, default);
        }

        /// <summary>
        /// Roda a reação <paramref name="actions"/> pelo mesmo caminho das outras listas: os alvos do evento são
        /// <paramref name="eventInstigator"/> (nenhum se nulo ou destruído), e a ação sem seletor recebe o dono do efeito.
        /// </summary>
        internal void RunReaction(List<GameplayAction> actions, EffectEvent effectEvent, AbilitySystemComponent eventInstigator, in AttributeChangeReport change)
        {
            RunActions(actions, effectEvent, eventInstigator, eventInstigator, in change);
        }

        private void RunActions(
            List<GameplayAction> actions,
            EffectEvent effectEvent,
            AbilitySystemComponent eventInstigator,
            AbilitySystemComponent eventTarget,
            in AttributeChangeReport change)
        {
            if (actions == null || actions.Count == 0 || Target == null) return;

            ActionContext context = Target.Runner.RentContext();
            if (Source != null) context.Instigator = Source;
            context.Level = Context.Level;
            context.EffectInstance = this;
            context.EffectDefinition = Effect;
            context.EffectEvent = effectEvent;
            context.SetEffect(Context);
            context.EventInstigator = eventInstigator;
            context.Change = change;
            context.DefaultTarget = DefaultTarget.Owner;
            if (eventTarget != null) context.EventTargets.Add(eventTarget);
            Target.RunActions(actions, context, this);
        }

        public void AddActionData(GameplayAction action, object data)
        {
            if (!_actionData.ContainsKey(action)) _actionData[action] = new List<object>();
            _actionData[action].Add(data);
        }

        public bool TryGetActionData<T>(GameplayAction action, out List<T> data)
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

        /// <summary>Fator de pilhas do valor de cada modificador: <see cref="CurrentStacks"/> se o efeito multiplica pelas pilhas, senão 1.</summary>
        internal int StackFactor => Effect.StacksMultiplyMagnitude ? CurrentStacks : 1;

        /// <summary>
        /// Guarda o modificador persistente criado pelo ASC, o valor de uma pilha e o atributo que ele afeta, e grava o valor
        /// já multiplicado pelo <see cref="StackFactor"/>. Não recalcula: o ASC adiciona o modificador ao atributo depois.
        /// Devolve o índice do modificador guardado.
        /// </summary>
        internal int TrackModifier(AttributeModifier modifier, float unitValue, AttributeState state)
        {
            _trackedModifiers ??= new List<TrackedModifier>();
            _trackedModifiers.Add(new TrackedModifier(modifier, unitValue, state));
            modifier.Value = unitValue * StackFactor;
            return _trackedModifiers.Count - 1;
        }

        /// <summary>
        /// Liga o modificador guardado em <paramref name="trackedIndex"/> às fontes de <paramref name="source"/> (modo ao vivo).
        /// Valor que não depende de nada que muda não cria ligação.
        /// </summary>
        internal void BindLiveModifier(int trackedIndex, ModifierValueSource source)
        {
            LiveModifierBinding binding = LiveModifierBinding.Create(this, trackedIndex, source);
            if (binding == null) return;
            _liveBindings ??= new List<LiveModifierBinding>();
            _liveBindings.Add(binding);
        }

        /// <summary>Desfaz todas as ligações ao vivo. O ASC chama na remoção.</summary>
        internal void DisposeLiveBindings()
        {
            if (_liveBindings == null) return;
            for (int i = 0; i < _liveBindings.Count; i++) _liveBindings[i].Dispose();
            _liveBindings.Clear();
        }

        /// <summary>
        /// Reavalia o modificador ao vivo <paramref name="trackedIndex"/> com o contexto da instância e, se o valor de uma pilha
        /// mudou, grava o novo valor (vezes o fator de pilhas) e recalcula o atributo. Sem alocação.
        /// </summary>
        internal void RecalculateLiveModifier(int trackedIndex, ModifierValueSource source)
        {
            if (IsRemoved || _trackedModifiers == null || trackedIndex >= _trackedModifiers.Count) return;

            EffectContext context = Context;
            float unitValue = source.Evaluate(in context);
            TrackedModifier tracked = _trackedModifiers[trackedIndex];
            if (unitValue == tracked.UnitValue) return;

            _trackedModifiers[trackedIndex] = new TrackedModifier(tracked.Modifier, unitValue, tracked.State);
            tracked.Modifier.Value = unitValue * StackFactor;
            tracked.State.Recalculate();
        }

        /// <summary>Atualiza o nível guardado no contexto. Só o modificador ao vivo que depende do nível chama (P12).</summary>
        internal void SetContextLevel(int level)
        {
            EffectContext c = Context;
            Context = new EffectContext(c.Instigator, c.Target, c.Ability, level, c.HasImpactPoint, c.ImpactPoint, c.CallerValues);
        }

        /// <summary>
        /// Grava em cada modificador persistente <c>valor de uma pilha × fator de pilhas</c> e recalcula cada atributo afetado
        /// uma vez (ADR 0029, item 2). O ASC chama depois de toda mudança de pilhas, antes de <c>OnEffectStacksChanged</c>.
        /// </summary>
        internal void RefreshMagnitudes()
        {
            if (_trackedModifiers == null) return;

            int factor = StackFactor;
            for (int i = 0; i < _trackedModifiers.Count; i++)
            {
                TrackedModifier tracked = _trackedModifiers[i];
                tracked.Modifier.Value = tracked.UnitValue * factor;
            }

            // Um recálculo por atributo, sem alocação: cada estado é recalculado no primeiro índice que o mira
            for (int i = 0; i < _trackedModifiers.Count; i++)
            {
                AttributeState state = _trackedModifiers[i].State;
                if (IsStateTrackedBefore(state, i)) continue;
                state.Recalculate();
            }
        }

        private bool IsStateTrackedBefore(AttributeState state, int index)
        {
            for (int k = 0; k < index; k++)
            {
                if (ReferenceEquals(_trackedModifiers[k].State, state)) return true;
            }
            return false;
        }

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

                    if (!IsRemoved) RunActions(Effect.OnTick, EffectEvent.Tick);

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
            // Libera o que as ações guardaram na instância (ex.: VFX a devolver)
            foreach (KeyValuePair<GameplayAction, List<object>> pair in _actionData)
            {
                if (pair.Key is IEffectActionCleanup cleanup) cleanup.Cleanup(this, pair.Value);
            }
            _actionData.Clear();

            RunActions(Effect.OnRemoved, EffectEvent.Removed);
        }
    }
}
