#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellBook.GAS.Tests
{
    /// <summary>MaxLevel, SetAbilityLevel e OnAbilityLevelChanged (B-007, VN4: CA28, CA29).</summary>
    public class AbilityLevelTests
    {
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        private GameplayAbility CreateAbility(int maxLevel)
        {
            GameplayAbility ability = _gas.CreateAbility();
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        [Test]
        public void MaxLevel_ClampsAndWarnsOnce()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = CreateAbility(3);
            LogAssert.Expect(LogType.Warning, new Regex("MaxLevel"));

            AbilitySpec spec = asc.GiveAbility(ability, 5);
            asc.SetAbilityLevel(ability, 9);
            asc.SetAbilityLevel(ability, 0);

            Assert.That(ability.MaxLevel, Is.EqualTo(3));
            Assert.That(spec.Level, Is.EqualTo(1), "nível abaixo de 1 deve virar 1");
            asc.SetAbilityLevel(ability, 4);
            Assert.That(spec.Level, Is.EqualTo(3), "nível acima do máximo deve virar o máximo");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LevelChanged_OnlyOnRealChange()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = CreateAbility(3);
            AbilitySpec spec = asc.GiveAbility(ability, 1);
            var changes = new List<(AbilitySpec Spec, int Before, int After)>();
            asc.OnAbilityLevelChanged += (s, before, after) => changes.Add((s, before, after));

            asc.SetAbilityLevel(ability, 2);
            asc.SetAbilityLevel(ability, 2);
            LogAssert.Expect(LogType.Warning, new Regex("MaxLevel"));
            asc.SetAbilityLevel(ability, 3);
            asc.SetAbilityLevel(ability, 7);

            Assert.That(changes, Is.EqualTo(new[] { (spec, 1, 2), (spec, 2, 3) }));
        }

        [Test]
        public void SetGetAbilityLevel_GiveWithLevel()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility granted = CreateAbility(5);
            GameplayAbility notGranted = CreateAbility(5);

            asc.GiveAbility(granted, 2);

            Assert.That(asc.GetAbilityLevel(granted), Is.EqualTo(2));
            Assert.That(asc.SetAbilityLevel(granted, 4), Is.True);
            Assert.That(asc.GetAbilityLevel(granted), Is.EqualTo(4));
            Assert.That(asc.FindSpec(granted).Level, Is.EqualTo(4));
            Assert.That(asc.GetAbilityLevel(notGranted), Is.EqualTo(0));
            Assert.That(asc.SetAbilityLevel(notGranted, 2), Is.False);
            Assert.That(asc.GetAbilityLevel(notGranted), Is.EqualTo(0));
        }
    }
}
#endif
