using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Tests.Inspector;
using SpellBook.Core.Tests.Validation;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Validador do projeto com tipos só do Core (Spec, seção 4.9): uma exceção não para a varredura, o tipo ausente é
    /// erro sem apagar dados (CA64) e o tipo sem <c>[HelpURL]</c> é erro no script dele. As regras dos outros módulos e
    /// o host limpo ficam no <c>ProjectValidationTests</c> do <c>SpellBook.Repository.Tests.EditMode</c>.
    /// </summary>
    public class ProjectValidatorTests
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
            MissingTypeAsset.Delete();
        }

        [Test]
        public void Validator_ExceptionInValidate_BecomesErrorAndContinues()
        {
            ValidatableHolder throwing = Track(ScriptableObject.CreateInstance<ValidatableHolder>());
            throwing.throws = true;
            ValidatableHolder invalid = Track(ScriptableObject.CreateInstance<ValidatableHolder>());
            invalid.valid = false;

            List<ValidationMessage> messages = Validate(throwing, invalid);

            Assert.That(ErrorsOf(messages, throwing), Is.EqualTo(new[] { "A validação lançou InvalidOperationException: falhou" }));
            Assert.That(ErrorsOf(messages, invalid), Is.EqualTo(new[] { ValidatableHolder.InvalidMessage }));
        }

        [Test]
        public void Validator_MissingSerializeReferenceType_IsError_DataKept()
        {
            PickHolder holder = MissingTypeAsset.Create();
            string expected = MissingReferenceTypes.FormatMessage(MissingReferenceTypes.Get(holder)[0]);

            List<ValidationMessage> messages = Validate(holder);

            Assert.That(ErrorsOf(messages, holder), Is.EqualTo(new[] { expected }));
            Assert.That(expected, Does.Contain(MissingTypeAsset.Assembly).And.Contain(MissingTypeAsset.GhostClass));
            Assert.That(MissingTypeAsset.ReadFile(), Does.Contain("class: " + MissingTypeAsset.GhostClass + ",")
                .And.Contain("Value: " + MissingTypeAsset.GhostValue));
            Assert.That(MissingReferenceTypes.Get(holder).Count, Is.EqualTo(1), "o validador limpou a referência");
        }

        [Test]
        public void ValidateTypes_TypeWithoutHelpUrl_IsErrorOnItsScript()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.ValidateTypes(new[] { typeof(NoHelpUrlScriptAsset).Assembly }, messages);

            ValidationMessage[] errors = messages
                .Where(m => m.Severity == ValidationSeverity.Error && m.Message.Contains(typeof(NoHelpUrlScriptAsset).FullName))
                .ToArray();
            Assert.That(errors, Has.Length.EqualTo(1));
            Assert.That(errors[0].Message, Does.Contain(HelpUrlRule.MissingReason));
            var script = errors[0].Context as MonoScript;
            Assert.That(script, Is.Not.Null, "o alvo do erro não é o MonoScript do tipo");
            Assert.That(script.GetClass(), Is.EqualTo(typeof(NoHelpUrlScriptAsset)));
        }

        [Test]
        public void ValidateTypes_TypeWithoutScript_IsErrorWithoutTarget()
        {
            var messages = new List<ValidationMessage>();

            ProjectValidator.ValidateTypes(new[] { typeof(NoHelpUrlTestAsset).Assembly }, messages);

            ValidationMessage error = messages.Single(m => m.Message.Contains(typeof(NoHelpUrlTestAsset).FullName));
            Assert.That(error.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(error.Context, Is.Null);
        }

        private static List<ValidationMessage> Validate(params Object[] targets)
        {
            var messages = new List<ValidationMessage>();
            ProjectValidator.Validate(targets, messages);
            return messages;
        }

        private static string[] ErrorsOf(IEnumerable<ValidationMessage> messages, Object context)
        {
            return messages.Where(m => m.Severity == ValidationSeverity.Error && m.Context == context)
                .Select(m => m.Message)
                .ToArray();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
