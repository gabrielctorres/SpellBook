using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="AbilityEventData"/>: eventos de habilidade do GAS.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Ability Channel", fileName = "New Ability Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class AbilityEventChannel : EventChannel<AbilityEventData>
    {
    }
}
