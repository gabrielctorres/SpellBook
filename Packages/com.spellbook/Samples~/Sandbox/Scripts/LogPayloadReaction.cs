using System;
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
    [Serializable, TypeCategory("Depuração", Summary = "Escreve uma mensagem com o payload.")]
    public sealed class LogPayloadReaction : Reaction
    {
        [SerializeField, Tooltip("Formato da mensagem. {0} é o payload.")]
        private string format = "Dano recebido: {0:0.#}";

        [SerializeField, Tooltip("Tipo da entrada no Console.")]
        private LogType type = LogType.Log;

        public override void Execute<T>(EventListener listener, in T payload)
        {
            Debug.unityLogger.Log(type, (object)string.Format(format, payload), listener);
        }

        public override void Validate(ValidationResult result)
        {
            if (string.IsNullOrEmpty(format)) result.AddError("Log sem formato: preencha o Format.");
        }
    }
}
