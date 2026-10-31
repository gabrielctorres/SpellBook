using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Dispara um canal com um payload fixo, sem código. Chame <see cref="Raise"/> por um <c>UnityEvent</c> (o
    /// <c>OnClick</c> de um botão, por exemplo) ou por um evento de animação.
    /// </summary>
    [HelpURL(DocUrls.Events + "#eventemitter")]
    public sealed class EventEmitter : MonoBehaviour, IValidatable
    {
        [SerializeReference, Tooltip("Canal disparado e payload enviado.")]
        private EventEmission emission;

        /// <summary>Dispara o canal da emissão com o payload configurado. Sem emissão ou sem canal, não faz nada.</summary>
        public void Raise()
        {
            emission?.Raise(this);
        }

        public void Validate(ValidationResult result)
        {
            if (emission == null)
            {
                result.AddError("EventEmitter sem emissão: escolha o canal.", nameof(emission));
                return;
            }

            emission.Validate(result);
        }
    }
}
