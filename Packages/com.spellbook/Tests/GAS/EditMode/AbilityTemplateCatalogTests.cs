using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Lista de modelos do assistente (menu de Editor do GAS, ME1): um modelo criado no projeto aparece sem código
    /// (CA5), e um modelo com asset modelo apagado sai da lista e vai para os quebrados (CA1).
    /// </summary>
    public class AbilityTemplateCatalogTests
    {
        private const string TempFolder = "Assets/__AbilityTemplateCatalogTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "__AbilityTemplateCatalogTests");
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void Find_ProjectTemplate_IsAvailable()
        {
            AbilityTemplate template = CreateTemplateAsset("Modelo do teste");
            var available = new List<AbilityTemplate>();
            var broken = new List<AbilityTemplate>();

            AbilityTemplateCatalog.Find(available, broken);

            Assert.That(available, Has.Member(template));
            Assert.That(broken, Has.No.Member(template));
        }

        [Test]
        public void Find_TemplateWithDeletedSource_IsBroken()
        {
            AbilityTemplate template = CreateTemplateAsset("Modelo quebrado");
            AssetDatabase.DeleteAsset(TempFolder + "/GA_Source.asset");
            var available = new List<AbilityTemplate>();
            var broken = new List<AbilityTemplate>();

            AbilityTemplateCatalog.Find(available, broken);

            Assert.That(broken, Has.Member(template));
            Assert.That(available, Has.No.Member(template));
        }

        // Grava no projeto um modelo com uma habilidade modelo, também gravada, como um time faria pelo menu Create.
        private static AbilityTemplate CreateTemplateAsset(string templateName)
        {
            var source = ScriptableObject.CreateInstance<GameplayAbility>();
            AssetDatabase.CreateAsset(source, TempFolder + "/GA_Source.asset");

            var template = ScriptableObject.CreateInstance<AbilityTemplate>();
            AssetDatabase.CreateAsset(template, TempFolder + "/" + templateName + ".asset");
            var serialized = new SerializedObject(template);
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.InsertArrayElementAtIndex(0);
            SerializedProperty entry = assets.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("source").objectReferenceValue = source;
            entry.FindPropertyRelative("nameRule").stringValue = "GA_{Name}";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return template;
        }
    }
}
