#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>A habilidade com <see cref="CooldownReductionPolicy"/> aplica a recarga pela política (B-003, EA6: CA20).</summary>
    public class CooldownPolicyAbilityTests
    {
        private const float AssetCooldown = 8f;

        private GasFactory _gas;
        private TimeFactory _time;
        private CooldownReductionPolicy _policy;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _policy = ScriptableObject.CreateInstance<CooldownReductionPolicy>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_policy);
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Ability_WithPolicy_UsesPolicyDuration()
        {
            using (var serialized = new SerializedObject(_policy))
            {
                serialized.FindProperty("minDuration").floatValue = 1f;
                serialized.FindProperty("maxReduction").floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AttributeDefinition cdr = _gas.CreateAttribute("CooldownReduction");
            AbilitySystemComponent asc = _gas.CreateAsc((cdr, 0.9f));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(AssetCooldown);
            ability.CooldownReductionAttribute = cdr;
            GasFactory.SetObjectField(ability, "cooldownReductionPolicy", _policy);
            asc.GiveAbility(ability);

            AbilityActivationResult result = asc.TryActivateAbility(ability, Vector3.zero);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(asc.GetCooldownTimeRemaining(ability.CooldownEffect), Is.EqualTo(4f).Within(1e-5f),
                "sem a política, 90 % de redução daria 0,8 s");
        }
    }
}
#endif
