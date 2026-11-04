using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Nomes exibidos em <see cref="LocalizedText"/> (DE5): o YAML 2.x abre com o texto que tinha, asset novo grava
    /// o campo novo com o legado vazio, e o menu <c>SpellBook &gt; Migrar assets do GAS (3.0)</c> regrava habilidades,
    /// atributos e categorias.
    /// </summary>
    public class DisplayNameMigrationTests
    {
        private const string MenuPath = "SpellBook/Migrar assets do GAS (3.0)";
        private const string TempFolder = "Assets/__DisplayNameMigrationTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void LegacyYaml_AbilityDisplayName_LoadsAsText()
        {
            GameplayAbility ability = LoadLegacy<GameplayAbility>("GA_Legacy", "  DisplayName: Bola de Fogo\n");

            AssertConverted(ability, ability.DisplayName, "Bola de Fogo");
        }

        [Test]
        public void LegacyYaml_AttributeDisplayName_LoadsAsText()
        {
            AttributeDefinition attribute = LoadLegacy<AttributeDefinition>("ATR_Legacy", "  DisplayName: Vida\n");

            AssertConverted(attribute, attribute.DisplayName, "Vida");
        }

        [Test]
        public void LegacyYaml_EffectDisplayName_LoadsAsText()
        {
            GameplayEffect effect = LoadLegacy<GameplayEffect>("GE_Legacy", "  displayName: Veneno\n");

            AssertConverted(effect, effect.DisplayName, "Veneno");
        }

        [Test]
        public void LegacyYaml_CategoryDisplayName_LoadsAsText()
        {
            AttributeCategory category = LoadLegacy<AttributeCategory>("CAT_Legacy", "  DisplayName: Ofensivo\n");

            AssertConverted(category, category.DisplayName, "Ofensivo");
        }

        [Test]
        public void NewAsset_SavesWithoutLegacy()
        {
            var ability = ScriptableObject.CreateInstance<GameplayAbility>();
            ability.DisplayName = new LocalizedText("gas.ability.fireball", "Bola de Fogo");
            EnsureTempFolder();
            string path = TempFolder + "/GA_New.asset";

            AssetDatabase.CreateAsset(ability, path);
            AssetDatabase.SaveAssets();
            string yaml = File.ReadAllText(path);

            Assert.That(yaml, Does.Contain("  localizedName:\n    key: gas.ability.fireball\n    text: Bola de Fogo\n"));
            Assert.That(yaml, Does.Contain("  legacyDisplayName: \n"));
            Assert.That(yaml, Does.Not.Contain("\n  DisplayName:"));
        }

        [Test]
        public void Migrate_RewritesAbilitiesAttributesCategories_Idempotent()
        {
            GameplayAbility ability = LoadLegacy<GameplayAbility>("GA_Migrate", "  DisplayName: Bola de Fogo\n");
            AttributeDefinition attribute = LoadLegacy<AttributeDefinition>("ATR_Migrate", "  DisplayName: Vida\n");
            AttributeCategory category = LoadLegacy<AttributeCategory>("CAT_Migrate", "  DisplayName: Ofensivo\n");
            string[] paths =
            {
                AssetDatabase.GetAssetPath(ability), AssetDatabase.GetAssetPath(attribute), AssetDatabase.GetAssetPath(category),
            };
            string[] texts = { "Bola de Fogo", "Vida", "Ofensivo" };

            bool ran = EditorApplication.ExecuteMenuItem(MenuPath);
            string[] first = Array.ConvertAll(paths, File.ReadAllText);
            EditorApplication.ExecuteMenuItem(MenuPath);
            string[] second = Array.ConvertAll(paths, File.ReadAllText);

            Assert.That(ran, Is.True, "o menu de migração não existe");
            for (int i = 0; i < paths.Length; i++)
            {
                Assert.That(first[i], Does.Not.Contain("\n  DisplayName:"), paths[i]);
                Assert.That(first[i], Does.Contain($"  localizedName:\n    key: \n    text: {texts[i]}\n"), paths[i]);
                Assert.That(first[i], Does.Contain("  legacyDisplayName: \n"), paths[i]);
                Assert.That(second[i], Is.EqualTo(first[i]), paths[i]);
            }
        }

        [Test]
        public void DisplayName_Resolve_KeyThroughProvider()
        {
            var attribute = ScriptableObject.CreateInstance<AttributeDefinition>();
            var category = ScriptableObject.CreateInstance<AttributeCategory>();
            var provider = new KeyTextProvider("gas.attribute.health", "Health");
            try
            {
                attribute.DisplayName = new LocalizedText("gas.attribute.health", "Vida");
                category.DisplayName = new LocalizedText("gas.category.offense", "Ofensivo");

                Assert.That(attribute.DisplayName.Resolve(provider), Is.EqualTo("Health"));
                Assert.That(attribute.DisplayName.Resolve(null), Is.EqualTo("Vida"));
                Assert.That(category.DisplayName.Resolve(provider), Is.EqualTo("Ofensivo"), "chave ausente: texto padrão");
            }
            finally
            {
                Object.DestroyImmediate(attribute);
                Object.DestroyImmediate(category);
            }
        }

        private static void AssertConverted(Object asset, LocalizedText displayName, string expected)
        {
            Assert.That(displayName.Text, Is.EqualTo(expected));
            Assert.That(displayName.Key, Is.Null.Or.Empty);
            Assert.That(new SerializedObject(asset).FindProperty("legacyDisplayName").stringValue, Is.Null.Or.Empty,
                "o legado volta a vazio depois da conversão");
        }

        /// <summary>Grava um asset do tipo no YAML 2.x, só com os campos dados, e importa.</summary>
        private static T LoadLegacy<T>(string name, string fields) where T : ScriptableObject
        {
            EnsureTempFolder();
            var probe = ScriptableObject.CreateInstance<T>();
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(probe)));
            Object.DestroyImmediate(probe);

            string path = $"{TempFolder}/{name}.asset";
            string yaml =
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 0}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n" +
                $"  m_Name: {name}\n" +
                "  m_EditorClassIdentifier: \n" +
                fields;
            File.WriteAllText(path, yaml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
        }

        /// <summary>Provedor de uma chave só, em cultura invariante.</summary>
        private sealed class KeyTextProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public KeyTextProvider(string key, string text)
            {
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture => CultureInfo.InvariantCulture;

            public event Action Changed { add { } remove { } }

            public string GetText(string key, string fallback)
            {
                return key == _key ? _text : fallback;
            }
        }
    }
}
