#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Listas de ações do efeito no executor do ASC (B-012, EV2: CA3, CA4; ADR 0034).</summary>
    public class EffectEventActionsTests
    {
        private GasFactory _gas;
        private TimeFactory _time;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Effect_AppliedTickRemoved_EachOnceWithEffectContext()
        {
            AbilitySystemComponent source = CreateManualAsc();
            AbilitySystemComponent target = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            var records = new List<Record>();
            effect.OnApplied.Add(new RecordAction("applied", records));
            effect.OnTick.Add(new RecordAction("tick", records));
            effect.OnRemoved.Add(new RecordAction("removed", records));

            ActiveEffectInstance instance = target.ApplyEffect(effect, source);
            target.Tick(1f);
            target.RemoveEffect(instance);

            Assert.That(records.ConvertAll(r => r.Name), Is.EqualTo(new[] { "applied", "tick", "removed" }));
            foreach (Record record in records)
            {
                Assert.That(record.HasEffect, Is.True, record.Name);
                Assert.That(record.EffectInstigator, Is.SameAs(source), record.Name);
                Assert.That(record.EffectTarget, Is.SameAs(target), record.Name);
                Assert.That(record.EffectLevel, Is.EqualTo(instance.Context.Level), record.Name);
                Assert.That(record.Instigator, Is.SameAs(source), record.Name);
                Assert.That(record.Owner, Is.SameAs(target), record.Name);
                Assert.That(record.Instance, Is.SameAs(instance), record.Name);
                Assert.That(record.Definition, Is.SameAs(effect), record.Name);
            }
        }

        [Test]
        public void EffectWaitThenApply_RemovedBefore_Cancelled()
        {
            AbilitySystemComponent target = CreateManualAsc();
            GameplayEffect marker = _gas.CreateDurationEffect(10f);
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            var wait = new WaitAction(1f);
            effect.OnApplied.Add(wait);
            effect.OnApplied.Add(new ApplyEffectProbe(marker));

            ActiveEffectInstance instance = target.ApplyEffect(effect);
            target.Tick(0.5f);
            target.RemoveEffect(instance);
            target.Tick(1f);

            Assert.That(target.FindEffects(marker, new List<ActiveEffectInstance>()), Is.EqualTo(0), "a ação depois da espera rodou com o efeito removido");
            Assert.That(wait.Cancelled, Is.EqualTo(1), "a espera em curso não recebeu OnCancel");
            Assert.That(target.HasRunningActions(instance), Is.False);
        }

        [Test]
        public void InstantEffect_OnApplied_HasDefinitionAndContext_NoInstance()
        {
            AbilitySystemComponent source = CreateManualAsc();
            AbilitySystemComponent target = CreateManualAsc();
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            var records = new List<Record>();
            effect.OnApplied.Add(new RecordAction("applied", records));

            target.ApplyEffect(effect, source);

            Assert.That(records, Has.Count.EqualTo(1));
            Record record = records[0];
            Assert.That(record.Definition, Is.SameAs(effect));
            Assert.That(record.Event, Is.EqualTo(EffectEvent.Applied));
            Assert.That(record.HasEffect, Is.True);
            Assert.That(record.EffectInstigator, Is.SameAs(source));
            Assert.That(record.EffectTarget, Is.SameAs(target));
            Assert.That(record.Instance, Is.Null);
            Assert.That(record.Owner, Is.SameAs(target));
        }

        [Test]
        public void EffectEvent_MatchesList_AppliedTickRemoved()
        {
            AbilitySystemComponent target = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            var records = new List<Record>();
            effect.OnApplied.Add(new RecordAction("applied", records));
            effect.OnTick.Add(new RecordAction("tick", records));
            effect.OnRemoved.Add(new RecordAction("removed", records));

            ActiveEffectInstance instance = target.ApplyEffect(effect);
            target.Tick(1f);
            target.RemoveEffect(instance);

            Assert.That(records.ConvertAll(r => r.Event),
                Is.EqualTo(new[] { EffectEvent.Applied, EffectEvent.Tick, EffectEvent.Removed }));
        }

        [TestCase(nameof(GameplayEffect.OnApplied), "Ao ser aplicado")]
        [TestCase(nameof(GameplayEffect.OnTick), "A cada tick")]
        [TestCase(nameof(GameplayEffect.OnRemoved), "Ao ser removido")]
        public void EffectActionList_Label_MatchesDesignSpec(string field, string expected)
        {
            FieldInfo info = typeof(GameplayEffect).GetField(field);

            var attribute = info.GetCustomAttribute<ActionEventAttribute>();

            Assert.That(attribute.Label, Is.EqualTo(expected));
        }

        private AbilitySystemComponent CreateManualAsc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        /// <summary>O que uma <see cref="RecordAction"/> leu do contexto.</summary>
        private readonly struct Record
        {
            public readonly string Name;
            public readonly EffectEvent Event;
            public readonly bool HasEffect;
            public readonly AbilitySystemComponent EffectInstigator;
            public readonly AbilitySystemComponent EffectTarget;
            public readonly int EffectLevel;
            public readonly AbilitySystemComponent Instigator;
            public readonly AbilitySystemComponent Owner;
            public readonly ActiveEffectInstance Instance;
            public readonly GameplayEffect Definition;

            public Record(string name, ActionContext context)
            {
                Name = name;
                Event = context.EffectEvent;
                HasEffect = context.HasEffect;
                EffectInstigator = context.Effect.Instigator;
                EffectTarget = context.Effect.Target;
                EffectLevel = context.Effect.Level;
                Instigator = context.Instigator;
                Owner = context.Owner;
                Instance = context.EffectInstance;
                Definition = context.EffectDefinition;
            }
        }

        private sealed class RecordAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<Record> _records;

            public RecordAction(string name, List<Record> records)
            {
                _name = name;
                _records = records;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _records.Add(new Record(_name, context));
                return ActionStatus.Done;
            }
        }

        private sealed class WaitAction : GameplayAction
        {
            private readonly float _seconds;

            public WaitAction(float seconds) => _seconds = seconds;

            public int Cancelled { get; private set; }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;
                return ActionStatus.Wait(_seconds);
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame) => Cancelled++;
        }

        /// <summary>Aplica <c>effect</c> no ASC em que a execução roda.</summary>
        private sealed class ApplyEffectProbe : GameplayAction
        {
            private readonly GameplayEffect _effect;

            public ApplyEffectProbe(GameplayEffect effect) => _effect = effect;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                context.Owner.ApplyEffect(_effect);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
