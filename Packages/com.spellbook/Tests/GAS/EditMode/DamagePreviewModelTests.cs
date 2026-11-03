using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Prévia "dano → processadores → alterações" do Inspector do atributo (B-010, DM8: R22, CA25).</summary>
    public class DamagePreviewModelTests
    {
        private GasFactory _gas;
        private DamagePreviewModel _model;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _model?.Dispose();
            _gas.Dispose();
        }

        [Test]
        public void Preview_Shield50Damage30_ListsShieldMinus30HealthZero()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition shield = _gas.CreateAttribute("Shield");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            _model = new DamagePreviewModel(health) { SampleDamage = 30f };
            _model.SetValue(health, 100f);
            _model.SetValue(shield, 50f);

            IReadOnlyList<DamagePreviewRow> rows = _model.Compute();

            Assert.That(_model.Attributes, Is.EqualTo(new[] { health, shield }));
            Assert.That(rows.Select(r => r.Attribute), Is.EqualTo(new[] { shield, health }));
            Assert.That(rows.Select(r => r.Applied), Is.EqualTo(new[] { -30f, 0f }));
            Assert.That(rows.Select(r => r.Before), Is.EqualTo(new[] { 50f, 100f }));
            Assert.That(rows.Select(r => r.After), Is.EqualTo(new[] { 20f, 100f }));
            Assert.That(rows[0].RedirectedFrom, Is.SameAs(health));
        }

        [Test]
        public void Preview_DestroysTemporaryAsc()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            int before = HiddenAscCount();

            _model = new DamagePreviewModel(health) { SampleDamage = 10f };
            _model.Compute();
            int whileOpen = HiddenAscCount();
            _model.Dispose();

            Assert.That(whileOpen, Is.EqualTo(before + 1));
            Assert.That(HiddenAscCount(), Is.EqualTo(before));
            Assert.That(_model.Compute(), Is.Empty, "Depois do Dispose, a prévia não recria o ASC.");
        }

        [Test]
        public void Preview_ReorderChangesResult()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AttributeDefinition shield = _gas.CreateAttribute("Shield");
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            SetProcessors(health, Reduce(armor), new AbsorbWithAttribute { Shield = shield });
            _model = new DamagePreviewModel(health) { SampleDamage = 30f };
            _model.SetValue(health, 100f);
            _model.SetValue(shield, 50f);
            _model.SetValue(armor, 10f);

            float reduceFirst = _model.Compute().Single(r => r.Attribute == shield).Applied;
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield }, Reduce(armor));
            float absorbFirst = _model.Compute().Single(r => r.Attribute == shield).Applied;

            Assert.That(reduceFirst, Is.EqualTo(-20f));
            Assert.That(absorbFirst, Is.EqualTo(-30f));
        }

        private static ReduceByAttribute Reduce(AttributeDefinition armor)
        {
            return new ReduceByAttribute { SourceAttribute = armor, Mode = ReductionMode.Flat };
        }

        private static int HiddenAscCount()
        {
            return Resources.FindObjectsOfTypeAll<AbilitySystemComponent>()
                .Count(asc => asc != null && (asc.gameObject.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave);
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
