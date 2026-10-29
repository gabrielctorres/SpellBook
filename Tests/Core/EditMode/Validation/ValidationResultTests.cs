using System;
using NUnit.Framework;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests.Validation
{
    /// <summary>Resultado de validação do <c>IValidatable</c> (ADR 0026, item 6; Spec, seção 4.2).</summary>
    public class ValidationResultTests
    {
        private GameObject _context;

        [SetUp]
        public void SetUp()
        {
            _context = new GameObject("ValidationContext");
        }

        [TearDown]
        public void TearDown()
        {
            if (_context != null)
            {
                Object.DestroyImmediate(_context);
            }
        }

        [Test]
        public void AddError_StoresSeverityMessageContextAndPath()
        {
            var result = new ValidationResult(_context);

            result.AddError("Falta o prefab.", "prefab");

            Assert.That(result.Context, Is.SameAs(_context));
            Assert.That(result.Messages, Has.Count.EqualTo(1));
            ValidationMessage message = result.Messages[0];
            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.Message, Is.EqualTo("Falta o prefab."));
            Assert.That(message.Context, Is.SameAs(_context));
            Assert.That(message.PropertyPath, Is.EqualTo("prefab"));
        }

        [Test]
        public void HasErrors_FalseWithOnlyWarnings_TrueWithError()
        {
            var result = new ValidationResult(_context);

            result.AddWarning("Lista vazia.");
            bool withWarningOnly = result.HasErrors;
            result.AddError("Falta o canal.");
            bool withError = result.HasErrors;

            Assert.That(withWarningOnly, Is.False);
            Assert.That(withError, Is.True);
        }

        [Test]
        public void AddError_EmptyMessage_Throws()
        {
            var result = new ValidationResult(_context);

            Assert.Throws<ArgumentException>(() => result.AddError(""));
            Assert.Throws<ArgumentException>(() => result.AddError(null));
            Assert.Throws<ArgumentException>(() => result.AddWarning(""));
            Assert.That(result.Messages, Is.Empty);
        }

        [Test]
        public void Messages_KeepInsertionOrder()
        {
            var result = new ValidationResult(_context);

            result.AddWarning("primeira", "a");
            result.AddError("segunda", "b");
            result.AddWarning("terceira");

            Assert.That(result.Messages, Has.Count.EqualTo(3));
            Assert.That(result.Messages[0].Message, Is.EqualTo("primeira"));
            Assert.That(result.Messages[0].Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(result.Messages[1].Message, Is.EqualTo("segunda"));
            Assert.That(result.Messages[1].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(result.Messages[2].Message, Is.EqualTo("terceira"));
            Assert.That(result.Messages[2].PropertyPath, Is.Null);
        }
    }
}
