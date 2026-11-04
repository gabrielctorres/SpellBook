using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Core.Tests.Text
{
    /// <summary><c>TextTable</c> e <c>TableTextProvider</c> (Spec do B-008, Design 1; ADR 0038, item 2; CA9, N1).</summary>
    public class TableTextProviderTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void KeyInTable_ReturnsText()
        {
            TableTextProvider provider = Provider("pt-BR", Table("pt-BR", "gas.ability.fireball", "Bola de fogo"));

            string result = provider.GetText("gas.ability.fireball", "padrão");

            Assert.That(result, Is.EqualTo("Bola de fogo"));
        }

        [Test]
        public void MissingKey_ReturnsFallback()
        {
            TableTextProvider provider = Provider("pt-BR", Table("pt-BR", "gas.ability.fireball", "Bola de fogo"));

            Assert.That(provider.GetText("gas.ability.frost", "Raio de gelo"), Is.EqualTo("Raio de gelo"));
            Assert.That(provider.GetText(null, "sem chave"), Is.EqualTo("sem chave"));
            Assert.That(provider.GetText(string.Empty, "vazia"), Is.EqualTo("vazia"));
        }

        [Test]
        public void NoProvider_FieldText()
        {
            TableTextProvider provider = Provider("pt-BR", Table("pt-BR", "gas.ability.fireball", "Bola de fogo"));
            var field = new LocalizedText("gas.ability.fireball", "Fireball");

            Assert.That(field.Resolve(null), Is.EqualTo("Fireball"));
            Assert.That(field.Resolve(provider), Is.EqualTo("Bola de fogo"));
        }

        [Test]
        public void CultureChain_PtBr_Pt_Neutral_Fallback()
        {
            TableTextProvider provider = Provider("pt-BR",
                Table("", "ui.a.only_neutral", "neutra", "ui.a.all", "neutra"),
                Table("pt", "ui.a.parent", "pt", "ui.a.all", "pt"),
                Table("pt-BR", "ui.a.all", "pt-BR"),
                Table("en", "ui.a.english", "en"));

            Assert.That(provider.GetText("ui.a.all", "x"), Is.EqualTo("pt-BR"));
            Assert.That(provider.GetText("ui.a.parent", "x"), Is.EqualTo("pt"));
            Assert.That(provider.GetText("ui.a.only_neutral", "x"), Is.EqualTo("neutra"));
            Assert.That(provider.GetText("ui.a.english", "x"), Is.EqualTo("x"));
        }

        [Test]
        public void SetCulture_RaisesChanged_CultureExposed()
        {
            TableTextProvider provider = Provider("pt-BR",
                Table("pt-BR", "ui.a.hello", "Olá"),
                Table("en", "ui.a.hello", "Hello"));
            int changed = 0;
            provider.Changed += () => changed++;
            Assert.That(provider.GetText("ui.a.hello", "x"), Is.EqualTo("Olá"));

            provider.SetCulture(CultureInfo.GetCultureInfo("en-US"));

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(provider.Culture, Is.EqualTo(CultureInfo.GetCultureInfo("en-US")));
            Assert.That(provider.GetText("ui.a.hello", "x"), Is.EqualTo("Hello"));
        }

        [Test]
        public void EmptyCulture_Invariant()
        {
            TableTextProvider provider = Provider("",
                Table("", "ui.a.neutral", "neutra"),
                Table("pt-BR", "ui.a.pt", "pt-BR"));

            Assert.That(provider.Culture, Is.EqualTo(CultureInfo.InvariantCulture));
            Assert.That(provider.GetText("ui.a.neutral", "x"), Is.EqualTo("neutra"));
            Assert.That(provider.GetText("ui.a.pt", "x"), Is.EqualTo("x"));

            provider.SetCulture(null);

            Assert.That(provider.Culture, Is.EqualTo(CultureInfo.InvariantCulture));
        }

        [Test]
        public void ResolvedKey_10000Reads_DoesNotAllocate()
        {
            TableTextProvider provider = Provider("pt-BR", Table("pt-BR", "gas.ability.fireball", "Bola de fogo"));
            int total = 0;
            TestDelegate read10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    total += provider.GetText("gas.ability.fireball", "padrão").Length;
                    total += provider.GetText("gas.ability.frost", "padrão").Length;
                }
            };
            read10000(); // aquecimento: o dicionário é montado na primeira leitura

            Assert.That(read10000, Is.Not.AllocatingGCMemory());
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void Table_DuplicateKey_Error_BadKey_Warning()
        {
            TextTable table = Table("pt-BR", "ui.a.ok", "ok", "Ui.Bad Key", "ruim", "ui.a.ok", "repetida");
            var result = new ValidationResult(table);

            table.Validate(result);

            ValidationMessage[] errors = result.Messages.Where(m => m.Severity == ValidationSeverity.Error).ToArray();
            ValidationMessage[] warnings = result.Messages.Where(m => m.Severity == ValidationSeverity.Warning).ToArray();
            Assert.That(errors.Select(m => m.PropertyPath), Is.EqualTo(new[] { "entries.Array.data[2].key" }));
            Assert.That(warnings.Select(m => m.PropertyPath), Is.EqualTo(new[] { "entries.Array.data[1].key" }));
        }

        [Test]
        public void Provider_SameKeyDifferentText_Warning()
        {
            TableTextProvider provider = Provider("pt-BR",
                Table("pt-BR", "ui.a.same", "igual", "ui.a.conflict", "um"),
                Table("pt-BR", "ui.a.same", "igual", "ui.a.conflict", "outro"),
                Table("en", "ui.a.conflict", "other"));
            var result = new ValidationResult(provider);

            provider.Validate(result);

            Assert.That(result.Messages, Has.Count.EqualTo(1));
            Assert.That(result.Messages[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(result.Messages[0].Message, Does.Contain("ui.a.conflict"));
            Assert.That(result.Messages[0].PropertyPath, Is.EqualTo("tables.Array.data[1]"));
        }

        [Test]
        public void Provider_NullTable_Error()
        {
            TableTextProvider provider = Provider("pt-BR", Table("pt-BR", "ui.a.ok", "ok"), null);
            var result = new ValidationResult(provider);

            provider.Validate(result);

            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.Messages.Single().PropertyPath, Is.EqualTo("tables.Array.data[1]"));
        }

        /// <summary>Tabela com pares chave, texto, gravados pelo <see cref="SerializedObject"/>.</summary>
        private TextTable Table(string culture, params string[] keyTextPairs)
        {
            var table = ScriptableObject.CreateInstance<TextTable>();
            _created.Add(table);
            var serialized = new SerializedObject(table);
            serialized.FindProperty("cultureName").stringValue = culture;
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = keyTextPairs.Length / 2;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = keyTextPairs[i * 2];
                entry.FindPropertyRelative("text").stringValue = keyTextPairs[i * 2 + 1];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private TableTextProvider Provider(string culture, params TextTable[] tables)
        {
            var provider = ScriptableObject.CreateInstance<TableTextProvider>();
            _created.Add(provider);
            var serialized = new SerializedObject(provider);
            serialized.FindProperty("cultureName").stringValue = culture;
            SerializedProperty list = serialized.FindProperty("tables");
            list.arraySize = tables.Length;
            for (int i = 0; i < tables.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = tables[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return provider;
        }
    }
}
