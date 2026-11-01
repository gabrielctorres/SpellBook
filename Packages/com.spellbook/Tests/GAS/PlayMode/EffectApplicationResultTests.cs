using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Resultado de aplicação de efeito e bloqueios (B-004, RL1: CA1 a CA10).</summary>
    public class EffectApplicationResultTests
    {
        private const float Duration = 8f;

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
        public void NewDurationEffect_Applied()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);

            EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance instance);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.Applied));
            Assert.That(instance, Is.Not.Null);
            Assert.That(asc.ActiveEffects, Has.Member(instance));
        }

        [Test]
        public void ReapplyWithCanStack_Stacked_AlsoAtMaxStacks()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = CreateStackingEffect(maxStacks: 2);
            asc.TryApplyEffect(effect, null, out ActiveEffectInstance first);

            EffectApplicationResult stacked = asc.TryApplyEffect(effect, null, out ActiveEffectInstance second);
            EffectApplicationResult atMax = asc.TryApplyEffect(effect, null, out ActiveEffectInstance third);

            Assert.That(stacked, Is.EqualTo(EffectApplicationResult.Stacked));
            Assert.That(atMax, Is.EqualTo(EffectApplicationResult.Stacked), "em MaxStacks a reaplicação também é Stacked");
            Assert.That(second, Is.SameAs(first));
            Assert.That(third, Is.SameAs(first));
            Assert.That(first.CurrentStacks, Is.EqualTo(2));
        }

        [Test]
        public void InstantEffect_AppliedInstant()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent asc = CreateManualAsc((health, 50f));
            GameplayEffect damage = CreateInstantAdd(health, -10f);

            EffectApplicationResult result = asc.TryApplyEffect(damage, null, out ActiveEffectInstance instance);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.AppliedInstant));
            Assert.That(instance, Is.Null);
            Assert.That(asc.GetAttributeValue(health), Is.EqualTo(40f));
        }

        [Test]
        public void ImmuneTarget_BlockedByImmunity_NothingApplied()
        {
            GameplayTag immune = _gas.CreateTag("State.Immune");
            AbilitySystemComponent asc = CreateManualAsc();
            GiveTag(asc, immune);
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);
            effect.ApplicationImmunityTags.Add(immune);
            int before = asc.ActiveEffects.Count;

            EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance instance);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.BlockedByImmunity));
            Assert.That(instance, Is.Null);
            Assert.That(asc.ActiveEffects.Count, Is.EqualTo(before));
        }

        [Test]
        public void MissingRequirement_MissingRequirements_CheckedBeforeImmunity()
        {
            GameplayTag immune = _gas.CreateTag("State.Immune");
            GameplayTag wet = _gas.CreateTag("State.Wet");
            AbilitySystemComponent asc = CreateManualAsc();
            GiveTag(asc, immune);
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);
            effect.ApplicationRequirements.Add(wet);
            effect.ApplicationImmunityTags.Add(immune);

            EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance instance);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.MissingRequirements));
            Assert.That(instance, Is.Null);
        }

        [Test]
        public void NullEffect_Invalid()
        {
            AbilitySystemComponent asc = CreateManualAsc();

            EffectApplicationResult result = asc.TryApplyEffect(null, null, out ActiveEffectInstance instance);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.Invalid));
            Assert.That(instance, Is.Null);
        }

        [Test]
        public void ReapplyWithoutCanStack_Refreshed()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);
            asc.TryApplyEffect(effect, null, out ActiveEffectInstance first);

            EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance second);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.Refreshed));
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public void Instance_FilledOnlyInAppliedStackedRefreshed()
        {
            foreach (EffectApplicationResult expected in Enum.GetValues(typeof(EffectApplicationResult)))
            {
                AbilitySystemComponent asc = CreateManualAsc();
                GameplayEffect effect = Arrange(asc, expected);

                EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance instance);

                Assert.That(result, Is.EqualTo(expected));
                bool hasInstance = expected == EffectApplicationResult.Applied
                    || expected == EffectApplicationResult.Stacked
                    || expected == EffectApplicationResult.Refreshed;
                Assert.That(instance != null, Is.EqualTo(hasInstance), $"instância em {expected}");
            }
        }

        [Test]
        public void Null_Invalid_NoException()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            EffectApplicationResult withOverride = EffectApplicationResult.Applied;
            EffectApplicationResult withoutOverride = EffectApplicationResult.Applied;

            Assert.DoesNotThrow(() => withoutOverride = asc.TryApplyEffect(null, null, out _));
            Assert.DoesNotThrow(() => withOverride = asc.TryApplyEffect(null, 3f, null, out _));
            Assert.DoesNotThrow(() => asc.ApplyEffect(null));
            Assert.DoesNotThrow(() => asc.ApplyEffect(null, 3f));

            Assert.That(withoutOverride, Is.EqualTo(EffectApplicationResult.Invalid));
            Assert.That(withOverride, Is.EqualTo(EffectApplicationResult.Invalid));
        }

        [Test]
        public void ApplyEffect_SameReturnAsBefore_EachCase()
        {
            foreach (EffectApplicationResult expected in Enum.GetValues(typeof(EffectApplicationResult)))
            {
                AbilitySystemComponent asc = CreateManualAsc();
                GameplayEffect effect = Arrange(asc, expected);
                ActiveEffectInstance existing = effect != null ? FindInstance(asc, effect) : null;

                ActiveEffectInstance returned = asc.ApplyEffect(effect);

                switch (expected)
                {
                    case EffectApplicationResult.Applied:
                        Assert.That(returned, Is.Not.Null, "Applied devolve a instância nova");
                        Assert.That(asc.ActiveEffects, Has.Member(returned));
                        break;
                    case EffectApplicationResult.Stacked:
                    case EffectApplicationResult.Refreshed:
                        Assert.That(returned, Is.SameAs(existing), $"{expected} devolve a instância existente");
                        break;
                    default:
                        Assert.That(returned, Is.Null, $"{expected} devolve null");
                        break;
                }
            }
        }

        [Test]
        public void ApplyEffectWithOverride_SameReturnAsTryApply()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);

            ActiveEffectInstance applied = asc.ApplyEffect(effect, 3f);
            float appliedDuration = applied.AppliedDuration;
            EffectApplicationResult result = asc.TryApplyEffect(effect, 5f, null, out ActiveEffectInstance refreshed);

            Assert.That(appliedDuration, Is.EqualTo(3f));
            Assert.That(result, Is.EqualTo(EffectApplicationResult.Refreshed));
            Assert.That(refreshed, Is.SameAs(applied));
            Assert.That(refreshed.AppliedDuration, Is.EqualTo(5f));
        }

        [Test]
        public void InstantNeverHasInstance_DurationNeverInstant()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent asc = CreateManualAsc((health, 50f));
            GameplayEffect instant = CreateInstantAdd(health, -1f);
            GameplayEffect duration = _gas.CreateDurationEffect(Duration);
            GameplayEffect infinite = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);

            var results = new List<EffectApplicationResult>();
            for (int i = 0; i < 2; i++)
            {
                Assert.That(asc.TryApplyEffect(instant, null, out ActiveEffectInstance a), Is.EqualTo(EffectApplicationResult.AppliedInstant));
                Assert.That(a, Is.Null);
                Assert.That(asc.TryApplyEffect(instant, 2f, null, out ActiveEffectInstance b), Is.EqualTo(EffectApplicationResult.AppliedInstant));
                Assert.That(b, Is.Null);
                results.Add(asc.TryApplyEffect(duration, null, out _));
                results.Add(asc.TryApplyEffect(duration, 2f, null, out _));
                results.Add(asc.TryApplyEffect(infinite, null, out _));
            }

            Assert.That(results, Has.No.Member(EffectApplicationResult.AppliedInstant));
        }

        [Test]
        public void ReapplyWithoutCanStack_Refreshed_SingleInstance()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);
            asc.TryApplyEffect(effect, null, out ActiveEffectInstance first);
            asc.Tick(3f);

            EffectApplicationResult result = asc.TryApplyEffect(effect, null, out ActiveEffectInstance second);

            Assert.That(result, Is.EqualTo(EffectApplicationResult.Refreshed));
            Assert.That(second, Is.SameAs(first));
            Assert.That(asc.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(second.CurrentStacks, Is.EqualTo(1));
            Assert.That(second.TimeRemaining, Is.EqualTo(Duration), "a reaplicação não renovou a duração");
        }

        [Test]
        public void Enum_Order()
        {
            Assert.That((int)EffectApplicationResult.Applied, Is.EqualTo(0));
            Assert.That((int)EffectApplicationResult.Stacked, Is.EqualTo(1));
            Assert.That((int)EffectApplicationResult.AppliedInstant, Is.EqualTo(2));
            Assert.That((int)EffectApplicationResult.BlockedByImmunity, Is.EqualTo(3));
            Assert.That((int)EffectApplicationResult.MissingRequirements, Is.EqualTo(4));
            Assert.That((int)EffectApplicationResult.Invalid, Is.EqualTo(5));
            Assert.That((int)EffectApplicationResult.Refreshed, Is.EqualTo(6));
            Assert.That(Enum.GetValues(typeof(EffectApplicationResult)).Length, Is.EqualTo(7));
        }

        [Test]
        public void TryApplyInstant_DoesNotAllocate_10000Times()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            mana.MinValue = 0f;
            mana.MaxValue = 1000000f;
            AbilitySystemComponent asc = CreateManualAsc((mana, 0f));
            GameplayEffect gain = CreateInstantAdd(mana, 1f);
            for (int i = 0; i < 10; i++) asc.TryApplyEffect(gain, null, out _); // aquecimento
            EffectApplicationResult last = EffectApplicationResult.Invalid;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) last = asc.TryApplyEffect(gain, null, out _);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(last, Is.EqualTo(EffectApplicationResult.AppliedInstant));
            Assert.That(asc.GetAttributeValue(mana), Is.EqualTo(10010f));
        }

        [Test]
        public void Blocked_FiresOncePerReason_NotOnOthers()
        {
            GameplayTag immune = _gas.CreateTag("State.Immune");
            GameplayTag wet = _gas.CreateTag("State.Wet");
            AbilitySystemComponent asc = CreateManualAsc();
            AbilitySystemComponent caster = CreateManualAsc();
            GiveTag(asc, immune);
            GameplayEffect blockedByImmunity = _gas.CreateDurationEffect(Duration);
            blockedByImmunity.ApplicationImmunityTags.Add(immune);
            GameplayEffect missing = _gas.CreateDurationEffect(Duration);
            missing.ApplicationRequirements.Add(wet);
            var calls = new List<(GameplayEffect Effect, AbilitySystemComponent Source, EffectApplicationResult Result)>();
            asc.OnEffectBlocked += (effect, source, result) => calls.Add((effect, source, result));

            asc.TryApplyEffect(blockedByImmunity, caster, out _);
            asc.ApplyEffect(missing);
            asc.TryApplyEffect(null, null, out _);
            asc.TryApplyEffect(_gas.CreateDurationEffect(Duration), null, out _);
            asc.TryApplyEffect(_gas.CreateEffect(EffectDurationPolicy.Instant, 0f), null, out _);

            Assert.That(calls.Count, Is.EqualTo(2), "dispara só nos dois bloqueios, uma vez cada");
            Assert.That(calls[0], Is.EqualTo((blockedByImmunity, caster, EffectApplicationResult.BlockedByImmunity)));
            Assert.That(calls[1], Is.EqualTo((missing, asc, EffectApplicationResult.MissingRequirements)), "origem nula vira o próprio ASC");
        }

        [Test]
        public void ReportBlocked_FiresWithTagAndSource_InvalidIgnored()
        {
            GameplayTag silenced = _gas.CreateTag("Blocked.Silenced");
            AbilitySystemComponent asc = CreateManualAsc();
            AbilitySystemComponent caster = CreateManualAsc();
            var calls = new List<(GameplayTag Reason, AbilitySystemComponent Source)>();
            asc.OnBlocked += (reason, source) => calls.Add((reason, source));

            asc.ReportBlocked(silenced, caster);
            asc.ReportBlocked(silenced);
            asc.ReportBlocked(default, caster);

            Assert.That(calls.Count, Is.EqualTo(2), "dispara uma vez por tag válida e ignora a inválida");
            Assert.That(calls[0], Is.EqualTo((silenced, caster)));
            Assert.That(calls[1], Is.EqualTo((silenced, asc)), "origem nula vira o próprio ASC");
        }

        /// <summary>Prepara o ASC e devolve o efeito que, aplicado sem duração, dá <paramref name="result"/>.</summary>
        private GameplayEffect Arrange(AbilitySystemComponent asc, EffectApplicationResult result)
        {
            switch (result)
            {
                case EffectApplicationResult.Applied:
                    return _gas.CreateDurationEffect(Duration);
                case EffectApplicationResult.Stacked:
                {
                    GameplayEffect effect = CreateStackingEffect(maxStacks: 3);
                    asc.ApplyEffect(effect);
                    return effect;
                }
                case EffectApplicationResult.AppliedInstant:
                    return _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
                case EffectApplicationResult.BlockedByImmunity:
                {
                    GameplayTag immune = _gas.CreateTag("State.Immune");
                    GiveTag(asc, immune);
                    GameplayEffect effect = _gas.CreateDurationEffect(Duration);
                    effect.ApplicationImmunityTags.Add(immune);
                    return effect;
                }
                case EffectApplicationResult.MissingRequirements:
                {
                    GameplayEffect effect = _gas.CreateDurationEffect(Duration);
                    effect.ApplicationRequirements.Add(_gas.CreateTag("State.Wet"));
                    return effect;
                }
                case EffectApplicationResult.Invalid:
                    return null;
                case EffectApplicationResult.Refreshed:
                {
                    GameplayEffect effect = _gas.CreateDurationEffect(Duration);
                    asc.ApplyEffect(effect);
                    return effect;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "valor novo sem caso no teste");
            }
        }

        private static ActiveEffectInstance FindInstance(AbilitySystemComponent asc, GameplayEffect effect)
        {
            for (int i = 0; i < asc.ActiveEffects.Count; i++)
            {
                if (asc.ActiveEffects[i].Effect == effect) return asc.ActiveEffects[i];
            }
            return null;
        }

        private void GiveTag(AbilitySystemComponent asc, GameplayTag tag)
        {
            asc.ApplyEffect(_gas.CreateEffect(EffectDurationPolicy.Infinite, 0f, tag));
        }

        private GameplayEffect CreateStackingEffect(int maxStacks)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(Duration);
            effect.CanStack = true;
            effect.MaxStacks = maxStacks;
            return effect;
        }

        private GameplayEffect CreateInstantAdd(AttributeDefinition attribute, float value)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Constant, ConstantValue = value },
            });
            return effect;
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
