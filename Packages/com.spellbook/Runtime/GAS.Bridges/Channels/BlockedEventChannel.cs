using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="BlockedEventData"/>: bloqueio genérico do GAS.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Blocked Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class BlockedEventChannel : EventChannel<BlockedEventData>
    {
    }
}
