#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Controle de recarga por código e evento de recarga (B-003, EA5: CA17 a CA19 e CA22).</summary>
    public class CooldownControlTests
    {
        private const float Cooldown = 8f;

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
        public void Reduce3_From8_Is5()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = GiveAndActivate(asc, _gas.CreateAbilityWithCooldown(Cooldown));

            bool changed = asc.ReduceCooldown(ability, 3f);

            Assert.That(changed, Is.True);
            Assert.That(asc.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(5f).Within(1e-4f));
        }

        [UnityTest]
        public IEnumerator Reset_IsZero_AndActivatable()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = GiveAndActivate(asc, _gas.CreateAbilityWithCooldown(Cooldown));
            // A habilidade sem ações termina no quadro seguinte; só a recarga deve sobrar
            yield return null;
            Assert.That(asc.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.OnCooldown));

            bool changed = asc.ResetCooldown(ability);

            Assert.That(changed, Is.True);
            Assert.That(asc.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(0f));
            Assert.That(asc.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.Success));
        }

        [Test]
        public void Restart_BackToAppliedFull()
        {
            // Com redução de 0,25, a duração aplicada (6 s) difere da do asset (8 s)
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = CreateManualAsc((cdr, 0.25f));
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(Cooldown);
            ability.CooldownReductionAttribute = cdr;
            GiveAndActivate(asc, ability);
            asc.Tick(2f);

            bool changed = asc.RestartCooldown(ability);

            Assert.That(changed, Is.True);
            Assert.That(asc.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(6f).Within(1e-4f), "não voltou à duração aplicada");
        }

        [Test]
        public void ControlsOnReadyAbility_ReturnFalse_NoException()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ready = _gas.CreateAbilityWithCooldown(Cooldown);
            asc.GiveAbility(ready);
            GameplayAbility noCooldown = _gas.CreateAbility();
            var results = new List<bool>();

            Assert.DoesNotThrow(() =>
            {
                foreach (GameplayAbility ability in new[] { ready, noCooldown, null })
                {
                    results.Add(asc.ReduceCooldown(ability, 3f));
                    results.Add(asc.ResetCooldown(ability));
                    results.Add(asc.RestartCooldown(ability));
                }
            });
            Assert.That(results, Has.Count.EqualTo(9).And.All.False);
        }

        [Test]
        public void Controls_RespectChildTagCooldown()
        {
            GameplayTag cooldownTag = _gas.CreateTag("Cooldown");
            GameplayTag fire = _gas.CreateTag("Cooldown.Fire", cooldownTag);
            GameplayTag unrelated = _gas.CreateTag("State.Hasted");
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = _gas.CreateAbility();
            ability.CooldownEffect = _gas.CreateDurationEffect(Cooldown, cooldownTag);
            asc.GiveAbility(ability);
            asc.ApplyEffect(_gas.CreateDurationEffect(Cooldown, fire));
            ActiveEffectInstance other = asc.ApplyEffect(_gas.CreateDurationEffect(Cooldown, unrelated));

            bool reduced = asc.ReduceCooldown(ability, 3f);
            float afterReduce = asc.GetCooldownTimeRemaining(ability.CooldownEffect);
            asc.Tick(1f);
            bool restarted = asc.RestartCooldown(ability);
            float afterRestart = asc.GetCooldownTimeRemaining(ability.CooldownEffect);
            bool reset = asc.ResetCooldown(ability);

            Assert.That(new[] { reduced, restarted, reset }, Is.All.True);
            Assert.That(afterReduce, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(afterRestart, Is.EqualTo(Cooldown).Within(1e-4f));
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.False);
            Assert.That(other.IsActive, Is.True, "o controle mexeu num efeito sem parentesco com a recarga");
            Assert.That(other.TimeRemaining, Is.EqualTo(Cooldown - 1f).Within(1e-4f));
        }

        [Test]
        public void CooldownChanged_StartedOnActivate_EndedOnExpiry()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(Cooldown);
            AbilitySpec spec = asc.GiveAbility(ability);
            List<(AbilitySpec, CooldownChange)> changes = RecordChanges(asc);

            GiveAndActivate(asc, ability);
            var afterActivate = new List<(AbilitySpec, CooldownChange)>(changes);
            asc.Tick(Cooldown);

            Assert.That(afterActivate, Is.EqualTo(new[] { (spec, CooldownChange.Started) }));
            Assert.That(changes, Is.EqualTo(new[] { (spec, CooldownChange.Started), (spec, CooldownChange.Ended) }));
        }

        [Test]
        public void CooldownChanged_ChangedOnReduceAndRestart()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(Cooldown);
            AbilitySpec spec = asc.GiveAbility(ability);
            List<(AbilitySpec, CooldownChange)> changes = RecordChanges(asc);
            GiveAndActivate(asc, ability);

            asc.ReduceCooldown(ability, 3f);
            asc.RestartCooldown(ability);

            Assert.That(changes, Is.EqualTo(new[]
            {
                (spec, CooldownChange.Started), (spec, CooldownChange.Changed), (spec, CooldownChange.Changed)
            }));
        }

        [Test]
        public void CooldownChanged_ReducePastZero_EndedNotChanged()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(Cooldown);
            AbilitySpec spec = asc.GiveAbility(ability);
            List<(AbilitySpec, CooldownChange)> changes = RecordChanges(asc);
            GiveAndActivate(asc, ability);

            bool changed = asc.ReduceCooldown(ability, Cooldown + 1f);

            Assert.That(changed, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { (spec, CooldownChange.Started), (spec, CooldownChange.Ended) }));
        }

        [Test]
        public void CooldownChanged_EndedOnReset()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(Cooldown);
            AbilitySpec spec = asc.GiveAbility(ability);
            List<(AbilitySpec, CooldownChange)> changes = RecordChanges(asc);
            GiveAndActivate(asc, ability);

            asc.ResetCooldown(ability);
            asc.Tick(Cooldown);

            Assert.That(changes, Is.EqualTo(new[] { (spec, CooldownChange.Started), (spec, CooldownChange.Ended) }));
        }

        private static GameplayAbility GiveAndActivate(AbilitySystemComponent asc, GameplayAbility ability)
        {
            asc.GiveAbility(ability);
            Assert.That(asc.TryActivateAbility(ability, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            return ability;
        }

        private static List<(AbilitySpec, CooldownChange)> RecordChanges(AbilitySystemComponent asc)
        {
            var changes = new List<(AbilitySpec, CooldownChange)>();
            asc.OnCooldownChanged += (spec, change) => changes.Add((spec, change));
            return changes;
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
