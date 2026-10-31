using System;
using UnityEngine;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Dono do campo <c>[SerializeReference]</c> da ida e volta de <see cref="EventEmission"/>. Fica em arquivo próprio
    /// para a Unity associar o script ao <c>ScriptableObject</c>.
    /// </summary>
    internal class EmissionHolder : ScriptableObject
    {
        [SerializeReference] internal EventEmission emission;
    }

    /// <summary>Payload próprio da ida e volta: o genérico fechado num struct que não é da Unity.</summary>
    [Serializable]
    public struct EmissionTestPayload
    {
        public int Amount;
        public string Label;
    }
}
