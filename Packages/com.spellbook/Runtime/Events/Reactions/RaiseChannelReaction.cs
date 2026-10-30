using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Dispara outro canal. Em <see cref="Mode.ForwardPayload"/>, repassa o payload recebido a um canal do mesmo tipo
    /// (de outro tipo, não dispara). Em <see cref="Mode.RaiseVoid"/>, dispara um <see cref="VoidEventChannel"/>.
    /// </summary>
    [Serializable, TypeCategory("Eventos", Summary = "Dispara outro canal, repassando ou não o payload.")]
    public sealed class RaiseChannelReaction : Reaction
    {
        /// <summary>Forma do disparo.</summary>
        public enum Mode
        {
            ForwardPayload,
            RaiseVoid
        }

        [SerializeField, Tooltip("ForwardPayload repassa o payload a um canal do mesmo tipo; RaiseVoid dispara um canal Void.")]
        private Mode mode;

        [SerializeField, Tooltip("Canal disparado.")]
        private EventChannel target;

        public override void Execute(EventListener listener)
        {
            if (mode == Mode.RaiseVoid && target is VoidEventChannel channel)
            {
                channel.Raise(default, listener);
            }
        }

        public override void Execute<T>(EventListener listener, in T payload)
        {
            if (mode == Mode.RaiseVoid)
            {
                Execute(listener);
            }
            else if (target is EventChannel<T> channel)
            {
                channel.Raise(payload, listener);
            }
        }

        public override void Validate(ValidationResult result)
        {
            if (target == null)
            {
                result.AddError("RaiseChannel sem canal: escolha o Target.");
            }
            else if (mode == Mode.RaiseVoid && !(target is VoidEventChannel))
            {
                result.AddError("RaiseChannel em RaiseVoid precisa de um canal Void: troque o Target ou use ForwardPayload.");
            }
        }

        public override void Validate(ValidationResult result, Type payloadType)
        {
            Validate(result);
            if (mode == Mode.ForwardPayload && target != null && target.PayloadType != payloadType)
            {
                result.AddError($"RaiseChannel em ForwardPayload precisa de um canal de {payloadType?.Name}, não de {target.PayloadType.Name}.");
            }
        }
    }
}
