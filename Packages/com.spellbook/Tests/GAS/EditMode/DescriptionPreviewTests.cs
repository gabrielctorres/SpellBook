using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Description;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using TextElement = SpellBook.GAS.Description.TextElement;

namespace SpellBook.GAS.Tests
{
    /// <summary>Prévia da descrição por nível no Inspector da habilidade (Spec do B-008, Design 7; CA13, CA8).</summary>
    public class DescriptionPreviewTests
    {
        private GasFactory _gas;
        private UnityEditor.Editor _editor;
        private string _savedCulture;
        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _themeBefore = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _savedCulture = SessionState.GetString(DescriptionPreviewElement.CultureSessionKey, null);
            SessionState.EraseString(DescriptionPreviewElement.CultureSessionKey);
            _themeBefore.Clear();
            _themeBefore.AddRange(GeneratedTheme.Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null));
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null) Object.DestroyImmediate(_editor);
            foreach (Object item in _created.Where(o => o != null)) Object.DestroyImmediate(item);
            _created.Clear();
            _gas.Dispose();
            if (_savedCulture == null) SessionState.EraseString(DescriptionPreviewElement.CultureSessionKey);
            else SessionState.SetString(DescriptionPreviewElement.CultureSessionKey, _savedCulture);
            DeleteGeneratedTheme();
        }

        [Test]
        public void Preview_InAbilityInspector_DescriptionSection()
        {
            GameplayAbility ability = Ability(new TextElement { Text = new LocalizedText(null, "Lança uma bola de fogo.") });

            VisualElement root = CreateInspector(ability);

            VisualElement section = root.Q(GameplayAbilityEditor.DescriptionSectionName);
            Assert.That(section, Is.Not.Null);
            Assert.That(section.Q<DescriptionPreviewElement>(), Is.Not.Null);
            Assert.That(LineTexts(section), Is.EqualTo(new[] { "Lança uma bola de fogo." }));
        }

        [Test]
        public void Preview_LevelChange_UpdatesLines()
        {
            GameplayAbility ability = Ability(Template("Causa {damage} de dano"));
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f));
            SetMaxLevel(ability, 3);
            VisualElement root = CreateInspector(ability);
            Assert.That(LineTexts(root), Is.EqualTo(new[] { "Causa 50 de dano" }));

            ((GameplayAbilityEditor)_editor).SetPreviewLevel(3);

            Assert.That(LineTexts(root), Is.EqualTo(new[] { "Causa 100 de dano" }));
            Assert.That(root.Q<DescriptionPreviewElement>().Level, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Preview_AssetEdit_Updates()
        {
            GameplayAbility ability = Ability(new TextElement { Text = new LocalizedText(null, "Antes") });
            var serialized = new SerializedObject(ability);
            var preview = new DescriptionPreviewElement(serialized);
            yield return Show(preview);
            Assert.That(LineTexts(preview), Is.EqualTo(new[] { "Antes" }));

            serialized.FindProperty("Description.Elements.Array.data[0].text.text").stringValue = "Depois";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            yield return WaitUntil(() => LineTexts(preview).SequenceEqual(new[] { "Depois" }));

            Assert.That(LineTexts(preview), Is.EqualTo(new[] { "Depois" }));
        }

        [Test]
        public void Preview_CultureField_DefaultIsEditorCulture()
        {
            var preview = new DescriptionPreviewElement(new SerializedObject(Ability()));

            var field = preview.Q<TextField>(DescriptionPreviewElement.CultureFieldName);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.value, Is.EqualTo(CultureInfo.CurrentCulture.Name));
        }

        [Test]
        public void Preview_CultureChange_FormatsNumbers()
        {
            GameplayAbility ability = Ability(Template("Causa {damage}"));
            ability.Values.Add("damage", ScalableFloat.Constant(1.5f));
            var preview = new DescriptionPreviewElement(new SerializedObject(ability));

            preview.SetCulture("pt-BR");
            string[] ptBr = LineTexts(preview);
            preview.SetCulture("en-US");
            string[] enUs = LineTexts(preview);

            Assert.That(ptBr, Is.EqualTo(new[] { "Causa 1,5" }));
            Assert.That(enUs, Is.EqualTo(new[] { "Causa 1.5" }));
            Assert.That(preview.Q<TextField>(DescriptionPreviewElement.CultureFieldName).value, Is.EqualTo("en-US"));
            Assert.That(SessionState.GetString(DescriptionPreviewElement.CultureSessionKey, null), Is.EqualTo("en-US"));
            Assert.That(new DescriptionPreviewElement(new SerializedObject(ability))
                .Q<TextField>(DescriptionPreviewElement.CultureFieldName).value, Is.EqualTo("en-US"), "a cultura fica na sessão");
        }

        [Test]
        public void Preview_Provider_ResolvesKeys()
        {
            GameplayAbility ability = Ability(new TextElement { Text = new LocalizedText("gas.test.fireball", "Bola de fogo") });
            TableTextProvider provider = Provider(Table("en", "gas.test.fireball", "Fireball"));
            var preview = new DescriptionPreviewElement(new SerializedObject(ability));
            string[] withoutProvider = LineTexts(preview);

            preview.SetProvider(provider);

            Assert.That(withoutProvider, Is.EqualTo(new[] { "Bola de fogo" }));
            Assert.That(LineTexts(preview), Is.EqualTo(new[] { "Fireball" }));
            Assert.That(preview.Q<ObjectField>(DescriptionPreviewElement.ProviderFieldName).value, Is.SameAs(provider));
        }

        [Test]
        public void Preview_PlainText_NoMarkup()
        {
            GameplayAbility ability = Ability(
                new TextElement { Text = new LocalizedText(null, "<b>forte</b>") },
                Template("Causa {damage}"),
                new CooldownElement());
            ability.Values.Add("damage", ScalableFloat.Constant(10f));
            var preview = new DescriptionPreviewElement(new SerializedObject(ability));

            Label[] lines = Lines(preview);

            Assert.That(lines.Length, Is.EqualTo(2), "sem recarga, o CooldownElement não gera linha");
            Assert.That(lines.All(l => !l.enableRichText), Is.True, "a prévia mostra o texto como está");
            Assert.That(lines[0].text, Is.EqualTo("<b>forte</b>"));
            Assert.That(lines[1].text, Does.Not.Contain("<"));
        }

        [Test]
        public void Preview_EmptyDescription_ShowsNotice()
        {
            GameplayAbility ability = Ability();
            var preview = new DescriptionPreviewElement(new SerializedObject(ability));

            Assert.That(Lines(preview), Is.Empty);
            Assert.That(preview.Q<Label>(DescriptionPreviewElement.EmptyName).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            ability.Description.Elements.Add(new TextElement { Text = new LocalizedText(null, "Algo") });
            preview.Refresh();

            Assert.That(preview.Q<Label>(DescriptionPreviewElement.EmptyName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private GameplayAbility Ability(params DescriptionElement[] elements)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Description = new AbilityDescription();
            ability.Description.Elements.AddRange(elements);
            return ability;
        }

        private static TemplateElement Template(string text)
        {
            return new TemplateElement { Template = new LocalizedText(null, text) };
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty("maxLevel").intValue = maxLevel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private VisualElement CreateInspector(GameplayAbility ability)
        {
            _editor = UnityEditor.Editor.CreateEditor(ability);
            return _editor.CreateInspectorGUI();
        }

        private static Label[] Lines(VisualElement root)
        {
            return root.Query<Label>(className: DescriptionPreviewElement.LineClass).ToList().ToArray();
        }

        private static string[] LineTexts(VisualElement root)
        {
            return Lines(root).Select(l => l.text).ToArray();
        }

        private TextTable Table(string culture, string key, string text)
        {
            var table = ScriptableObject.CreateInstance<TextTable>();
            _created.Add(table);
            var serialized = new SerializedObject(table);
            serialized.FindProperty("cultureName").stringValue = culture;
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("key").stringValue = key;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("text").stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private TableTextProvider Provider(TextTable table)
        {
            var provider = ScriptableObject.CreateInstance<TableTextProvider>();
            _created.Add(provider);
            var serialized = new SerializedObject(provider);
            serialized.FindProperty("cultureName").stringValue = table.CultureName;
            SerializedProperty tables = serialized.FindProperty("tables");
            tables.arraySize = 1;
            tables.GetArrayElementAtIndex(0).objectReferenceValue = table;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return provider;
        }

        /// <summary>
        /// Põe o elemento num painel de <see cref="UIDocument"/>, que não pede dispositivo gráfico (o batchmode roda com
        /// <c>-nographics</c>), para o <c>TrackSerializedObjectValue</c> conferir o asset no laço do Editor. A Unity
        /// grava o tema padrão em <c>Assets/UI Toolkit/</c> ao criar o painel; o TearDown apaga.
        /// </summary>
        private IEnumerator Show(VisualElement element)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var host = new GameObject("DescriptionPreviewPanel");
            _created.Add(host);
            _created.Add(settings);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            yield return WaitUntil(() => document.rootVisualElement?.panel != null);

            document.rootVisualElement.Add(element);
            yield return null;
        }

        /// <summary>Espera o laço do Editor até a condição, com um teto de quadros.</summary>
        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            for (int frame = 0; frame < MaxFrames && !condition(); frame++)
            {
                yield return null;
            }
        }

        private const int MaxFrames = 300;

        /// <summary>O tema padrão que a Unity grava em <c>Assets/</c> ao criar o painel, com as pastas, do arquivo para a raiz.</summary>
        private static readonly string[] GeneratedTheme =
        {
            "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss",
            "Assets/UI Toolkit/UnityThemes",
            "Assets/UI Toolkit",
        };

        /// <summary>Apaga o tema e as pastas que o teste criou e que ficaram vazias; o que já existia fica.</summary>
        private void DeleteGeneratedTheme()
        {
            foreach (string path in GeneratedTheme.Where(path => !_themeBefore.Contains(path)))
            {
                bool isFolder = AssetDatabase.IsValidFolder(path);
                if (isFolder && AssetDatabase.FindAssets(string.Empty, new[] { path }).Length > 0) return;
                if (isFolder || AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
