using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Ativa ou desativa um GameObject.</summary>
    [Serializable, TypeCategory("Objeto", Summary = "Ativa ou desativa um GameObject.")]
    public sealed class SetActiveReaction : Reaction
    {
        [SerializeField, Tooltip("Objeto ativado ou desativado.")]
        private GameObject target;

        [SerializeField, Tooltip("Marcado ativa o objeto; desmarcado, desativa.")]
        private bool active;

        public override void Execute(EventListener listener)
        {
            if (target != null) target.SetActive(active);
        }

        public override void Validate(ValidationResult result)
        {
            if (target == null) result.AddError("SetActive sem GameObject: escolha o Target.");
        }
    }
}
