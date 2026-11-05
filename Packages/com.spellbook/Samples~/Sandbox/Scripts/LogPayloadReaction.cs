using System;
using System.Globalization;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Escreve no Console uma mensagem com o payload do disparo, com o <see cref="EventListener"/> como contexto.
    /// A <see cref="LogReaction"/> do pacote escreve uma mensagem fixa.
    /// </summary>
    [Serializable, TypeCategory("Depuração", Summary = "Writes a message with the payload.")]
    public sealed class LogPayloadReaction : Reaction
    {
        [SerializeField, Tooltip("Message format. {0} is the payload.")]
        private string format = "Damage taken: {0:0.#}";

        [SerializeField, Tooltip("Console entry type.")]
        private LogType type = LogType.Log;

        public override void Execute<T>(EventListener listener, in T payload)
        {
            Debug.unityLogger.Log(type, (object)string.Format(CultureInfo.InvariantCulture, format, payload), listener);
        }

        public override void Validate(ValidationResult result)
        {
            if (string.IsNullOrEmpty(format)) result.AddError("Log has no format: fill in Format.");
        }
    }
}
