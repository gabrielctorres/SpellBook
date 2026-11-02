#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Morte como sinal: evento do ASC com a causa e tag de estado no mínimo (B-010, DM5: CA10, CA15 a CA18, CA20, CA24).
    /// </summary>
    public class DeathSignalTests
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
        public void Depleted_WithInstigatorAndEffect_OnceAtMin_AgainAfterHeal()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent target = CreateManualAsc((health, 100f));
            AbilitySystemComponent attacker = CreateManualAsc();
            var causes = new List<AttributeChangeReport>();
            var states = new List<AttributeState>();
            target.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) =>
            {
                states.Add(state);
                causes.Add(cause);
            };
            GameplayEffect hit = Damage(health, 150f);

            target.ApplyEffect(hit, attacker);
            target.ApplyEffect(hit, attacker);
            int whileDead = causes.Count;
            target.ApplyAttributeChange(health, 30f);
            target.ApplyEffect(hit, attacker);

            Assert.That(whileDead, Is.EqualTo(1), "uma vez ao chegar ao mínimo, sem repetir");
            Assert.That(causes.Count, Is.EqualTo(2), "de novo depois da cura");
            target.AttributeSet.TryGetState(health, out AttributeState healthState);
            Assert.That(states[0], Is.SameAs(healthState));
            Assert.That(causes[0].IsValid, Is.True);
            Assert.That(causes[0].Attribute, Is.SameAs(health));
            Assert.That(causes[0].Effect, Is.SameAs(hit));
            Assert.That(causes[0].Context.Instigator, Is.SameAs(attacker));
            Assert.That(causes[0].Requested, Is.EqualTo(-150f));
            Assert.That(causes[0].Applied, Is.EqualTo(-100f), "aplicado limitado ao mínimo");
        }

        [Test]
        public void DestroyedInstigator_NullNoException()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent target = CreateManualAsc((health, 100f));
            var attackerObject = new GameObject("Atacante");
            AbilitySystemComponent attacker = attackerObject.AddComponent<AbilitySystemComponent>();
            GameplayEffect poison = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            poison.Period = 1f;
            poison.PeriodicModifiers.Add(Modifier(health, -100f));
            target.ApplyEffect(poison, attacker);
            Object.DestroyImmediate(attackerObject); // o atacante morre antes do tique do veneno
            bool instigatorIsNull = false;
            int deaths = 0;
            target.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) =>
            {
                deaths++;
                instigatorIsNull = cause.Context.Instigator == null;
            };

            Assert.DoesNotThrow(() => target.Tick(1f));

            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(instigatorIsNull, Is.True, "instigador destruído chega nulo");
        }

        [Test]
        public void StateTag_GrantedAtMin_ReturnedOnHeal_EmptyFieldNothing()
        {
            GameplayTag dead = _gas.CreateTag("Estado.Morto");
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition mana = CreateAttribute("Mana");
            SetStateTag(health, dead);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (mana, 50f));
            var tagChanges = new List<bool>();
            asc.OnTagChanged += (tag, added) => { if (tag == dead) tagChanges.Add(added); };

            asc.ApplyAttributeChange(health, -100f);
            bool deadAtMin = asc.HasTag(dead);
            asc.ApplyAttributeChange(mana, -50f);
            asc.ApplyAttributeChange(health, 10f);

            Assert.That(deadAtMin, Is.True, "tag concedida no mínimo");
            Assert.That(asc.HasTag(dead), Is.False, "tag devolvida na cura");
            Assert.That(tagChanges, Is.EqualTo(new[] { true, false }), "a mana sem tag configurada não mexe em nada");
        }

        [Test]
        public void BornAtMin_HasStateTag()
        {
            GameplayTag dead = _gas.CreateTag("Estado.Morto");
            AttributeDefinition health = CreateAttribute("Vida");
            SetStateTag(health, dead);

            AbilitySystemComponent asc = CreateManualAsc((health, 0f));

            Assert.That(asc.HasTag(dead), Is.True);
        }

        [Test]
        public void StateTag_BlocksAbilityViaBlockedTags()
        {
            GameplayTag dead = _gas.CreateTag("Estado.Morto");
            AttributeDefinition health = CreateAttribute("Vida");
            SetStateTag(health, dead);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            GameplayAbility ability = _gas.CreateAbility();
            ability.BlockedTags.Add(dead);
            asc.GiveAbility(ability);

            asc.ApplyAttributeChange(health, -100f);
            AbilityActivationResult whileDead = asc.TryActivateAbility(ability, Vector3.zero);
            asc.ApplyAttributeChange(health, 50f);
            AbilityActivationResult afterHeal = asc.TryActivateAbility(ability, Vector3.zero);

            Assert.That(whileDead, Is.EqualTo(AbilityActivationResult.BlockedByTags));
            Assert.That(afterHeal, Is.EqualTo(AbilityActivationResult.Success));
        }

        [Test]
        public void HealAfterZeroBase_RaisesHealth()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            asc.ApplyEffect(Damage(health, 300f));

            asc.ApplyEffect(Damage(health, -40f));

            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(40f), "a cura parte do zero, sem dívida");
        }

        [Test]
        public void ListenerHealsInsideDeath_NoExceptionNoSkippedEvents()
        {
            GameplayTag dead = _gas.CreateTag("Estado.Morto");
            AttributeDefinition health = CreateAttribute("Vida");
            SetStateTag(health, dead);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            int deaths = 0;
            asc.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) =>
            {
                deaths++;
                asc.ApplyAttributeChange(health, 40f); // o ouvinte revive dentro do evento
            };

            Assert.DoesNotThrow(() => asc.ApplyAttributeChange(health, -100f));
            Assert.DoesNotThrow(() => asc.ApplyAttributeChange(health, -100f));

            Assert.That(deaths, Is.EqualTo(2), "a segunda morte não foi pulada");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(40f));
            Assert.That(asc.HasTag(dead), Is.False, "a tag saiu com a cura de dentro do evento");
        }

        [Test]
        public void LimitChangeDepletion_CauseInvalid()
        {
            AttributeDefinition floor = CreateAttribute("Piso");
            AttributeDefinition health = CreateAttribute("Vida");
            health.MinSource = LimitSource.Attribute;
            health.MinAttribute = floor;
            AbilitySystemComponent asc = CreateManualAsc((floor, 0f), (health, 50f));
            var causes = new List<AttributeChangeReport>();
            asc.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) => causes.Add(cause);

            asc.ApplyAttributeChange(floor, 60f);

            Assert.That(causes.Count, Is.EqualTo(1), "a vida chegou ao mínimo pelo limite");
            Assert.That(causes[0].IsValid, Is.False, "a alteração em curso é de outro atributo");
            Assert.That(causes[0].Effect, Is.Null);
            Assert.That(causes[0].Context.Instigator, Is.Null);
        }

        [Test]
        public void PersistentModifierDepletion_CauseInvalid()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent asc = CreateManualAsc((health, 50f));
            var causes = new List<AttributeChangeReport>();
            asc.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) => causes.Add(cause);
            GameplayEffect curse = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            curse.Modifiers.Add(Modifier(health, -100f));

            asc.ApplyEffect(curse);

            Assert.That(causes.Count, Is.EqualTo(1));
            Assert.That(causes[0].IsValid, Is.False, "modificador persistente não é alteração do pipeline");
        }

        [Test]
        public void ReachedMax_RaisesWithCause()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent asc = CreateManualAsc((health, 900f));
            var causes = new List<AttributeChangeReport>();
            asc.OnAttributeReachedMax += (AttributeState state, in AttributeChangeReport cause) => causes.Add(cause);
            GameplayEffect heal = Damage(health, -500f);

            asc.ApplyEffect(heal);

            Assert.That(causes.Count, Is.EqualTo(1));
            Assert.That(causes[0].Effect, Is.SameAs(heal));
            Assert.That(causes[0].Applied, Is.EqualTo(100f));
        }

        [Test]
        public void Death1000_WithStateTag_DoNotAllocate()
        {
            GameplayTag dead = _gas.CreateTag("Estado.Morto");
            AttributeDefinition health = CreateAttribute("Vida");
            SetStateTag(health, dead);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            int deaths = 0;
            asc.OnAttributeDepleted += (AttributeState state, in AttributeChangeReport cause) => deaths++;
            asc.ApplyAttributeChange(health, -100f); // aquecimento da morte, da tag e da cura
            asc.ApplyAttributeChange(health, 100f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    asc.ApplyAttributeChange(health, -100f);
                    asc.ApplyAttributeChange(health, 100f);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(deaths, Is.EqualTo(1001), "o evento não disparou na medição");
            Assert.That(asc.HasTag(dead), Is.False);
        }

        private GameplayEffect Damage(AttributeDefinition attribute, float amount)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(attribute, -amount));
            return effect;
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) },
            };
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private static void SetStateTag(AttributeDefinition definition, GameplayTag tag)
        {
            using (var so = new SerializedObject(definition))
            {
                so.FindProperty("stateTagWhileDepleted.definition").objectReferenceValue = tag.GetDefinition();
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }
    }
}
#endif
