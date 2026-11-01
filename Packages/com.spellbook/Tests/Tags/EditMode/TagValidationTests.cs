using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Tags.Tests
{
    /// <summary>Validação do conjunto e da consulta pelo dono que repassa a chamada (R16, CA19).</summary>
    public class TagValidationTests
    {
        private TagTestFactory _factory;
        private TagValidationHolder _holder;
        private GameplayTagDefinition _state;
        private GameplayTagDefinition _stunned;
        private GameplayTagDefinition _immune;

        [SetUp]
        public void SetUp()
        {
            _factory = new TagTestFactory();
            _state = _factory.Create("State");
            _stunned = _factory.Create("State.Stunned", _state);
            _immune = _factory.Create("State.Immune", _state);
            _holder = ScriptableObject.CreateInstance<TagValidationHolder>();
            _holder.Query.All.Add(new GameplayTag(_stunned));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
            _factory.Dispose();
        }

        [Test]
        public void EmptyTagInContainer_IsError_WithPropertyPath()
        {
            SetSerializedTags("tags.tags", _stunned, null);

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Is.EqualTo("Tag vazia em tags (posição 1). Escolha uma tag ou remova a linha."));
            Assert.That(message.PropertyPath, Is.EqualTo("tags.tags.Array.data[1]"));
        }

        [Test]
        public void DuplicateTagInContainer_IsWarning()
        {
            SetSerializedTags("tags.tags", _stunned, _immune, _stunned, _stunned);

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.Message, Is.EqualTo("Tag 'State.Stunned' repetida em tags. Remova a repetição."));
            Assert.That(message.PropertyPath, Is.EqualTo("tags.tags.Array.data[2]"));
        }

        [Test]
        public void SameTagInTwoQueryLists_IsError()
        {
            _holder.Query.None.Add(new GameplayTag(_stunned));

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Is.EqualTo("Tag 'State.Stunned' está em Todas e Nenhuma de query."));
            Assert.That(message.PropertyPath, Is.EqualTo("query"));
        }

        [Test]
        public void ParentInAllChildInNone_IsNotReported()
        {
            _holder.Query.All.Clear();
            _holder.Query.All.Add(new GameplayTag(_state));
            _holder.Query.None.Add(new GameplayTag(_stunned));

            Assert.That(Validate(), Is.Empty);
        }

        [Test]
        public void EmptyQuery_IsWarning()
        {
            _holder.Query.All.Clear();

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.Message, Is.EqualTo("A consulta query está vazia e casa com qualquer conjunto."));
            Assert.That(message.PropertyPath, Is.EqualTo("query"));
        }

        [Test]
        public void EmptyTagInQueryList_UsesListPath()
        {
            SetSerializedTags("query.any.tags", (GameplayTagDefinition)null);

            ValidationMessage message = Validate().Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Is.EqualTo("Tag vazia em query.any (posição 0). Escolha uma tag ou remova a linha."));
            Assert.That(message.PropertyPath, Is.EqualTo("query.any.tags.Array.data[0]"));
        }

        [Test]
        public void ProjectValidator_ReportsOwnerAsContext()
        {
            SetSerializedTags("tags.tags", (GameplayTagDefinition)null);
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { _holder }, messages);

            ValidationMessage message = messages.Single();
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Context, Is.SameAs(_holder));
            Assert.That(message.PropertyPath, Is.EqualTo("tags.tags.Array.data[0]"));
        }

        private IReadOnlyList<ValidationMessage> Validate()
        {
            var result = new ValidationResult(_holder);
            _holder.Validate(result);
            return result.Messages;
        }

        // Grava a lista direto no dado serializado, como o Inspector, para ter linhas vazias e repetidas que o Add recusa.
        private void SetSerializedTags(string listPath, params GameplayTagDefinition[] definitions)
        {
            using (var serialized = new SerializedObject(_holder))
            {
                SerializedProperty list = serialized.FindProperty(listPath);
                list.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("definition").objectReferenceValue = definitions[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    /// <summary>Dono de teste: um conjunto e uma consulta, com o repasse da validação.</summary>
    internal sealed class TagValidationHolder : ScriptableObject, IValidatable
    {
        [SerializeField] private GameplayTagContainer tags = new GameplayTagContainer();
        [SerializeField] private GameplayTagQuery query = new GameplayTagQuery();

        public GameplayTagQuery Query => query;

        public void Validate(ValidationResult result)
        {
            tags.Validate(result, nameof(tags));
            query.Validate(result, nameof(query));
        }
    }
}
