using System;
using System.Collections.Generic;

namespace SpellBook.Core.Validation
{
    /// <summary>
    /// Junta as mensagens que um <see cref="IValidatable"/> gera para um objeto (ADR 0026, item 6).
    /// As mensagens ficam na ordem em que foram adicionadas.
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly List<ValidationMessage> _messages;

        public ValidationResult(UnityEngine.Object context)
        {
            Context = context;
            _messages = new List<ValidationMessage>();
        }

        /// <summary>Objeto validado; vai em cada mensagem.</summary>
        public UnityEngine.Object Context { get; }

        /// <summary>Mensagens na ordem de inserção.</summary>
        public IReadOnlyList<ValidationMessage> Messages => _messages;

        /// <summary><c>true</c> se ao menos uma mensagem é <see cref="ValidationSeverity.Error"/>.</summary>
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < _messages.Count; i++)
                {
                    if (_messages[i].Severity == ValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Adiciona um erro. Lança <see cref="ArgumentException"/> se a mensagem for nula ou vazia.</summary>
        public void AddError(string message, string propertyPath = null)
        {
            Add(ValidationSeverity.Error, message, propertyPath);
        }

        /// <summary>Adiciona um aviso. Lança <see cref="ArgumentException"/> se a mensagem for nula ou vazia.</summary>
        public void AddWarning(string message, string propertyPath = null)
        {
            Add(ValidationSeverity.Warning, message, propertyPath);
        }

        private void Add(ValidationSeverity severity, string message, string propertyPath)
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("A mensagem de validação não pode ser nula nem vazia.", nameof(message));
            }

            _messages.Add(new ValidationMessage(severity, message, Context, propertyPath));
        }
    }
}
