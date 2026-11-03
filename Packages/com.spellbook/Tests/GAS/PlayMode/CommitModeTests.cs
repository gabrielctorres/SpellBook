#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Commit no início ou pela <see cref="CommitAction"/> (EV9: CA29, CA30; cargas do B-009).</summary>
    public class CommitModeTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private AttributeDefinition _mana;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _mana = _gas.CreateAttribute("Mana");
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Manual_NoChargeBeforeCommit_CancelBeforeFree_CommitTwiceOnce_FailCancelsAndReports()
        {
            AbilitySystemComponent asc = CreateManualAsc(50f);
            GameplayAbility ability = CreateManualAbility(1f);
            ability.OnActivate.Add(new CommitAction());
            ability.OnActivate.Add(Wait(5f));
            AbilitySpec spec = asc.GiveAbility(ability);

            // Antes da CommitAction, nada é cobrado; cancelar aí não cobra
            Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(50f));
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.False);
            Assert.That(spec.IsCommitted, Is.False);
            asc.CancelAbility(spec);
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(50f), "cancelar antes do commit não cobra custo");
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.False, "cancelar antes do commit não cobra recarga");

            // Duas CommitAction na mesma ativação cobram uma vez
            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f);
            Assert.That(spec.IsActive, Is.True);
            Assert.That(spec.IsCommitted, Is.True);
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(40f), "o commit duplo cobra uma vez");
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.True);

            AssertCommitFailsOnCost_CancelsAndReports();
        }

        // Falha de custo no commit: cancela a habilidade, informa o motivo e não roda as ações seguintes
        private void AssertCommitFailsOnCost_CancelsAndReports()
        {
            AbilitySystemComponent asc = CreateManualAsc(15f);
            GameplayAbility first = CreateManualAbility(0.5f);
            first.OnActivate.Add(Wait(5f));
            GameplayAbility second = CreateManualAbility(1f);
            var after = new CountAction();
            second.OnActivate.Add(after);
            AbilitySpec firstSpec = asc.GiveAbility(first);
            AbilitySpec secondSpec = asc.GiveAbility(second);
            var failures = new List<(GameplayAbility, AbilityActivationResult)>();
            var ended = new List<(AbilitySpec, bool)>();
            asc.OnAbilityFailed += (failed, failure) => failures.Add((failed, failure.Result));
            asc.OnAbilityEnded += (endedSpec, cancelled) => ended.Add((endedSpec, cancelled));

            // As duas passam na checagem com 15 de mana; a primeira cobra 10 antes de a segunda chegar ao commit
            AbilityActivationResult firstResult = asc.TryActivateAbility(first, AbilityTarget.None);
            AbilityActivationResult secondResult = asc.TryActivateAbility(second, AbilityTarget.None);
            asc.Tick(0.5f); // a primeira cobra num Tick e a segunda no seguinte
            asc.Tick(0.5f);

            Assert.That(firstResult, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(secondResult, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(firstSpec.IsActive, Is.True);
            Assert.That(firstSpec.IsCommitted, Is.True);
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(5f));
            Assert.That(secondSpec.IsActive, Is.False, "a falha no commit cancela a habilidade");
            Assert.That(secondSpec.IsCommitted, Is.False);
            Assert.That(after.Count, Is.EqualTo(0), "as ações depois do commit não rodam");
            Assert.That(ended, Is.EqualTo(new[] { (secondSpec, true) }));
            Assert.That(failures, Is.EqualTo(new[] { (second, AbilityActivationResult.CannotAffordCost) }));
        }

        [Test]
        public void OnStart_ChargesBeforeAnyAction()
        {
            AbilitySystemComponent asc = CreateManualAsc(50f);
            var capture = new CaptureAction(asc, _mana);
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
            ability.CostEffect = Cost(10f);
            ability.OnActivate.Add(capture);
            ability.OnActivate.Add(new CommitAction());
            AbilitySpec spec = asc.GiveAbility(ability);

            asc.TryActivateAbility(ability, AbilityTarget.None);

            Assert.That(ability.CommitMode, Is.EqualTo(CommitMode.OnStart), "OnStart é o padrão");
            Assert.That(capture.Mana, Is.EqualTo(40f), "o custo sai antes da primeira ação");
            Assert.That(capture.OnCooldown, Is.True, "a recarga sai antes da primeira ação");
            Assert.That(capture.Committed, Is.True);
            Assert.That(spec.IsCommitted, Is.True);
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(40f), "a CommitAction com OnStart não cobra de novo");
        }

        [Test]
        public void Manual_SpendsChargeAtCommit()
        {
            AbilitySystemComponent asc = CreateManualAsc(50f);
            GameplayAbility ability = CreateManualAbility(1f);
            ability.OnActivate.Add(Wait(5f));
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.Constant(2f)
            };
            ability.Charges.RechargeTime = ScalableFloat.Constant(10f);
            AbilitySpec spec = asc.GiveAbility(ability);

            asc.TryActivateAbility(ability, AbilityTarget.None);
            int beforeCommit = asc.GetCharges(spec);
            asc.CancelAbility(spec);
            int afterCancel = asc.GetCharges(spec);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f);

            Assert.That(beforeCommit, Is.EqualTo(2), "a carga não sai na ativação");
            Assert.That(afterCancel, Is.EqualTo(2), "cancelar antes do commit não gasta carga");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(1), "a carga sai no commit");
        }

        // Manual, com custo de 10 de mana e recarga de 5 s; espera commitDelay segundos e roda a CommitAction
        private GameplayAbility CreateManualAbility(float commitDelay)
        {
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(5f);
            ability.CostEffect = Cost(10f);
            ability.CommitMode = CommitMode.Manual;
            ability.OnActivate.Add(Wait(commitDelay));
            ability.OnActivate.Add(new CommitAction());
            return ability;
        }

        private static WaitAction Wait(float seconds) => new WaitAction { Duration = ScalableFloat.Constant(seconds) };

        private GameplayEffect Cost(float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        private AbilitySystemComponent CreateManualAsc(float mana)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, mana));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private sealed class CountAction : GameplayAction
        {
            public int Count;

            public override bool UsesTargets => false;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Count++;
                return ActionStatus.Done;
            }
        }

        // Lê, na primeira ação, o estado que o commit do início já deveria ter deixado
        private sealed class CaptureAction : GameplayAction
        {
            private readonly AbilitySystemComponent _asc;
            private readonly AttributeDefinition _attribute;
            public float Mana;
            public bool OnCooldown;
            public bool Committed;

            public CaptureAction(AbilitySystemComponent asc, AttributeDefinition attribute)
            {
                _asc = asc;
                _attribute = attribute;
            }

            public override bool UsesTargets => false;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Mana = _asc.GetAttributeValue(_attribute);
                OnCooldown = _asc.IsOnCooldown(context.Ability.CooldownEffect);
                Committed = context.Spec.IsCommitted;
                return ActionStatus.Done;
            }
        }
    }
}
#endif
