using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Habilidade <see cref="AbilityBehavior.Toggle"/> (EV12: CA25, C17; CA27, parte de Toggle). Os ASCs têm
    /// <c>autoTick</c> desligado: só avançam por <c>Tick</c>.
    /// </summary>
    public class ToggleTests
    {
        public enum OffPath { CancelledByTag, CancelAbility, RemoveAbility }

        private GasFactory _gas;
        private TimeFactory _time;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Toggle_OnOff_CostOnlyOnOn_StateAndEvent()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayEffect aura = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            GameplayAbility ability = Toggle(new ApplyEffectAction { Effects = new List<GameplayEffect> { aura }, ApplyToSource = true });
            ability.OnCancel.Add(new RemoveEffectAction { Effect = aura, Target = new CasterSelector() });
            ability.CostEffect = Cost(mana, 10f);
            ability.CooldownEffect = _gas.CreateDurationEffect(5f);
            AbilitySystemComponent asc = CreateAsc((mana, 15f));
            AbilitySpec spec = asc.GiveAbility(ability);
            Subscribe(asc);
            int failures = 0;
            asc.OnAbilityFailed += (_, _) => failures++;

            Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
            asc.Tick(1f);

            Assert.That(asc.GetAttributeValue(mana), Is.EqualTo(5f));
            Assert.That(spec.IsToggledOn, Is.True);
            Assert.That(asc.FindEffects(aura, new List<ActiveEffectInstance>()), Is.EqualTo(1), "o efeito de ligar não ficou no dono");
            Assert.That(asc.GetActivationResult(ability), Is.EqualTo(AbilityActivationResult.Success), "a habilidade ligada não está acionável");
            Assert.That(_log, Is.EqualTo(new[] { "activated", "toggle(true)" }));
            _log.Clear();

            Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));

            Assert.That(asc.GetAttributeValue(mana), Is.EqualTo(5f), "desligar cobrou o custo");
            Assert.That(spec.IsToggledOn, Is.False);
            Assert.That(spec.IsActive, Is.False);
            Assert.That(asc.FindEffects(aura, new List<ActiveEffectInstance>()), Is.Zero, "'Ao ser cancelada' não removeu o efeito");
            Assert.That(_log, Is.EqualTo(new[] { "toggle(false)", "ended(cancelado)" }));
            Assert.That(failures, Is.Zero);
        }

        [TestCase(OffPath.CancelledByTag)]
        [TestCase(OffPath.CancelAbility)]
        [TestCase(OffPath.RemoveAbility)]
        public void Toggle_OffByCancelTagOrRemoval_RaisesToggleChanged(OffPath path)
        {
            GameplayTag stun = _gas.CreateTag("Stun");
            GameplayAbility ability = Toggle();
            ability.CancelledByTags.Add(stun);
            AbilitySystemComponent asc = CreateAsc();
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            Subscribe(asc);

            switch (path)
            {
                case OffPath.CancelledByTag: asc.AddTag(stun); break;
                case OffPath.CancelAbility: asc.CancelAbility(ability); break;
                case OffPath.RemoveAbility: asc.RemoveAbility(ability); break;
            }

            Assert.That(spec.IsToggledOn, Is.False);
            Assert.That(_log, Is.EqualTo(new[] { "toggle(false)", "ended(cancelado)" }));
        }

        [Test]
        public void Toggle_OwnedTags_GrantedAndReturned()
        {
            GameplayTag aura = _gas.CreateTag("State.Aura");
            GameplayAbility ability = Toggle();
            ability.ActivationOwnedTags.Add(aura);
            AbilitySystemComponent asc = CreateAsc();
            asc.AddTag(aura);
            asc.GiveAbility(ability);

            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1f);
            int whileOn = asc.GetTagCount(aura);
            asc.TryActivateAbility(ability, AbilityTarget.None);

            Assert.That(whileOn, Is.EqualTo(2), "ligada, a habilidade não concedeu a tag");
            Assert.That(asc.GetTagCount(aura), Is.EqualTo(1), "desligada, a contagem não voltou à de antes");
        }

        private void Subscribe(AbilitySystemComponent asc)
        {
            asc.OnAbilityActivated += _ => _log.Add("activated");
            asc.OnToggleChanged += (_, on) => _log.Add(on ? "toggle(true)" : "toggle(false)");
            asc.OnAbilityEnded += (_, cancelled) => _log.Add(cancelled ? "ended(cancelado)" : "ended");
        }

        private GameplayAbility Toggle(params GameplayAction[] actions)
        {
            GameplayAbility ability = _gas.CreateAbility(actions);
            ability.Behavior = AbilityBehavior.Toggle;
            return ability;
        }

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private GameplayEffect Cost(AttributeDefinition attribute, float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }
    }
}
