#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Reações do efeito a dano e morte (EV11a: CA23 sem OnAbilityExecuted, C15, P11, P12).</summary>
    public class EffectReactionsTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private AttributeDefinition _health;
        private AttributeDefinition _mana;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _health = CreateAttribute("Vida");
            _mana = CreateAttribute("Mana");
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Thorns_OnTakeDamage_InstigatorAndChange_OncePerOriginal()
        {
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(_health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent a = CreateAsc();
            AbilitySystemComponent b = CreateAsc((shield, 5f));
            var records = new List<Record>();
            b.ApplyEffect(Reacting(onTakeDamage: new RecordAction("dano", records)), b);

            b.ApplyEffect(Hit(_health, -10f), a);
            b.ApplyEffect(Hit(_health, 5f), a);
            b.ApplyAttributeChange(_health, -3f);

            Assert.That(records.Count, Is.EqualTo(1), "uma vez por alteração original; cura e código sem efeito não disparam");
            Record record = records[0];
            Assert.That(record.EventInstigator, Is.SameAs(a));
            Assert.That(record.EventTargets, Is.EqualTo(new[] { a }));
            Assert.That(record.Event, Is.EqualTo(EffectEvent.TakeDamage));
            Assert.That(record.Change.Requested, Is.EqualTo(-10f));
            Assert.That(record.Change.RedirectedFrom, Is.Null);
            Assert.That(b.GetAttributeBaseValue(shield), Is.EqualTo(0f), "o escudo absorveu parte");
        }

        [Test]
        public void OnTakeDamage_DamageAttributesFilter_EmptyMeansAny()
        {
            AbilitySystemComponent b = CreateAsc();
            var filtered = new List<Record>();
            var any = new List<Record>();
            GameplayEffect onlyHealth = Reacting(onTakeDamage: new RecordAction("vida", filtered));
            SetList(onlyHealth, "damageAttributes", _health);
            b.ApplyEffect(onlyHealth, b);
            b.ApplyEffect(Reacting(onTakeDamage: new RecordAction("qualquer", any)), b);

            b.ApplyEffect(Hit(_mana, -10f));
            b.ApplyEffect(Hit(_health, -10f));

            Assert.That(onlyHealth.DamageAttributes, Is.EqualTo(new[] { _health }));
            Assert.That(filtered.ConvertAll(r => r.Change.Attribute), Is.EqualTo(new[] { _health }), "dano em mana não dispara");
            Assert.That(any.ConvertAll(r => r.Change.Attribute), Is.EqualTo(new[] { _mana, _health }), "lista vazia aceita qualquer atributo");
        }

        [Test]
        public void Reaction_NoSelector_TargetsEffectOwner_EventTargetsSelectorTargetsInstigator()
        {
            AbilitySystemComponent a = CreateAsc();
            AbilitySystemComponent b = CreateAsc();
            var records = new List<Record>();
            var toEvent = new RecordAction("evento", records) { Target = new EventTargetsSelector() };
            b.ApplyEffect(Reacting(onTakeDamage: new GameplayAction[] { new RecordAction("padrão", records), toEvent }), b);
            GameplayEffect applied = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            applied.OnApplied.Add(new RecordAction("aplicado", records));

            b.ApplyEffect(Hit(_health, -10f), a);
            b.ApplyEffect(applied, a);

            Assert.That(records.ConvertAll(r => r.Name), Is.EqualTo(new[] { "padrão", "evento", "aplicado" }));
            Assert.That(records[0].Targets, Is.EqualTo(new[] { b }), "sem seletor, o dono do efeito");
            Assert.That(records[1].Targets, Is.EqualTo(new[] { a }), "alvos do evento, quem causou");
            Assert.That(records[2].Targets, Is.EqualTo(new[] { b }), "OnApplied continua no dono");
            Assert.That(records[2].Change.IsValid, Is.False, "fora de reação o relatório é default");
        }

        [Test]
        public void OnDeath_WhenDeathAttributeDepleted_WithKiller()
        {
            AbilitySystemComponent a = CreateAsc();
            AbilitySystemComponent b = CreateAsc((_health, 10f), (_mana, 10f));
            var records = new List<Record>();
            GameplayEffect effect = Reacting(onDeath: new RecordAction("morte", records));
            SetDeathAttribute(effect, _health);
            b.ApplyEffect(effect, b);

            b.ApplyEffect(Hit(_mana, -50f), a);
            b.ApplyEffect(Hit(_health, -30f), a);

            Assert.That(effect.DeathAttribute, Is.SameAs(_health));
            Assert.That(records.Count, Is.EqualTo(1), "mana zerada não dispara; vida zerada dispara sem ouvinte público");
            Assert.That(records[0].Event, Is.EqualTo(EffectEvent.Death));
            Assert.That(records[0].EventInstigator, Is.SameAs(a));
            Assert.That(records[0].Change.Applied, Is.EqualTo(-10f), "variação aplicada até o mínimo");
        }

        [Test]
        public void Reactions_AfterPublicEvents_LethalHitOrder()
        {
            AbilitySystemComponent b = CreateAsc((_health, 10f));
            var log = new List<string>();
            GameplayEffect effect = Reacting(onTakeDamage: new LogAction("damage-reaction", log), onDeath: new LogAction("death-reaction", log));
            SetDeathAttribute(effect, _health);
            b.ApplyEffect(effect, b);
            b.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) => log.Add("depleted-event");
            b.OnAttributeChangeApplied += (in AttributeChangeReport report) => log.Add("change-applied-event");

            b.ApplyEffect(Hit(_health, -30f));

            Assert.That(log, Is.EqualTo(new[] { "depleted-event", "death-reaction", "change-applied-event", "damage-reaction" }));
        }

        [Test]
        public void ReactionDamage_TwoThorns_BounceOnceNoLoop()
        {
            AbilitySystemComponent a = CreateAsc();
            AbilitySystemComponent b = CreateAsc();
            var countA = new CountAction();
            var countB = new CountAction();
            GameplayEffect bounce = Hit(_health, -1f);
            a.ApplyEffect(Reacting(onTakeDamage: new GameplayAction[] { countA, Thorns(bounce) }), a);
            b.ApplyEffect(Reacting(onTakeDamage: new GameplayAction[] { countB, Thorns(bounce) }), b);

            b.ApplyEffect(Hit(_health, -10f), a);

            Assert.That(countB.Count, Is.EqualTo(1), "B reage ao golpe e ignora a volta do próprio espinho");
            Assert.That(countA.Count, Is.EqualTo(1), "A reage ao espinho de B");
            Assert.That(b.GetAttributeBaseValue(_health), Is.EqualTo(89f), "100 - 10 - 1");
            Assert.That(a.GetAttributeBaseValue(_health), Is.EqualTo(99f), "100 - 1");
        }

        [Test]
        public void ReactionDepth_CappedAt8_WarnsOnce()
        {
            var chain = new AbilitySystemComponent[10];
            for (int i = 0; i < chain.Length; i++) chain[i] = CreateAsc();
            var count = new CountAction();
            GameplayEffect hit = Hit(_health, -1f);
            for (int i = 0; i < chain.Length - 1; i++)
            {
                chain[i].ApplyEffect(Reacting(onTakeDamage: new GameplayAction[] { count, new HitNextAction(chain[i + 1], hit) }), chain[i]);
            }
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GAS\] Reações de '.*' passaram de 8"));

            chain[0].ApplyEffect(hit);
            chain[0].ApplyEffect(hit);

            Assert.That(count.Count, Is.EqualTo(16), "8 reações aninhadas por golpe");
            Assert.That(chain[9].GetAttributeBaseValue(_health), Is.EqualTo(100f), "a cadeia para antes do fim");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Reaction_RemovesOwnEffect_IterationSafe_RemovedStopsReacting()
        {
            AbilitySystemComponent b = CreateAsc();
            var countSelf = new CountAction();
            var countOther = new CountAction();
            var countAfterWait = new CountAction();
            var wait = new WaitAction(1f);
            GameplayEffect self = Reacting(onTakeDamage: countSelf);
            self.OnTakeDamage.Add(new RemoveEffectAction { Effect = self });
            b.ApplyEffect(Reacting(onTakeDamage: countOther), b);
            ActiveEffectInstance waiting = b.ApplyEffect(Reacting(onTakeDamage: new GameplayAction[] { wait, countAfterWait }), b);
            b.ApplyEffect(self, b);

            b.ApplyEffect(Hit(_health, -1f));
            b.ApplyEffect(Hit(_health, -1f));
            b.RemoveEffect(waiting);
            b.Tick(2f);

            Assert.That(countSelf.Count, Is.EqualTo(1), "removido, não reage ao segundo golpe");
            Assert.That(countOther.Count, Is.EqualTo(2), "o outro efeito reage aos dois golpes");
            Assert.That(b.FindEffects(self, new List<ActiveEffectInstance>()), Is.EqualTo(0));
            Assert.That(wait.Cancelled, Is.EqualTo(2), "as duas esperas em curso cancelam com a saída do efeito");
            Assert.That(countAfterWait.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reactions_AfterWarmup_DoNotAllocate()
        {
            AbilitySystemComponent plain = CreateAsc((_health, 1000000f));
            AbilitySystemComponent reacting = CreateAsc((_health, 1000000f));
            var count = new CountAction();
            reacting.ApplyEffect(Reacting(onTakeDamage: count), reacting);
            reacting.ApplyEffect(Reacting(onTakeDamage: count), reacting);
            GameplayEffect hit = Hit(_health, -1f);
            plain.ApplyEffect(hit);
            reacting.ApplyEffect(hit);

            Assert.That(() => { for (int i = 0; i < 1000; i++) plain.ApplyEffect(hit); }, Is.Not.AllocatingGCMemory(), "sem reação");
            Assert.That(() => { for (int i = 0; i < 1000; i++) reacting.ApplyEffect(hit); }, Is.Not.AllocatingGCMemory(), "com reação");
            Assert.That(count.Count, Is.EqualTo(2002));
        }

        private GameplayEffect Reacting(GameplayAction onTakeDamage = null, GameplayAction onDeath = null)
        {
            return Reacting(onTakeDamage != null ? new[] { onTakeDamage } : null, onDeath);
        }

        private GameplayEffect Reacting(GameplayAction[] onTakeDamage, GameplayAction onDeath = null)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            if (onTakeDamage != null) effect.OnTakeDamage.AddRange(onTakeDamage);
            if (onDeath != null) effect.OnDeath.Add(onDeath);
            return effect;
        }

        private GameplayEffect Hit(AttributeDefinition attribute, float delta)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(delta) },
            });
            return effect;
        }

        private static ApplyEffectAction Thorns(GameplayEffect hit)
        {
            return new ApplyEffectAction { Effects = new List<GameplayEffect> { hit }, Target = new EventTargetsSelector() };
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 10000000f;
            return definition;
        }

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition Definition, float InitialValue)[] extra)
        {
            var attributes = new List<(AttributeDefinition, float)> { (_health, 100f), (_mana, 100f) };
            foreach (var pair in extra)
            {
                attributes.RemoveAll(p => p.Item1 == pair.Definition);
                attributes.Add(pair);
            }
            AbilitySystemComponent asc = _gas.CreateAsc(attributes.ToArray());
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetList(GameplayEffect effect, string field, params AttributeDefinition[] values)
        {
            using (var so = new SerializedObject(effect))
            {
                SerializedProperty list = so.FindProperty(field);
                list.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetDeathAttribute(GameplayEffect effect, AttributeDefinition attribute)
        {
            using (var so = new SerializedObject(effect))
            {
                so.FindProperty("deathAttribute").objectReferenceValue = attribute;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private readonly struct Record
        {
            public readonly string Name;
            public readonly EffectEvent Event;
            public readonly AbilitySystemComponent EventInstigator;
            public readonly AbilitySystemComponent[] EventTargets;
            public readonly AbilitySystemComponent[] Targets;
            public readonly AttributeChangeReport Change;

            public Record(string name, ActionContext context)
            {
                Name = name;
                Event = context.EffectEvent;
                EventInstigator = context.EventInstigator;
                EventTargets = context.EventTargets.ToArray();
                Targets = context.Targets.ToArray();
                Change = context.Change;
            }
        }

        [System.Serializable]
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

        [System.Serializable]
        private sealed class LogAction : GameplayAction
        {
            private readonly string _name;
            private readonly List<string> _log;

            public LogAction(string name, List<string> log)
            {
                _name = name;
                _log = log;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _log.Add(_name);
                return ActionStatus.Done;
            }
        }

        [System.Serializable]
        private sealed class CountAction : GameplayAction
        {
            public int Count { get; private set; }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        [System.Serializable]
        private sealed class HitNextAction : GameplayAction
        {
            private readonly AbilitySystemComponent _next;
            private readonly GameplayEffect _hit;

            public HitNextAction(AbilitySystemComponent next, GameplayEffect hit)
            {
                _next = next;
                _hit = hit;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _next.ApplyEffect(_hit, context.Owner);
                return ActionStatus.Done;
            }
        }

        [System.Serializable]
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
    }
}
#endif
