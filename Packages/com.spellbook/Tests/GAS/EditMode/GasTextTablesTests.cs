using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Text;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>Chaves de texto do GAS e as tabelas pt-BR e en do pacote (Spec do B-008, Design 6; Q5).</summary>
    public class GasTextTablesTests
    {
        private const string Folder = "Packages/com.spellbook/Runtime/GAS/Text/";

        [TestCase("GasTexts.pt-BR.asset", "pt-BR")]
        [TestCase("GasTexts.en.asset", "en")]
        public void EveryGasTextKey_HasTextInPtBrAndEn(string file, string culture)
        {
            TextTable table = Load(file);

            Assert.That(table.CultureName, Is.EqualTo(culture));
            foreach (string key in AllKeys())
            {
                TextTable.Entry[] found = table.Entries.Where(e => e.Key == key).ToArray();
                Assert.That(found.Length, Is.EqualTo(1), $"{key} em {file}");
                Assert.That(found[0].Text, Is.Not.Empty, $"{key} em {file}");
            }
        }

        [TestCase("GasTexts.pt-BR.asset")]
        [TestCase("GasTexts.en.asset")]
        public void Tables_OnlyGasTextKeys_NoValidationIssues(string file)
        {
            TextTable table = Load(file);
            var result = new ValidationResult(table);

            table.Validate(result);

            Assert.That(table.Entries.Select(e => e.Key), Is.EquivalentTo(AllKeys()));
            Assert.That(result.Messages, Is.Empty);
        }

        [Test]
        public void Tables_ChargesLabel_KeepsBothPlaceholders()
        {
            foreach (string file in new[] { "GasTexts.pt-BR.asset", "GasTexts.en.asset" })
            {
                string text = Load(file).Entries.Single(e => e.Key == GasTextKeys.DescriptionCharges).Text;
                Assert.That(text, Does.Contain("{0}").And.Contain("{1}"), file);
            }
        }

        [Test]
        public void GasTextKeys_FollowConvention()
        {
            string[] keys = AllKeys();

            Assert.That(keys.Length, Is.EqualTo(21));
            Assert.That(keys, Is.Unique);
            foreach (string key in keys)
            {
                Assert.That(TextKey.IsValid(key), Is.True, key);
                Assert.That(key, Does.StartWith("gas.activation.").Or.StartWith("gas.description.").Or.StartWith("gas.tooltip."),
                    key);
            }
        }

        [Test]
        public void GasConditionDefaultKeys_AreGasTextKeys()
        {
            string[] keys = AllKeys();
            Type[] conditions = typeof(ConditionContext).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Condition<ConditionContext>).IsAssignableFrom(t) &&
                            t.GetConstructor(Type.EmptyTypes) != null)
                .ToArray();

            Assert.That(conditions, Is.Not.Empty);
            foreach (Type type in conditions)
            {
                var condition = (Condition<ConditionContext>)Activator.CreateInstance(type);
                if (condition.DefaultFailureKey == null) continue;
                Assert.That(keys, Does.Contain(condition.DefaultFailureKey), type.Name);
            }
        }

        [Test]
        public void ResultKeys_AreGasTextKeys()
        {
            string[] keys = AllKeys();
            foreach (AbilityActivationResult result in Enum.GetValues(typeof(AbilityActivationResult)))
            {
                string key = GasTextKeys.ForResult(result);
                if (key != null) Assert.That(keys, Does.Contain(key), result.ToString());
            }
        }

        private static TextTable Load(string file)
        {
            var table = AssetDatabase.LoadAssetAtPath<TextTable>(Folder + file);
            Assert.That(table, Is.Not.Null, Folder + file);
            return table;
        }

        private static string[] AllKeys()
        {
            var keys = new List<string>();
            foreach (FieldInfo field in typeof(GasTextKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string)) keys.Add((string)field.GetRawConstantValue());
            }

            return keys.ToArray();
        }
    }
}
