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
        /// <summary>Valor atual do atributo escolhido no conjurador ou, numa lista do efeito, no dono do efeito. Canal Float.</summary>
        AttributeValue,
        /// <summary>Nome do asset da habilidade. Canal String. Fora das listas do efeito.</summary>
        AbilityName,
        /// <summary>Objeto do conjurador ou, numa lista do efeito, de quem aplicou o efeito. Canal GameObject.</summary>
        SourceObject,
        /// <summary>Objeto do primeiro alvo válido ou, numa lista do efeito, do dono do efeito. Canal GameObject.</summary>
        TargetObject,
        /// <summary>
        /// Dados da habilidade em execução (canal <see cref="AbilityEventData"/>) ou, numa lista do efeito, do efeito
        /// (canal <see cref="EffectEventData"/>).
        /// </summary>
        EventData,
        /// <summary>
        /// Valor do modificador de índice <c>modifierIndex</c> do efeito, no nível do efeito: de <c>PeriodicModifiers</c> no
        /// tique e de <c>Modifiers</c> nos outros eventos. Canal Float. Só nas listas do efeito.
        /// </summary>
        ModifierMagnitude,
        /// <summary>Pilhas atuais do efeito; no efeito instantâneo, 1. Canal Float. Só nas listas do efeito.</summary>
        Stacks,
        /// <summary>Nome do asset do efeito. Canal String. Só nas listas do efeito.</summary>
        EffectName
    }

    /// <summary>Tabela de tipos de canal aceitos por cada <see cref="GasPayloadSource"/>.</summary>
    public static class GasPayloadSourceUtility
    {
        /// <summary>
        /// A <paramref name="source"/> produz um payload do tipo <paramref name="payloadType"/> numa lista fora do efeito
        /// (habilidade, projétil). É o mesmo que <see cref="Supports(GasPayloadSource, Type, bool)"/> com
        /// <c>inEffectList</c> falso.
        /// </summary>
        public static bool Supports(GasPayloadSource source, Type payloadType)
        {
            return Supports(source, payloadType, false);
        }

        /// <summary>
        /// A <paramref name="source"/> produz um payload do tipo <paramref name="payloadType"/> numa lista do efeito
        /// (<paramref name="inEffectList"/>) ou fora dela. <c>Fixed</c> aceita qualquer tipo nos dois lugares;
        /// <c>ModifierMagnitude</c>, <c>Stacks</c> e <c>EffectName</c> servem só nas listas do efeito, e <c>AbilityName</c>
        /// só fora delas. <c>EventData</c> pede <see cref="EffectEventData"/> no efeito e <see cref="AbilityEventData"/> fora.
        /// </summary>
        public static bool Supports(GasPayloadSource source, Type payloadType, bool inEffectList)
        {
            switch (source)
            {
                case GasPayloadSource.Fixed:
                    return true;
                case GasPayloadSource.AttributeValue:
                    return payloadType == typeof(float);
                case GasPayloadSource.AbilityName:
                    return !inEffectList && payloadType == typeof(string);
                case GasPayloadSource.ModifierMagnitude:
                case GasPayloadSource.Stacks:
                    return inEffectList && payloadType == typeof(float);
                case GasPayloadSource.EffectName:
                    return inEffectList && payloadType == typeof(string);
                case GasPayloadSource.SourceObject:
                case GasPayloadSource.TargetObject:
                    return payloadType == typeof(GameObject);
                case GasPayloadSource.EventData:
                    return payloadType == (inEffectList ? typeof(EffectEventData) : typeof(AbilityEventData));
                default:
                    return false;
            }
        }
    }
}
