#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Tique dentro da duração acontece antes da expiração (B-002, NC7; Achado B2-1).</summary>
    public class EffectPeriodTests
    {
        private const float Step = 0.5f;

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
        public void Duration4_Period0_5_AppliesPeriodicModifier8Times()
        {
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            asc.ApplyEffect(CreatePeriodicEffect(EffectDurationPolicy.HasDuration, 4f, 0.5f, hits));

            TickTimes(asc, 10);

            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(8f));
        }

        [Test]
        public void TickActionsAndPeriodicModifiers_RunSameNumberOfTimes()
        {
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            GameplayEffect effect = CreatePeriodicEffect(EffectDurationPolicy.HasDuration, 4f, 0.5f, hits);
            var tick = new TickProbe();
            effect.OnTick.Add(tick);
            asc.ApplyEffect(effect);

            TickTimes(asc, 10);

            Assert.That(tick.Count, Is.EqualTo(8), "ações de tique");
            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(8f), "modificadores periódicos");
        }

        [Test]
        public void TickActions_NeverRunAfterRemoval()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(4f);
            effect.Period = 0.5f;
            var removal = new RemovalProbe();
            var tick = new TickProbe(removal);
            effect.OnTick.Add(tick);
            effect.OnRemoved.Add(removal);
            asc.ApplyEffect(effect);

            TickTimes(asc, 10);

            Assert.That(removal.Count, Is.EqualTo(1), "o efeito não expirou uma vez");
            Assert.That(tick.CountAfterRemoval, Is.EqualTo(0), "uma ação de tique rodou depois da OnRemoval");
        }

        [Test]
        public void TickActionRemovesOwnInstance_CleansUpOnce_NoFurtherTick()
        {
            // O segundo tique cai no quadro da expiração (1 s / 0,5 s) e é ele que remove a instância
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            GameplayEffect effect = CreatePeriodicEffect(EffectDurationPolicy.HasDuration, 1f, 0.5f, hits);
            var removal = new RemovalProbe();
            var tick = new TickProbe(removal, removeOnCount: 2);
            effect.OnTick.Add(tick);
            effect.OnRemoved.Add(removal);
            asc.ApplyEffect(effect);

            TickTimes(asc, 4);

            Assert.That(tick.Count, Is.EqualTo(2), "ações de tique");
            Assert.That(tick.CountAfterRemoval, Is.EqualTo(0), "uma ação de tique rodou depois da OnRemoval");
            Assert.That(removal.Count, Is.EqualTo(1), "a limpeza não rodou uma vez");
            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(2f), "modificadores periódicos");
        }

        [Test]
        public void DeltaBeyondExpiry_TicksOnceThenExpires()
        {
            GameplayTag poisoned = _gas.CreateTag("State.Poisoned");
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            GameplayEffect effect = CreatePeriodicEffect(EffectDurationPolicy.HasDuration, 4f, 0.5f, hits);
            effect.GrantedTags.Add(poisoned);
            var removal = new RemovalProbe();
            var tick = new TickProbe(removal);
            effect.OnTick.Add(tick);
            effect.OnRemoved.Add(removal);
            asc.ApplyEffect(effect);

            asc.Tick(10f);

            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(1f), "modificadores periódicos");
            Assert.That(tick.Count, Is.EqualTo(1), "ações de tique");
            Assert.That(tick.CountAfterRemoval, Is.EqualTo(0), "a ação de tique rodou depois da OnRemoval");
            Assert.That(removal.Count, Is.EqualTo(1), "o efeito não expirou");
            Assert.That(asc.HasTag(poisoned), Is.False, "o efeito continua ativo");
        }

        [Test]
        public void Duration4_Period1_5_TickCountUnchanged()
        {
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            GameplayEffect effect = CreatePeriodicEffect(EffectDurationPolicy.HasDuration, 4f, 1.5f, hits);
            var tick = new TickProbe();
            effect.OnTick.Add(tick);
            asc.ApplyEffect(effect);

            TickTimes(asc, 2); // 1 s
            float beforeFirst = asc.GetAttributeValue(hits);
            TickTimes(asc, 1); // 1,5 s
            float atFirst = asc.GetAttributeValue(hits);
            TickTimes(asc, 3); // 3 s
            float atSecond = asc.GetAttributeValue(hits);
            TickTimes(asc, 4); // 5 s, depois da expiração em 4 s

            Assert.That(beforeFirst, Is.EqualTo(0f), "tique antes de 1,5 s");
            Assert.That(atFirst, Is.EqualTo(1f), "tique de 1,5 s");
            Assert.That(atSecond, Is.EqualTo(2f), "tique de 3 s");
            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(2f), "tique depois de 3 s");
            Assert.That(tick.Count, Is.EqualTo(2), "ações de tique");
        }

        [Test]
        public void InfinitePeriodic_TickCountUnchanged()
        {
            AttributeDefinition hits = _gas.CreateAttribute("Hits");
            AbilitySystemComponent asc = CreateManualAsc((hits, 0f));
            GameplayEffect effect = CreatePeriodicEffect(EffectDurationPolicy.Infinite, 0f, 0.5f, hits);
            var tick = new TickProbe();
            effect.OnTick.Add(tick);
            asc.ApplyEffect(effect);

            TickTimes(asc, 8); // 4 s
            asc.Tick(10f); // um tique por Tick, como antes

            Assert.That(asc.GetAttributeValue(hits), Is.EqualTo(9f), "modificadores periódicos");
            Assert.That(tick.Count, Is.EqualTo(9), "ações de tique");
        }

        /// <summary>Efeito periódico que soma 1 em <paramref name="attribute"/> a cada tique.</summary>
        private GameplayEffect CreatePeriodicEffect(
            EffectDurationPolicy policy,
            float duration,
            float period,
            AttributeDefinition attribute)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, duration);
            effect.Period = period;
            effect.PeriodicModifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(1f) },
            });
            return effect;
        }

        private static void TickTimes(AbilitySystemComponent asc, int count)
        {
            for (int i = 0; i < count; i++) asc.Tick(Step);
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        /// <summary>Ação de remoção que conta quantas vezes rodou.</summary>
        private sealed class RemovalProbe : GameplayAction
        {
            public int Count { get; private set; }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        /// <summary>
        /// Ação de tique que conta as execuções e as que vieram depois da <see cref="RemovalProbe"/>.
        /// Com <c>removeOnCount</c>, remove a própria instância na execução de número dado.
        /// </summary>
        private sealed class TickProbe : GameplayAction
        {
            private readonly RemovalProbe _removal;
            private readonly int _removeOnCount;

            public TickProbe(RemovalProbe removal = null, int removeOnCount = 0)
            {
                _removal = removal;
                _removeOnCount = removeOnCount;
            }

            public int Count { get; private set; }
            public int CountAfterRemoval { get; private set; }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                if (_removal != null && _removal.Count > 0) CountAfterRemoval++;
                if (Count == _removeOnCount) context.Owner.RemoveEffect(context.EffectInstance);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
