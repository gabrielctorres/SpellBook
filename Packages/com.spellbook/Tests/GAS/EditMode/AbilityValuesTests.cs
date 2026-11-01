using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Values;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Tabela AbilityValues e modo Table do ScalableFloat (B-007, VN4: CA8 runtime, CA9, CA11 e parte do CA26).</summary>
    public class AbilityValuesTests
    {
        private GameplayAbility _ability;

        [SetUp]
        public void SetUp()
        {
            _ability = ScriptableObject.CreateInstance<GameplayAbility>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_ability);
        }

        [Test]
        public void GetValue_NameAndLevel_ReturnsAndExists()
        {
            _ability.Values.Add("Damage", ScalableFloat.FromArray(50f, 75f, 100f));
            _ability.Values.Add("Radius", ScalableFloat.Constant(3f));

            bool found = _ability.TryGetValue("Damage", 3, out float damage);

            Assert.That(found, Is.True);
            Assert.That(damage, Is.EqualTo(100f));
            Assert.That(_ability.Values.IndexOf("Radius"), Is.EqualTo(1));
            Assert.That(_ability.Values.Evaluate(1, 2), Is.EqualTo(3f));
        }

        [Test]
        public void GetValue_UnknownName_FalseNoWarning()
        {
            _ability.Values.Add("Damage", ScalableFloat.Constant(50f));

            bool found = _ability.TryGetValue("damage", 1, out float value);

            Assert.That(found, Is.False, "a busca por nome deve diferenciar maiúsculas");
            Assert.That(value, Is.EqualTo(0f));
            Assert.That(_ability.Values.IndexOf("Missing"), Is.EqualTo(-1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Validate_DuplicateOrInvalidNames_TableInsideTable()
        {
            var values = new AbilityValues();
            values.Add("Damage", ScalableFloat.Constant(1f));
            values.Add("Damage", ScalableFloat.Constant(2f));
            values.Add("", ScalableFloat.Constant(3f));
            values.Add("bad name", ScalableFloat.Constant(4f));
            values.Add("Ref", ScalableFloat.FromTable("Damage"));
            values.Add("Ok_2", ScalableFloat.FromArray(1f, 2f));
            var result = new ValidationResult(null);

            values.Validate(result, "values");

            string[] errors = result.Messages
                .Where(m => m.Severity == ValidationSeverity.Error)
                .Select(m => m.PropertyPath)
                .ToArray();
            Assert.That(errors, Is.EquivalentTo(new[]
            {
                "values.entries[1].name",
                "values.entries[2].name",
                "values.entries[3].name",
                "values.entries[4].value"
            }));
        }

        [Test]
        public void Table_SwitchingTables_ReResolves_EditingTableBumpsVersion()
        {
            var first = new AbilityValues();
            first.Add("Damage", ScalableFloat.Constant(10f));
            var second = new AbilityValues();
            second.Add("Other", ScalableFloat.Constant(0f));
            second.Add("Damage", ScalableFloat.FromArray(20f, 40f));
            ScalableFloat value = ScalableFloat.FromTable("Damage", 99f);

            Assert.That(value.Evaluate(1, first), Is.EqualTo(10f));
            Assert.That(value.Evaluate(2, second), Is.EqualTo(40f));
            Assert.That(value.Evaluate(1, first), Is.EqualTo(10f));

            int before = first.Version;
            first.Set("Damage", ScalableFloat.Constant(15f));

            Assert.That(first.Version, Is.GreaterThan(before));
            Assert.That(value.Evaluate(1, first), Is.EqualTo(15f));

            Assert.That(value.Evaluate(1, second), Is.EqualTo(20f));
            second.Remove("Other");

            Assert.That(value.Evaluate(1, second), Is.EqualTo(20f), "o índice em cache ficou velho depois da remoção");
        }

        [Test]
        public void Table_NoTable_Fallback_NoWarning()
        {
            ScalableFloat value = ScalableFloat.FromTable("Damage", 7f);

            Assert.That(value.Evaluate(1), Is.EqualTo(7f));
            Assert.That(value.Evaluate(3, null), Is.EqualTo(7f));
            Assert.That(ScalableFloat.FromTable("Damage").Evaluate(1), Is.EqualTo(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Table_UnknownName_FallbackSingleWarning()
        {
            var table = new AbilityValues();
            table.Add("Damage", ScalableFloat.Constant(10f));
            ScalableFloat value = ScalableFloat.FromTable("Missing", 5f);
            LogAssert.Expect(LogType.Warning, new Regex("Missing"));

            float first = value.Evaluate(1, table);
            float second = value.Evaluate(2, table);

            Assert.That(first, Is.EqualTo(5f));
            Assert.That(second, Is.EqualTo(5f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TableEvaluate_DoesNotAllocate_10000Times()
        {
            var table = new AbilityValues();
            table.Add("Cooldown", ScalableFloat.FromArray(10f, 8f, 6f));
            table.Add("Damage", ScalableFloat.FromArray(50f, 75f, 100f));
            ScalableFloat value = ScalableFloat.FromTable("Damage");
            float sum = value.Evaluate(1, table); // aquecimento (resolve o nome)

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    sum += value.Evaluate(i % 4, table);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sum, Is.GreaterThan(0f));
        }

        [Test]
        public void ScalableValidate_TableWithoutName_TableNotAllowed()
        {
            var result = new ValidationResult(null);

            ScalableFloat.FromTable("").Validate(result, "a");
            ScalableFloat.FromTable("Damage").Validate(result, "b", allowTable: false);
            ScalableFloat.FromTable("Damage").Validate(result, "c");

            Assert.That(result.Messages.Select(m => m.PropertyPath), Is.EqualTo(new[] { "a", "b" }));
        }
    }
}
