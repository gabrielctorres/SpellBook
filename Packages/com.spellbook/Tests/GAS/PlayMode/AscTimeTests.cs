using System.Collections;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Tempo do ASC: <c>Tick</c>, <c>autoTick</c> e fonte de tempo (CA7, CA8, CA9, CA11, CA46, N2 e N3; Spec 4.4).</summary>
    public class AscTimeTests
    {
        private const int Frames = 5;

        private GasFactory _gas;
        private TimeFactory _time;
        private float _timeScale;

        [SetUp]
        public void SetUp()
        {
            _timeScale = UnityEngine.Time.timeScale;
            UnityEngine.Time.timeScale = 1f;
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Time.timeScale = _timeScale;
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Effect2s_ActiveAfter1_9_RemovedAfter0_2()
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            GameplayEffect effect = _gas.CreateDurationEffect(2f, burning);
            AbilitySystemComponent asc = CreateManualAsc();
            asc.ApplyEffect(effect);

            asc.Tick(1.9f);
            bool activeAfter1_9 = asc.HasTag(burning);
            asc.Tick(0.2f);

            Assert.That(activeAfter1_9, Is.True, "o efeito expirou antes dos 2 s");
            Assert.That(asc.HasTag(burning), Is.False, "o efeito não expirou depois dos 2 s");
            Assert.That(asc.GetCooldownTimeRemaining(effect), Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator Cooldown3s_BlockedBefore_AllowedAt3()
        {
            // Dois arranjos independentes, nunca a soma 2,9 + 0,1 (Spec, risco 8).
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(3f);
            AbilitySystemComponent blocked = CreateManualAsc();
            AbilitySystemComponent allowed = CreateManualAsc();
            blocked.GiveAbility(ability);
            allowed.GiveAbility(ability);
            Assert.That(blocked.TryActivateAbility(ability, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(allowed.TryActivateAbility(ability, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            yield return null; // a habilidade sem ações termina; sem autoTick, o quadro não conta tempo

            blocked.Tick(2.9f);
            allowed.Tick(3.0f);

            Assert.That(blocked.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.OnCooldown));
            Assert.That(allowed.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.Success));
        }

        [UnityTest]
        public IEnumerator StoppedSource_AutoTick_NothingChangesOverNFrames()
        {
            GameplayTag burning = _gas.CreateTag("State.Burning");
            GameplayEffect effect = _gas.CreateDurationEffect(2f, burning);
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(3f);
            AbilitySystemComponent asc = _gas.CreateAsc(); // autoTick ligado (padrão)
            asc.SetTimeSource(_time.CreateManual());
            asc.GiveAbility(ability);
            asc.ApplyEffect(effect);
            asc.TryActivateAbility(ability, Vector3.zero);

            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(asc.TickedTime, Is.EqualTo(0d));
            Assert.That(asc.HasTag(burning), Is.True);
            Assert.That(asc.GetCooldownTimeRemaining(effect), Is.EqualTo(2f));
            Assert.That(asc.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(3f));
            Assert.That(asc.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.OnCooldown));
        }

        [Test]
        public void SameTicks_SameState()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            GameplayTag hasted = _gas.CreateTag("State.Hasted");
            GameplayEffect poison = _gas.CreateDurationEffect(2f);
            poison.Period = 0.5f;
            poison.PeriodicModifiers.Add(new AttributeEffectModifier
            {
                Attribute = health,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Constant, ConstantValue = -5f },
            });
            GameplayEffect haste = _gas.CreateDurationEffect(1.5f, hasted);
            float[] ticks = { 0.3f, 0.25f, 0.6f, 0.1f };

            var first = Run(health, poison, haste, hasted, ticks);
            var second = Run(health, poison, haste, hasted, ticks);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.Health, Is.EqualTo(90f), "o veneno deveria ter dado 2 ticks em 1,25 s");
        }

        [UnityTest]
        public IEnumerator PausableGroup_TwoAscsFrozen_UnscaledAscRuns()
        {
            PausableTimeSource group = _time.CreatePausable();
            GameplayEffect cooldown = _gas.CreateDurationEffect(100f);
            AbilitySystemComponent first = _gas.CreateAsc();
            AbilitySystemComponent second = _gas.CreateAsc();
            AbilitySystemComponent unscaled = _gas.CreateAsc();
            first.SetTimeSource(group);
            second.SetTimeSource(group);
            unscaled.SetTimeSource(_time.CreateUnscaled());
            first.ApplyEffect(cooldown);
            second.ApplyEffect(cooldown);
            unscaled.ApplyEffect(cooldown);
            yield return null;

            group.Pause();
            float firstFrozen = first.GetCooldownTimeRemaining(cooldown);
            float secondFrozen = second.GetCooldownTimeRemaining(cooldown);
            float unscaledBefore = unscaled.GetCooldownTimeRemaining(cooldown);
            for (int i = 0; i < Frames; i++)
            {
                yield return null;
            }

            Assert.That(first.GetCooldownTimeRemaining(cooldown), Is.EqualTo(firstFrozen), "o 1º ASC andou com o grupo pausado");
            Assert.That(second.GetCooldownTimeRemaining(cooldown), Is.EqualTo(secondFrozen), "o 2º ASC andou com o grupo pausado");
            Assert.That(unscaled.GetCooldownTimeRemaining(cooldown), Is.LessThan(unscaledBefore), "o ASC não escalado parou");

            group.Resume();
            yield return null;

            float delta = UnityEngine.Time.deltaTime;
            Assert.That(first.GetCooldownTimeRemaining(cooldown), Is.EqualTo(firstFrozen - delta).Within(1e-4f));
            Assert.That(second.GetCooldownTimeRemaining(cooldown), Is.EqualTo(secondFrozen - delta).Within(1e-4f));
        }

        [Test]
        public void Tick_InSteadyState_DoesNotAllocate()
        {
            // Sem tick periódico: o Recalculate do tick periódico aloca até o B-002 (Spec, D3).
            AbilitySystemComponent asc = CreateManualAsc();
            asc.ApplyEffect(_gas.CreateDurationEffect(100f, _gas.CreateTag("State.Shielded")));
            asc.ApplyEffect(_gas.CreateDurationEffect(50f));
            asc.Tick(0.016f); // aquecimento

            Assert.That(() => asc.Tick(0.016f), Is.Not.AllocatingGCMemory());
        }

        /// <summary>ASC com fonte manual e <c>autoTick</c> desligado: só avança por <c>Tick</c>.</summary>
        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private (float Health, bool Hasted, float PoisonRemaining, float HasteRemaining, double TickedTime) Run(
            AttributeDefinition health, GameplayEffect poison, GameplayEffect haste, GameplayTag hasted, float[] ticks)
        {
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            asc.ApplyEffect(poison);
            asc.ApplyEffect(haste);
            foreach (float tick in ticks)
            {
                asc.Tick(tick);
            }

            return (asc.GetAttributeValue(health), asc.HasTag(hasted), asc.GetCooldownTimeRemaining(poison),
                asc.GetCooldownTimeRemaining(haste), asc.TickedTime);
        }
    }
}
