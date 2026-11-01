using System;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// De onde a <see cref="RaiseEventAction"/> tira o payload. A ordem é a da serialização: valores novos entram no fim.
    /// </summary>
    public enum GasPayloadSource
    {
        /// <summary>O payload fixo da emissão. Serve para qualquer canal.</summary>
        Fixed,
        /// <summary>Valor atual do atributo escolhido no conjurador. Canal Float.</summary>
        AttributeValue,
        /// <summary>Nome do asset da habilidade. Canal String.</summary>
        AbilityName,
        /// <summary>Objeto do conjurador. Canal GameObject.</summary>
        SourceObject,
        /// <summary>Objeto do primeiro alvo válido. Canal GameObject.</summary>
        TargetObject,
        /// <summary>Dados da habilidade em execução. Canal <see cref="AbilityEventData"/>.</summary>
        EventData
    }

    /// <summary>Tabela de tipos de canal aceitos por cada <see cref="GasPayloadSource"/>.</summary>
    public static class GasPayloadSourceUtility
    {
        /// <summary>
        /// A <paramref name="source"/> produz um payload do tipo <paramref name="payloadType"/>. <c>Fixed</c> aceita qualquer
        /// tipo; as outras fontes, só o tipo da tabela.
        /// </summary>
        public static bool Supports(GasPayloadSource source, Type payloadType)
        {
            switch (source)
            {
                case GasPayloadSource.Fixed:
                    return true;
                case GasPayloadSource.AttributeValue:
                    return payloadType == typeof(float);
                case GasPayloadSource.AbilityName:
                    return payloadType == typeof(string);
                case GasPayloadSource.SourceObject:
                case GasPayloadSource.TargetObject:
                    return payloadType == typeof(GameObject);
                case GasPayloadSource.EventData:
                    return payloadType == typeof(AbilityEventData);
                default:
                    return false;
            }
        }
    }
}
