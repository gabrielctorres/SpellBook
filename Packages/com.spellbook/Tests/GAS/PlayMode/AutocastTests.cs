#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Abilities.Triggers;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Autocast, gatilho "quando pronta" e passiva que se reativa (EV14: CA37 a CA40, CA42; cenários C23 a C26).
    /// Os ASCs têm <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class AutocastTests
    {
        public enum CheckPosition { Tags, Charges, Cooldown, Cost, Condition }

        // Longe da origem, para não cruzar colliders de outros testes
        private static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);

        private GasFactory _gas;
        private TimeFactory _time;
        private AttributeDefinition _mana;
        private readonly List<GameObject> _units = new List<GameObject>();

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
            foreach (GameObject unit in _units) Object.DestroyImmediate(unit);
            _units.Clear();
            Physics.SyncTransforms();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void AutocastOff_Nothing_On_ActivatesWithSelectorTarget_OffStops()
        {
            AbilitySystemComponent caster = CreateAsc();
            AbilitySystemComponent enemy = CreateUnit(Origin + new Vector3(2f, 0f, 0f));
            var count = new CountAction();
            GameplayAbility ability = _gas.CreateAbility(count);
            ability.Autocast.Allowed = true;
            ability.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true, Selector = new AreaSelector() });
            AbilitySpec spec = caster.GiveAbility(ability);

            for (int i = 0; i < 3; i++) caster.Tick(1f);
            int whileOff = count.Count;
            bool turnedOn = caster.SetAutocast(spec, true);
            caster.Tick(1f);
            int afterOn = count.Count;
            caster.SetAutocast(spec, false);
            for (int i = 0; i < 3; i++) caster.Tick(1f);

            Assert.That(whileOff, Is.Zero, "com o autocast desligado, nada é ativado sozinho");
            Assert.That(turnedOn, Is.True);
            Assert.That(afterOn, Is.EqualTo(1), "ligado, o Tick seguinte ativa a habilidade");
            Assert.That(count.FirstTargets, Is.EqualTo(new[] { enemy }), "a ativação usa o alvo do seletor");
            Assert.That(count.Count, Is.EqualTo(1), "desligado de novo, para");
        }

        [Test]
        public void OnlyWithAutocast_IgnoredWhenOff_ManualWorksBoth()
        {
            GameplayTag hit = _gas.CreateTag("Evento.Acerto");
            AbilitySystemComponent asc = CreateAsc();
            var markedCount = new CountAction();
            GameplayAbility marked = _gas.CreateAbility(markedCount);
            marked.Autocast.Allowed = true;
            marked.Triggers.Add(new GameplayEventTrigger { Tag = hit, OnlyWithAutocast = true });
            var plainCount = new CountAction();
            GameplayAbility plain = _gas.CreateAbility(plainCount);
            plain.Triggers.Add(new GameplayEventTrigger { Tag = hit });
            AbilitySpec spec = asc.GiveAbility(marked);
            asc.GiveAbility(plain);

            asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 0f));
            Assert.That(markedCount.Count, Is.Zero, "o gatilho marcado é ignorado com o autocast desligado");
            Assert.That(plainCount.Count, Is.EqualTo(1));
            Assert.That(asc.TryActivateAbility(marked, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(markedCount.Count, Is.EqualTo(1), "a ativação manual funciona com o autocast desligado");

            asc.SetAutocast(spec, true);
            asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 0f));
            Assert.That(markedCount.Count, Is.EqualTo(2), "o gatilho marcado dispara com o autocast ligado");
            Assert.That(plainCount.Count, Is.EqualTo(2), "o gatilho sem a marca dispara nos dois estados");
            Assert.That(asc.TryActivateAbility(marked, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(markedCount.Count, Is.EqualTo(3), "a ativação manual funciona com o autocast ligado");
        }

        [Test]
        public void FailureSilent_NoTargetSkips_OnePerTick()
        {
            GameplayTag hit = _gas.CreateTag("Evento.Acerto");
            AbilitySystemComponent asc = CreateAsc((_mana, 100f));
            var failures = new List<AbilityActivationResult>();
            asc.OnAbilityFailed += (_, failure) => failures.Add(failure.Result);

            // Em recarga: nem a passada nem o gatilho por evento marcado avisam a falha; a ativação manual avisa
            var count = new CountAction();
            GameplayAbility cooling = _gas.CreateAbilityWithCooldown(5f);
            cooling.OnActivate.Add(count);
            cooling.Autocast.Allowed = true;
            cooling.Autocast.StartsOn = true;
            cooling.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true });
            cooling.Triggers.Add(new GameplayEventTrigger { Tag = hit, OnlyWithAutocast = true });
            asc.GiveAbility(cooling);
            asc.Tick(1f);
            for (int i = 0; i < 3; i++) asc.Tick(1f);
            asc.SendGameplayEvent(hit, new GameplayEventData(asc, null, 0f));
            Assert.That(count.Count, Is.EqualTo(1), "a primeira passada ativa; as seguintes caem na recarga");
            Assert.That(failures, Is.Empty, "a tentativa de autocast que falha é silenciosa");
            asc.TryActivateAbility(cooling, AbilityTarget.None);
            Assert.That(failures, Is.EqualTo(new[] { AbilityActivationResult.OnCooldown }), "a ativação manual avisa a falha");

            // Seletor sem alvo: pula a tentativa, sem custo, sem evento e sem exceção
            failures.Clear();
            var noTargetCount = new CountAction();
            GameplayAbility noTarget = _gas.CreateAbility(noTargetCount);
            noTarget.CostEffect = Cost(10f);
            noTarget.Triggers.Add(new WhenReadyTrigger { Selector = new AreaSelector() });
            asc.GiveAbility(noTarget);
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 3; i++) asc.Tick(1f);
            });
            Assert.That(noTargetCount.Count, Is.Zero);
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(100f), "a tentativa pulada não cobra");
            Assert.That(failures, Is.Empty);

            // Dois gatilhos "quando pronta" no mesmo spec: uma tentativa por Tick
            var probe = new CountingCondition { Result = false };
            GameplayAbility twice = _gas.CreateAbility();
            twice.ActivationConditions.Add(new ActivationRule(probe));
            twice.Triggers.Add(new WhenReadyTrigger());
            twice.Triggers.Add(new WhenReadyTrigger());
            asc.GiveAbility(twice);
            asc.Tick(1f);
            asc.Tick(1f);
            Assert.That(probe.Count, Is.EqualTo(2), "no máximo uma tentativa por habilidade por Tick");
        }

        [Test]
        public void Autocast_CheckOrder_SameAsManual(
            [Values] CheckPosition position)
        {
            GameplayTag stunned = _gas.CreateTag("Stunned");
            AbilitySystemComponent asc = CreateAsc((_mana, 100f));
            var failures = 0;
            asc.OnAbilityFailed += (_, _) => failures++;
            var count = new CountAction();
            GameplayAbility ability = _gas.CreateAbility(count);
            var probe = new CountingCondition { Result = position != CheckPosition.Condition };
            ability.ActivationConditions.Add(new ActivationRule(probe));
            ability.CostEffect = Cost(position == CheckPosition.Cost ? 200f : 10f);
            ability.BlockedTags.Add(stunned);
            if (position == CheckPosition.Charges)
            {
                ability.Charges.Enabled = true;
                ability.Charges.RechargeTime = ScalableFloat.Constant(2f);
            }
            if (position == CheckPosition.Cooldown) ability.CooldownEffect = _gas.CreateDurationEffect(2f);
            ability.Autocast.Allowed = true;
            ability.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true });
            AbilitySpec spec = asc.GiveAbility(ability);
            // A carga e a recarga se gastam numa ativação manual; tags, custo e condição já falham
            if (position == CheckPosition.Charges || position == CheckPosition.Cooldown)
            {
                asc.TryActivateAbility(ability, AbilityTarget.None);
            }
            if (position == CheckPosition.Tags) asc.AddTag(stunned);
            asc.SetAutocast(spec, true);
            int before = count.Count;
            probe.Count = 0;

            asc.Tick(1f);

            Assert.That(count.Count, Is.EqualTo(before), "a tentativa com a checagem falhando não ativa");
            Assert.That(failures, Is.Zero, "a falha do autocast é silenciosa");
            Assert.That(probe.Count, Is.EqualTo(position == CheckPosition.Condition ? 1 : 0),
                "as checagens anteriores param a tentativa antes da regra de ativação");
            Assert.That(asc.GetActivationResult(ability, AbilityTarget.None, out _), Is.EqualTo(Expected(position)),
                "a tentativa usa a mesma checagem da ativação manual");

            if (position == CheckPosition.Tags) asc.RemoveTag(stunned);
            if (position == CheckPosition.Cost) ability.CostEffect = Cost(10f);
            if (position == CheckPosition.Condition) probe.Result = true;
            asc.Tick(2f);

            Assert.That(count.Count, Is.EqualTo(before + 1), "sem a falha, o Tick seguinte ativa");
        }

        [Test]
        public void SetAutocast_FalseWithoutAllowedOrNoChange_EventOnce_StartsOn()
        {
            AbilitySystemComponent asc = CreateAsc();
            var events = new List<(AbilitySpec, bool)>();
            asc.OnAutocastChanged += (spec, on) => events.Add((spec, on));

            AbilitySpec notAllowed = asc.GiveAbility(_gas.CreateAbility());
            GameplayAbility toggle = _gas.CreateAbility();
            toggle.Behavior = AbilityBehavior.Toggle;
            toggle.Autocast.Allowed = true;
            AbilitySpec toggleSpec = asc.GiveAbility(toggle);
            GameplayAbility allowed = _gas.CreateAbility();
            allowed.Autocast.Allowed = true;
            AbilitySpec spec = asc.GiveAbility(allowed);

            Assert.That(asc.SetAutocast(notAllowed, true), Is.False, "sem Allowed");
            Assert.That(asc.SetAutocast(toggleSpec, true), Is.False, "em Toggle");
            Assert.That(asc.SetAutocast(null, true), Is.False, "spec nulo");
            Assert.That(asc.SetAutocast(spec, false), Is.False, "sem mudança");
            Assert.That(asc.SetAutocast(spec, true), Is.True);
            Assert.That(asc.SetAutocast(spec, true), Is.False, "sem mudança");
            Assert.That(asc.IsAutocastEnabled(spec), Is.True);
            Assert.That(asc.SetAutocast(spec, false), Is.True);
            Assert.That(asc.IsAutocastEnabled(spec), Is.False);
            Assert.That(asc.IsAutocastEnabled(notAllowed), Is.False);
            Assert.That(events, Is.EqualTo(new[] { (spec, true), (spec, false) }), "um evento por mudança real");

            // StartsOn liga na concessão, sem evento; em Passive e Toggle, não liga
            events.Clear();
            GameplayAbility startsOn = _gas.CreateAbility();
            startsOn.Autocast.Allowed = true;
            startsOn.Autocast.StartsOn = true;
            GameplayAbility passive = _gas.CreateAbility();
            passive.Behavior = AbilityBehavior.Passive;
            passive.Autocast.Allowed = true;
            passive.Autocast.StartsOn = true;

            Assert.That(asc.IsAutocastEnabled(asc.GiveAbility(startsOn)), Is.True);
            Assert.That(asc.IsAutocastEnabled(asc.GiveAbility(passive)), Is.False);
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void Autocast_ManualCommitAndCharges_ChargeLikeManual()
        {
            AbilitySystemComponent auto = CreateAsc((_mana, 100f));
            AbilitySystemComponent manual = CreateAsc((_mana, 100f));
            GameplayAbility autoAbility = ManualCommitWithCharges();
            autoAbility.Autocast.Allowed = true;
            autoAbility.Autocast.StartsOn = true;
            autoAbility.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true });
            AbilitySpec autoSpec = auto.GiveAbility(autoAbility);
            AbilitySpec manualSpec = manual.GiveAbility(ManualCommitWithCharges());

            auto.Tick(0.5f);
            manual.TryActivateAbility(manualSpec, AbilityTarget.None);
            manual.Tick(0.5f);

            Assert.That(autoSpec.IsActive, Is.True, "a passada ativou");
            Assert.That(auto.GetCharges(autoSpec), Is.EqualTo(manual.GetCharges(manualSpec)), "antes do commit");
            Assert.That(auto.GetCharges(autoSpec), Is.EqualTo(2), "com Manual, a ativação não cobra");
            Assert.That(auto.GetAttributeValue(_mana), Is.EqualTo(100f));

            auto.Tick(1f);
            manual.Tick(1f);

            Assert.That(auto.GetCharges(autoSpec), Is.EqualTo(1), "a CommitAction gasta uma carga");
            Assert.That(auto.GetAttributeValue(_mana), Is.EqualTo(90f), "a CommitAction cobra o custo uma vez");
            Assert.That(auto.GetCharges(autoSpec), Is.EqualTo(manual.GetCharges(manualSpec)), "igual ao uso manual");
            Assert.That(auto.GetAttributeValue(_mana), Is.EqualTo(manual.GetAttributeValue(_mana)), "igual ao uso manual");
        }

        [Test]
        public void NoAutocastNoTrigger_NoPerTickWork_NoAlloc()
        {
            AbilitySystemComponent asc = CreateAsc();
            var probe = new CountingCondition { Result = true };
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(probe));
            asc.GiveAbility(ability);

            for (int i = 0; i < 100; i++) asc.Tick(0.1f);
            Assert.That(probe.Count, Is.Zero, "sem autocast e sem gatilho, nenhuma tentativa por Tick");
            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) asc.Tick(0.01f);
            }, Is.Not.AllocatingGCMemory());

            // Com a passada e o seletor de área, a tentativa que cai na recarga também não aloca depois do aquecimento
            AbilitySystemComponent caster = CreateAsc();
            CreateUnit(Origin + new Vector3(2f, 0f, 0f));
            var count = new CountAction();
            GameplayAbility cooling = _gas.CreateAbilityWithCooldown(1000f);
            cooling.OnActivate.Add(count);
            cooling.Autocast.Allowed = true;
            cooling.Autocast.StartsOn = true;
            cooling.Triggers.Add(new WhenReadyTrigger { OnlyWithAutocast = true, Selector = new AreaSelector() });
            caster.GiveAbility(cooling);
            for (int i = 0; i < 3; i++) caster.Tick(0.1f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) caster.Tick(0.01f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(count.Count, Is.EqualTo(1));
        }

        [Test]
        public void Passive_Reactivates_WhenConfigured()
        {
            GameplayTag ready = _gas.CreateTag("Ready");
            AbilitySystemComponent asc = CreateAsc();
            var failures = 0;
            asc.OnAbilityFailed += (_, _) => failures++;
            GameplayAbility reactivating = Passive(ready);
            reactivating.ReactivatePassive = true;
            AbilitySpec spec = asc.GiveAbility(reactivating);

            asc.Tick(1f);
            bool activeWithoutTag = spec.IsActive;
            asc.AddTag(ready);
            asc.Tick(1f);
            bool activeWithTag = spec.IsActive;
            asc.CancelAbility(spec);
            bool activeAfterCancel = spec.IsActive;
            asc.Tick(1f);

            Assert.That(activeWithoutTag, Is.False);
            Assert.That(activeWithTag, Is.True, "com a tag, o Tick seguinte reativa");
            Assert.That(activeAfterCancel, Is.False);
            Assert.That(spec.IsActive, Is.True, "cancelada, volta no Tick seguinte");
            Assert.That(failures, Is.Zero, "a reativação é silenciosa");

            // Sem ReactivatePassive, a cancelada fica inativa
            AbilitySpec once = asc.GiveAbility(Passive(ready));
            asc.CancelAbility(once);
            for (int i = 0; i < 3; i++) asc.Tick(1f);
            Assert.That(once.IsActive, Is.False);
        }

        private static AbilityActivationResult Expected(CheckPosition position) => position switch
        {
            CheckPosition.Tags => AbilityActivationResult.BlockedByTags,
            CheckPosition.Charges => AbilityActivationResult.NoCharges,
            CheckPosition.Cooldown => AbilityActivationResult.OnCooldown,
            CheckPosition.Cost => AbilityActivationResult.CannotAffordCost,
            _ => AbilityActivationResult.BlockedByCondition,
        };

        private GameplayAbility Passive(GameplayTag required)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = AbilityBehavior.Passive;
            ability.RequiredTags.Add(required);
            return ability;
        }

        // Manual, duas cargas que voltam em 10 s e custo de 10 de mana; a CommitAction roda depois de 1 s
        private GameplayAbility ManualCommitWithCharges()
        {
            GameplayAbility ability = _gas.CreateAbility(
                new WaitAction { Duration = ScalableFloat.Constant(1f) }, new CommitAction());
            ability.CommitMode = CommitMode.Manual;
            ability.CostEffect = Cost(10f);
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(2f) };
            ability.Charges.RechargeTime = ScalableFloat.Constant(10f);
            return ability;
        }

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

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition, float)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            asc.transform.position = Origin;
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private AbilitySystemComponent CreateUnit(Vector3 position)
        {
            AbilitySystemComponent unit = CreateAsc();
            unit.transform.position = position;
            unit.gameObject.AddComponent<SphereCollider>().radius = 0.25f;
            _units.Add(unit.gameObject);
            Physics.SyncTransforms();
            return unit;
        }

        private sealed class CountAction : GameplayAction
        {
            public int Count;
            public AbilitySystemComponent[] FirstTargets = Array.Empty<AbilitySystemComponent>();

            public override bool UsesTargets => false;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (Count == 0) FirstTargets = context.EventTargets.ToArray();
                Count++;
                return ActionStatus.Done;
            }
        }

        private sealed class CountingCondition : Condition<ConditionContext>
        {
            public int Count;
            public bool Result;

            public override bool Evaluate(in ConditionContext context)
            {
                Count++;
                return Result;
            }
        }
    }
}
#endif
