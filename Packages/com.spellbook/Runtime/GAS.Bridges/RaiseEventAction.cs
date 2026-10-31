using System;
using System.Collections;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Dispara um canal de eventos uma vez, com o payload fixo da emissão, e termina no mesmo quadro. O ASC que conjurou
    /// aparece como quem disparou. Sem emissão ou sem canal, não faz nada, e a validação acusa o erro.
    /// </summary>
    [Serializable, TypeCategory("Eventos", Summary = "Dispara um canal de eventos com um payload fixo.")]
    public sealed class RaiseEventAction : AbilityAction
    {
        [SerializeReference, Tooltip("Canal disparado e payload enviado.")]
        private EventEmission emission;

        public override IEnumerator Execute(AbilityContext context)
        {
            emission?.Raise(context.Source);
            yield break;
        }

        public override void Validate(ValidationResult result)
        {
            if (emission == null)
            {
                result.AddError("RaiseEventAction sem emissão: escolha o canal.");
                return;
            }

            emission.Validate(result);
        }
    }
}
