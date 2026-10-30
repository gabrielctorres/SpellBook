using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Escreve uma mensagem no console, sem prefixo, com o <see cref="EventListener"/> como contexto.</summary>
    [Serializable, TypeCategory("Depuração", Summary = "Escreve uma mensagem no console.")]
    public sealed class LogReaction : Reaction
    {
        [SerializeField, Tooltip("Texto escrito no console.")]
        private string message;

        [SerializeField, Tooltip("Tipo da entrada no console.")]
        private LogType type = LogType.Log;

        public override void Execute(EventListener listener)
        {
            Debug.unityLogger.Log(type, (object)message, listener);
        }

        public override void Validate(ValidationResult result)
        {
            if (string.IsNullOrEmpty(message)) result.AddError("Log sem mensagem: preencha o Message.");
        }
    }
}
