using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Modelo de habilidade (menu de Editor do GAS, ME1; CA1 e CA5): validação, disponibilidade e Inspector.
    /// </summary>
    public class AbilityTemplateTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Validate_Complete_NoMessages()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_{Name}", "Ability.{Name}");

            ValidationResult result = Validate(template);

            Assert.That(result.Messages, Is.Empty);
        }

        [Test]
        public void Validate_NoAssets_Error()
        {
            AbilityTemplate template = CreateTemplate(null, null);

            ValidationResult result = Validate(template);

            Assert.That(result.Messages.Single().Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(result.Messages.Single().PropertyPath, Is.EqualTo("assets"));
        }

        [Test]
        public void Validate_MissingSource_ErrorOnField()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_{Name}");
            AddEntry(template, null, "GE_{Name}_Cost");

            ValidationResult result = Validate(template);

            ValidationMessage message = result.Messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("assets.Array.data[1].source"));
            Assert.That(message.Message, Does.Contain("GE_{Name}_Cost"));
        }

        [Test]
        public void Validate_NameRuleWithoutToken_ErrorOnField()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_Fixo");

            ValidationResult result = Validate(template);

            ValidationMessage message = result.Messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("assets.Array.data[0].nameRule"));
            Assert.That(message.Message, Does.Contain(AbilityTemplate.NameToken));
        }

        [Test]
        public void Validate_TagRuleWithoutToken_ErrorOnField()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_{Name}", "Ability.{Name}", "Cooldown");

            ValidationResult result = Validate(template);

            ValidationMessage message = result.Messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("tags.Array.data[1]"));
        }

        [Test]
        public void IsAvailable_FalseWhenSourceMissing()
        {
            AbilityTemplate complete = CreateTemplate(Ability(), "GA_{Name}");
            AbilityTemplate broken = CreateTemplate(Ability(), "GA_{Name}");
            AddEntry(broken, null, "GE_{Name}_Cost");
            AbilityTemplate empty = CreateTemplate(null, null);

            Assert.That(complete.IsAvailable, Is.True);
            Assert.That(broken.IsAvailable, Is.False);
            Assert.That(empty.IsAvailable, Is.False);
        }

        [Test]
        public void DisplayName_EmptyUsesAssetName()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_{Name}");
            template.name = "Projétil do projeto";

            Assert.That(template.DisplayName, Is.EqualTo("Projétil do projeto"));

            SetString(template, "displayName", "Projétil");
            Assert.That(template.DisplayName, Is.EqualTo("Projétil"));
        }

        [Test]
        public void ProjectValidator_MissingSource_ReportsTemplate()
        {
            AbilityTemplate template = CreateTemplate(null, "GA_{Name}");
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { template }, messages);

            Assert.That(messages.Single().Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(messages.Single().Context, Is.SameAs(template));
        }

        [Test]
        public void Inspector_IsSpellBookEditor()
        {
            AbilityTemplate template = CreateTemplate(Ability(), "GA_{Name}");

            var editor = UnityEditor.Editor.CreateEditor(template);
            _created.Add(editor);

            Assert.That(editor, Is.InstanceOf<SpellBookEditor>());
        }

        private GameplayAbility Ability()
        {
            var ability = ScriptableObject.CreateInstance<GameplayAbility>();
            _created.Add(ability);
            return ability;
        }

        // Monta o modelo pelos campos serializados, como o designer faz no Inspector. Sem regra de nome, sem entrada.
        private AbilityTemplate CreateTemplate(ScriptableObject source, string nameRule, params string[] tags)
        {
            var template = ScriptableObject.CreateInstance<AbilityTemplate>();
            _created.Add(template);
            if (nameRule != null)
            {
                AddEntry(template, source, nameRule);
            }

            var serialized = new SerializedObject(template);
            SerializedProperty tagList = serialized.FindProperty("tags");
            foreach (string tag in tags)
            {
                tagList.InsertArrayElementAtIndex(tagList.arraySize);
                tagList.GetArrayElementAtIndex(tagList.arraySize - 1).stringValue = tag;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private static void AddEntry(AbilityTemplate template, ScriptableObject source, string nameRule)
        {
            var serialized = new SerializedObject(template);
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.InsertArrayElementAtIndex(assets.arraySize);
            SerializedProperty entry = assets.GetArrayElementAtIndex(assets.arraySize - 1);
            entry.FindPropertyRelative("source").objectReferenceValue = source;
            entry.FindPropertyRelative("nameRule").stringValue = nameRule;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(AbilityTemplate template, string field, string value)
        {
            var serialized = new SerializedObject(template);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ValidationResult Validate(AbilityTemplate template)
        {
            var result = new ValidationResult(template);
            template.Validate(result);
            return result;
        }
    }
}
