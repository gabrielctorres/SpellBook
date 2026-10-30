using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Liga, desliga ou inverte o <c>enabled</c> de um componente.</summary>
    [Serializable, TypeCategory("Objeto", Summary = "Liga, desliga ou inverte um componente.")]
    public sealed class ToggleComponentReaction : Reaction
    {
        /// <summary>O que fazer com o <c>enabled</c> do componente.</summary>
        public enum Mode
        {
            Enable,
            Disable,
            Toggle
        }

        [SerializeField, Tooltip("Componente ligado ou desligado.")]
        private Behaviour target;

        [SerializeField, Tooltip("Enable liga, Disable desliga, Toggle inverte.")]
        private Mode mode;

        public override void Execute(EventListener listener)
        {
            if (target == null) return;

            target.enabled = mode switch
            {
                Mode.Enable => true,
                Mode.Disable => false,
                _ => !target.enabled
            };
        }

        public override void Validate(ValidationResult result)
        {
            if (target == null) result.AddError("Toggle de componente sem componente: escolha o Target.");
        }
    }
}
