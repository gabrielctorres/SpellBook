#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Cargas: configuração, gasto, recarga sequencial e API (B-009, AV6: CA13, CA14, CA16 a CA19, CA21, CA22).</summary>
    public class ChargesTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object asset in _assets) Object.Destroy(asset);
            _assets.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [UnityTest]
        public IEnumerator Three_Spend_FourthNoCharges_SequentialRecharge()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 2f);
            AbilitySpec spec = asc.GiveAbility(ability);
            var results = new List<AbilityActivationResult>();

            for (int i = 0; i < 4; i++)
            {
                results.Add(asc.TryActivateAbility(ability, AbilityTarget.None));
                yield return null; // a habilidade sem ações termina no quadro seguinte
            }
            asc.Tick(2.1f);
            int after2_1 = asc.GetCharges(spec);
            asc.Tick(2f);
            int after4_1 = asc.GetCharges(spec);
            asc.Tick(2f);

            Assert.That(results, Is.EqualTo(new[]
            {
                AbilityActivationResult.Success, AbilityActivationResult.Success,
                AbilityActivationResult.Success, AbilityActivationResult.NoCharges
            }));
            Assert.That(after2_1, Is.EqualTo(1), "uma carga por RechargeTime");
            Assert.That(after4_1, Is.EqualTo(2), "a recarga é sequencial: a segunda carga volta no dobro do tempo");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3));
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(0f), "no máximo, a recarga para");
        }

        [UnityTest]
        public IEnumerator OneOfThree_RechargeRunning_Activates()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            yield return null;
            asc.TryActivateAbility(ability, AbilityTarget.None);
            yield return null;
            asc.Tick(1f);
            float remainingBefore = asc.GetChargeRechargeRemaining(spec);

            AbilityActivationResult result = asc.TryActivateAbility(ability, AbilityTarget.None);

            Assert.That(remainingBefore, Is.EqualTo(4f).Within(1e-5f), "a recarga da carga não estava correndo");
            Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(asc.GetCharges(spec), Is.EqualTo(0));
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(4f).Within(1e-5f),
                "gastar com a recarga correndo não a recomeça");
        }

        [UnityTest]
        public IEnumerator AddRespectsMax_RefillFills_ChangedOnlyOnRealChange()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            var changes = new List<(int Before, int After)>();
            asc.OnChargesChanged += (s, before, after) => changes.Add((before, after));
            yield return SpendTwice(asc, ability);
            changes.Clear();

            bool addOne = asc.AddCharges(spec, 1);
            bool addFive = asc.AddCharges(spec, 5);
            bool addAtMax = asc.AddCharges(spec, 1);
            bool addZero = asc.AddCharges(spec, 0);
            bool addNegative = asc.AddCharges(spec, -1);
            bool refillAtMax = asc.RefillCharges(spec);
            int chargesAfterAdd = asc.GetCharges(spec);
            float remainingAtMax = asc.GetChargeRechargeRemaining(spec);
            var addChanges = new List<(int, int)>(changes);
            yield return SpendTwice(asc, ability);
            changes.Clear();
            bool refill = asc.RefillCharges(spec);

            Assert.That(new[] { addOne, addFive, addAtMax, addZero, addNegative, refillAtMax },
                Is.EqualTo(new[] { true, true, false, false, false, false }));
            Assert.That(chargesAfterAdd, Is.EqualTo(3), "AddCharges passou do máximo");
            Assert.That(remainingAtMax, Is.EqualTo(0f), "no máximo, a recarga para");
            Assert.That(addChanges, Is.EqualTo(new[] { (1, 2), (2, 3) }), "evento sem mudança real ou faltando");
            Assert.That(refill, Is.True);
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(new[] { (1, 3) }));
            Assert.That(asc.AddCharges(null, 1), Is.False);
            Assert.That(asc.RefillCharges(null), Is.False);
            Assert.That(asc.GetCharges(null), Is.EqualTo(0));
        }

        [Test]
        public void RechargeUsesPolicyFloorAndCap()
        {
            CooldownReductionPolicy policy = CreatePolicy(minDuration: 1f, maxReduction: 0.5f);
            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = CreateManualAsc((cdr, 0.9f));
            GameplayAbility capped = CreateChargedAbility(2, 8f);
            GameplayAbility floored = CreateChargedAbility(2, 1.5f);
            foreach (GameplayAbility ability in new[] { capped, floored })
            {
                ability.CooldownReductionAttribute = cdr;
                GasFactory.SetObjectField(ability, "cooldownReductionPolicy", policy);
            }
            AbilitySpec cappedSpec = asc.GiveAbility(capped);
            AbilitySpec flooredSpec = asc.GiveAbility(floored);

            asc.TryActivateAbility(capped, AbilityTarget.None);
            asc.TryActivateAbility(floored, AbilityTarget.None);

            Assert.That(asc.GetChargeRechargeRemaining(cappedSpec), Is.EqualTo(4f).Within(1e-5f),
                "o teto de 50 % da política não valeu (90 % daria 0,8 s)");
            Assert.That(cappedSpec.ChargeRechargeDuration, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(asc.GetChargeRechargeRemaining(flooredSpec), Is.EqualTo(1f).Within(1e-5f),
                "o piso de 1 s da política não valeu");
            Assert.That(asc.GetChargeRechargeNormalized(cappedSpec), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void BornFull_DisabledSameAsToday()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility charged = CreateChargedAbility(3, 2f);
            GameplayAbility plain = _gas.CreateAbility(new WaitAction { Duration = ScalableFloat.Constant(100f) }); // sem Tick, segue ativa
            plain.CooldownEffect = _gas.CreateDurationEffect(5f);
            AbilitySpec chargedSpec = asc.GiveAbility(charged);
            AbilitySpec plainSpec = asc.GiveAbility(plain);
            int changes = 0;
            asc.OnChargesChanged += (s, before, after) => changes++;

            AbilityActivationResult plainResult = asc.TryActivateAbility(plain, AbilityTarget.None);

            Assert.That(charged.Charges.Enabled, Is.True);
            Assert.That(plain.Charges.Enabled, Is.False, "as cargas nascem desligadas");
            Assert.That(chargedSpec.Charges, Is.EqualTo(3), "as cargas não nasceram cheias");
            Assert.That(asc.GetMaxCharges(chargedSpec), Is.EqualTo(3));
            Assert.That(asc.GetChargeRechargeRemaining(chargedSpec), Is.EqualTo(0f));
            Assert.That(asc.GetChargeRechargeNormalized(chargedSpec), Is.EqualTo(0f));
            Assert.That(plainResult, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(asc.GetActivationResult(plain), Is.EqualTo(AbilityActivationResult.AlreadyActive));
            Assert.That(asc.GetCharges(plainSpec), Is.EqualTo(0));
            Assert.That(asc.GetMaxCharges(plainSpec), Is.EqualTo(0));
            Assert.That(asc.AddCharges(plainSpec, 1), Is.False);
            Assert.That(asc.RefillCharges(plainSpec), Is.False);
            Assert.That(changes, Is.EqualTo(0), "habilidade sem cargas disparou OnChargesChanged");
        }

        [UnityTest]
        public IEnumerator WithCooldown_BlocksBetweenUses_WithoutOnlyCharges()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility withCooldown = CreateChargedAbility(3, 10f);
            withCooldown.CooldownEffect = _gas.CreateDurationEffect(1f);
            GameplayAbility singleWithCooldown = CreateChargedAbility(1, 10f);
            singleWithCooldown.CooldownEffect = _gas.CreateDurationEffect(1f);
            GameplayAbility chargesOnly = CreateChargedAbility(3, 10f);
            asc.GiveAbility(withCooldown);
            asc.GiveAbility(singleWithCooldown);
            asc.GiveAbility(chargesOnly);

            asc.TryActivateAbility(withCooldown, AbilityTarget.None);
            asc.TryActivateAbility(singleWithCooldown, AbilityTarget.None);
            AbilityActivationResult chargesOnlyFirst = asc.TryActivateAbility(chargesOnly, AbilityTarget.None);
            yield return null;
            AbilityActivationResult betweenUses = asc.GetActivationResult(withCooldown);
            AbilityActivationResult noChargeAndCooldown = asc.GetActivationResult(singleWithCooldown);
            AbilityActivationResult chargesOnlySecond = asc.TryActivateAbility(chargesOnly, AbilityTarget.None);
            asc.Tick(1.1f);

            Assert.That(betweenUses, Is.EqualTo(AbilityActivationResult.OnCooldown),
                "com carga disponível, o Cooldown barra o uso entre ativações");
            Assert.That(noChargeAndCooldown, Is.EqualTo(AbilityActivationResult.NoCharges),
                "as cargas vêm antes do Cooldown na checagem 2");
            Assert.That(chargesOnlyFirst, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(chargesOnlySecond, Is.EqualTo(AbilityActivationResult.Success), "sem Cooldown, só as cargas decidem");
            Assert.That(asc.GetActivationResult(withCooldown), Is.EqualTo(AbilityActivationResult.Success));
        }

        [UnityTest]
        public IEnumerator ManualTimeSource_Only()
        {
            AbilitySystemComponent asc = _gas.CreateAsc(); // autoTick ligado (padrão)
            ManualTimeSource source = _time.CreateManual();
            asc.SetTimeSource(source);
            GameplayAbility ability = CreateChargedAbility(1, 0.01f);
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
            int chargesWhileStopped = asc.GetCharges(spec);
            source.Advance(0.02f);
            yield return null;

            Assert.That(chargesWhileStopped, Is.EqualTo(0), "a recarga andou pelo relógio da Unity");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(1), "a recarga não andou pela fonte de tempo");
        }

        [UnityTest]
        public IEnumerator BigTick_RestoresSeveral_CarriesExcess()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 2f);
            AbilitySpec spec = asc.GiveAbility(ability);
            yield return SpendTwice(asc, ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            var changes = new List<(int Before, int After)>();
            asc.OnChargesChanged += (s, before, after) => changes.Add((before, after));

            asc.Tick(5f);
            int chargesAfter5 = asc.GetCharges(spec);
            float remainingAfter5 = asc.GetChargeRechargeRemaining(spec);
            asc.Tick(1f);

            Assert.That(chargesAfter5, Is.EqualTo(2));
            Assert.That(remainingAfter5, Is.EqualTo(1f).Within(1e-5f), "o excesso de 1 s não passou para a próxima carga");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3));
            Assert.That(changes, Is.EqualTo(new[] { (0, 1), (1, 2), (2, 3) }));
        }

        [Test]
        public void Query_DoesNotSpendCharge()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(2, 2f);
            AbilitySpec spec = asc.GiveAbility(ability);
            int changes = 0;
            asc.OnChargesChanged += (s, before, after) => changes++;

            AbilityActivationResult query = asc.GetActivationResult(ability, AbilityTarget.None, out _);
            bool canActivate = ability.CanActivate(asc);

            Assert.That(query, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(canActivate, Is.True);
            Assert.That(asc.GetCharges(spec), Is.EqualTo(2), "a consulta gastou carga");
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(0f), "a consulta começou a recarga");
            Assert.That(changes, Is.EqualTo(0), "a consulta disparou OnChargesChanged");
        }

        [Test]
        public void Ticks1000_DoNotAllocate()
        {
            AbilitySystemComponent asc = CreateManualAsc();
            GameplayAbility ability = CreateChargedAbility(3, 5f);
            AbilitySpec spec = asc.GiveAbility(ability);
            asc.TryActivateAbility(ability, AbilityTarget.None);
            asc.Tick(0.01f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    asc.Tick(0.01f);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(asc.GetCharges(spec), Is.EqualTo(3), "os ticks não cobriram a reposição da carga");
        }

        private IEnumerator SpendTwice(AbilitySystemComponent asc, GameplayAbility ability)
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.That(asc.TryActivateAbility(ability, AbilityTarget.None), Is.EqualTo(AbilityActivationResult.Success));
                yield return null;
            }
        }

        private GameplayAbility CreateChargedAbility(int maxCharges, float rechargeTime)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.Constant(maxCharges)
            };
            ability.Charges.RechargeTime = ScalableFloat.Constant(rechargeTime);
            return ability;
        }

        private CooldownReductionPolicy CreatePolicy(float minDuration, float maxReduction)
        {
            var policy = ScriptableObject.CreateInstance<CooldownReductionPolicy>();
            _assets.Add(policy);
            using (var serialized = new SerializedObject(policy))
            {
                serialized.FindProperty("minDuration").floatValue = minDuration;
                serialized.FindProperty("maxReduction").floatValue = maxReduction;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return policy;
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
