#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>Máximo variável de cargas e controle da recarga de carga (B-009, AV7: CA15, CA20, R18, P6).</summary>
    public class ChargesMaxAndControlTests
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
        public void MaxByTableName_Level1Is2_Level3Is4()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateTableMaxAbility(5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            int maxAtLevel1 = asc.GetMaxCharges(spec);

            asc.SetAbilityLevel(ability, 3);

            Assert.That(maxAtLevel1, Is.EqualTo(2));
            Assert.That(asc.GetMaxCharges(spec), Is.EqualTo(4), "o máximo não acompanhou a mudança de nível");
        }

        [Test]
        public void MaxByAttribute_Follows()
        {
            AttributeDefinition maxDef = _gas.CreateAttribute("MaxCharges");
            AbilitySystemComponent asc = CreateManualAsc((maxDef, 2f));
            GameplayAbility ability = CreateAttributeMaxAbility(maxDef, 5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            int maxAtGrant = asc.GetMaxCharges(spec);

            SetAttribute(asc, maxDef, 3f);
            asc.Tick(0.01f);
            int maxAfterRaise = asc.GetMaxCharges(spec);
            SetAttribute(asc, maxDef, 1f);
            asc.Tick(0.01f);

            Assert.That(maxAtGrant, Is.EqualTo(2));
            Assert.That(maxAfterRaise, Is.EqualTo(3), "o máximo não seguiu o atributo no Tick");
            Assert.That(asc.GetMaxCharges(spec), Is.EqualTo(1));
            Assert.That(asc.GetCharges(spec), Is.EqualTo(1));
        }

        [Test]
        public void MaxUp_DoesNotFill_RechargeStarts()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateTableMaxAbility(5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            var changes = new List<(int Before, int After)>();
            asc.OnChargesChanged += (s, before, after) => changes.Add((before, after));

            asc.SetAbilityLevel(ability, 3);
            int chargesAfterLevelUp = asc.GetCharges(spec);
            float remainingAfterLevelUp = asc.GetChargeRechargeRemaining(spec);
            asc.Tick(5f);

            Assert.That(chargesAfterLevelUp, Is.EqualTo(2), "o máximo que sobe não enche as cargas");
            Assert.That(remainingAfterLevelUp, Is.EqualTo(5f).Within(1e-5f), "abaixo do novo máximo, a recarga não começou");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(new[] { (2, 3) }), "subir o máximo disparou OnChargesChanged");
        }

        [Test]
        public void MaxDown_Clamps_StopsRechargeAtMax()
        {
            AttributeDefinition maxDef = _gas.CreateAttribute("MaxCharges");
            AbilitySystemComponent asc = CreateManualAsc((maxDef, 4f));
            GameplayAbility ability = CreateAttributeMaxAbility(maxDef, 5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            var changes = new List<(int Before, int After)>();
            asc.OnChargesChanged += (s, before, after) => changes.Add((before, after));

            SetAttribute(asc, maxDef, 2f);
            asc.Tick(0.01f);

            Assert.That(spec.Charges, Is.EqualTo(2), "as cargas não foram limitadas ao máximo novo");
            Assert.That(asc.GetMaxCharges(spec), Is.EqualTo(2));
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(0f), "no máximo, a recarga para");
            Assert.That(changes, Is.EqualTo(new[] { (3, 2) }));
        }

        [UnityTest]
        public IEnumerator Reduce_ShortensRecharge_CanRestoreSeveral()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 2f);
            AbilitySpec spec = asc.GiveAbility(ability);
            yield return Spend(asc, ability, 3);

            bool shortened = asc.ReduceCooldown(ability, 1f);
            float remainingAfterShorten = asc.GetChargeRechargeRemaining(spec);
            bool restored = asc.ReduceCooldown(ability, 3.5f);

            Assert.That(shortened, Is.True);
            Assert.That(remainingAfterShorten, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(restored, Is.True);
            Assert.That(asc.GetCharges(spec), Is.EqualTo(2), "a redução grande não devolveu duas cargas");
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(1.5f).Within(1e-5f), "o excesso não passou adiante");
        }

        [UnityTest]
        public IEnumerator Reset_ReturnsOneCharge_RestartsNext()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 4f);
            AbilitySpec spec = asc.GiveAbility(ability);
            yield return Spend(asc, ability, 2);
            asc.Tick(1f);
            var changes = new List<(int Before, int After)>();
            asc.OnChargesChanged += (s, before, after) => changes.Add((before, after));

            bool first = asc.ResetCooldown(ability);
            float remainingAfterFirst = asc.GetChargeRechargeRemaining(spec);
            bool second = asc.ResetCooldown(ability);
            bool atMax = asc.ResetCooldown(ability);

            Assert.That(first, Is.True);
            Assert.That(remainingAfterFirst, Is.EqualTo(4f).Within(1e-5f), "a próxima recarga não recomeçou cheia");
            Assert.That(second, Is.True);
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3));
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(0f), "no máximo, a recarga para");
            Assert.That(atMax, Is.False, "sem recarga correndo, nada muda");
            Assert.That(changes, Is.EqualTo(new[] { (1, 2), (2, 3) }));
        }

        [Test]
        public void Restart_BackToFullAppliedDuration()
        {
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = CreateManualAsc((cdr, 0.5f));
            GameplayAbility ability = CreateChargedAbility(2, 4f);
            ability.CooldownReductionAttribute = cdr;
            AbilitySpec spec = asc.GiveAbility(ability);
            bool stopped = asc.RestartCooldown(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(1.5f);

            bool restarted = asc.RestartCooldown(ability);

            Assert.That(stopped, Is.False, "com a recarga parada, nada muda");
            Assert.That(restarted, Is.True);
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(2f).Within(1e-5f),
                "voltou à duração do asset, e não à duração aplicada");
            Assert.That(asc.GetChargeRechargeNormalized(spec), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Controls_AlsoActOnCooldownEffect()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 10f);
            ability.CooldownEffect = _gas.CreateDurationEffect(4f);
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);

            bool reduced = asc.ReduceCooldown(ability, 1f);
            float cooldownAfterReduce = asc.GetCooldownTimeRemaining(ability.CooldownEffect);
            float chargeAfterReduce = asc.GetChargeRechargeRemaining(spec);
            bool restarted = asc.RestartCooldown(ability);
            float cooldownAfterRestart = asc.GetCooldownTimeRemaining(ability.CooldownEffect);
            float chargeAfterRestart = asc.GetChargeRechargeRemaining(spec);
            bool reset = asc.ResetCooldown(ability);

            Assert.That(reduced, Is.True);
            Assert.That(cooldownAfterReduce, Is.EqualTo(3f).Within(1e-5f));
            Assert.That(chargeAfterReduce, Is.EqualTo(9f).Within(1e-5f), "ReduceCooldown não agiu na recarga da carga");
            Assert.That(restarted, Is.True);
            Assert.That(cooldownAfterRestart, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(chargeAfterRestart, Is.EqualTo(10f).Within(1e-5f), "RestartCooldown não agiu na recarga da carga");
            Assert.That(reset, Is.True);
            Assert.That(asc.IsOnCooldown(ability.CooldownEffect), Is.False);
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3), "ResetCooldown não devolveu a carga");
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(0f));
        }

        private static IEnumerator Spend(AbilitySystemComponent asc, GameplayAbility ability, int times)
        {
            for (int i = 0; i < times; i++)
            {
                Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
                yield return null; // a habilidade sem ações termina no quadro seguinte
            }
        }

        private GameplayAbility CreateChargedAbility(int maxCharges, float rechargeTime)
        {
            return CreateChargedAbility(new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.Constant(maxCharges)
            }, rechargeTime);
        }

        /// <summary>Máximo por <c>%Charges</c> da tabela: 2, 3 e 4 nos níveis 1 a 3.</summary>
        private GameplayAbility CreateTableMaxAbility(float rechargeTime)
        {
            GameplayAbility ability = CreateChargedAbility(new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.FromTable("Charges")
            }, rechargeTime);
            ability.Values.Add("Charges", ScalableFloat.FromArray(2f, 3f, 4f));
            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty("maxLevel").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return ability;
        }

        private GameplayAbility CreateAttributeMaxAbility(AttributeDefinition maxDef, float rechargeTime)
        {
            return CreateChargedAbility(new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Attribute,
                Attribute = maxDef
            }, rechargeTime);
        }

        private GameplayAbility CreateChargedAbility(ModifierValueSource maxCharges, float rechargeTime)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = maxCharges;
            ability.Charges.RechargeTime = ScalableFloat.Constant(rechargeTime);
            return ability;
        }

        private static void SetAttribute(AbilitySystemComponent asc, AttributeDefinition def, float value)
        {
            Assert.That(asc.AttributeSet.TryGetState(def, out AttributeState state), Is.True);
            state.SetBaseValue(value);
        }

        /// <summary>ASC com fonte manual e <c>autoTick</c> desligado: só avança por <c>Tick</c>.</summary>
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
