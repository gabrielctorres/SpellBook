using System;
using System.Collections;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Dispara um canal de eventos uma vez e termina no mesmo quadro. O payload é o fixo da emissão ou, com outra
    /// <see cref="GasPayloadSource"/>, um valor lido do <see cref="AbilityContext"/> na hora. O ASC que conjurou aparece
    /// como quem disparou. Sem emissão, sem canal, com fonte incompatível com o canal ou sem valor para enviar (sem
    /// conjurador, sem o atributo, sem alvo válido), não dispara nada, e a validação acusa o que é configuração.
    /// </summary>
    [Serializable, TypeCategory("Eventos", Summary = "Dispara um canal de eventos com um payload fixo ou da habilidade.")]
    public sealed class RaiseEventAction : AbilityAction
    {
        [SerializeReference, Tooltip("Canal disparado e payload enviado.")]
        private EventEmission emission;

        [SerializeField, Tooltip("De onde vem o payload. Fixed usa o payload da emissão; as outras fontes o ignoram.")]
        private GasPayloadSource payloadSource;

        [SerializeField, Tooltip("Atributo do conjurador lido pela fonte AttributeValue.")]
        private AttributeDefinition attribute;

        public override IEnumerator Execute(AbilityContext context)
        {
            if (emission == null) yield break;

            if (payloadSource == GasPayloadSource.Fixed)
            {
                emission.Raise(context.Source);
            }
            else if (emission.Channel != null && GasPayloadSourceUtility.Supports(payloadSource, emission.PayloadType))
            {
                RaiseDynamic(context);
            }
        }

        public override void Validate(ValidationResult result)
        {
            if (emission == null)
            {
                result.AddError("RaiseEventAction sem emissão: escolha o canal.");
                return;
            }

            emission.Validate(result);

            if (emission.Channel != null && !GasPayloadSourceUtility.Supports(payloadSource, emission.PayloadType))
            {
                result.AddError(
                    $"RaiseEventAction: a fonte {payloadSource} não serve para um canal de {emission.PayloadType.Name}.",
                    nameof(payloadSource));
            }

            if (payloadSource == GasPayloadSource.AttributeValue && attribute == null)
            {
                result.AddError("RaiseEventAction com fonte AttributeValue sem atributo: escolha o atributo.", nameof(attribute));
            }
        }

        // Só é chamado com a fonte compatível com o canal, então cada cast abaixo acerta o tipo.
        private void RaiseDynamic(AbilityContext context)
        {
            AbilitySystemComponent source = context.Source;
            switch (payloadSource)
            {
                case GasPayloadSource.AttributeValue:
                    if (source != null && attribute != null && source.TryGetAttributeValue(attribute, out float value))
                    {
                        ((EventChannel<float>)emission.Channel).Raise(value, source);
                    }

                    break;
                case GasPayloadSource.AbilityName:
                    if (context.Ability != null)
                    {
                        ((EventChannel<string>)emission.Channel).Raise(context.Ability.name, source);
                    }

                    break;
                case GasPayloadSource.SourceObject:
                    if (source != null)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(source.gameObject, source);
                    }

                    break;
                case GasPayloadSource.TargetObject:
                    List<AbilitySystemComponent> targets = context.GetValidTargets();
                    if (targets.Count > 0)
                    {
                        ((EventChannel<GameObject>)emission.Channel).Raise(targets[0].gameObject, source);
                    }

                    break;
                case GasPayloadSource.EventData:
                    var data = new AbilityEventData
                    {
                        Owner = source,
                        Ability = context.Ability,
                        Level = context.Level,
                        Cancelled = context.IsCancelled
                    };
                    ((EventChannel<AbilityEventData>)emission.Channel).Raise(data, source);
                    break;
            }
        }
    }
}
