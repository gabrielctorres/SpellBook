using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="EffectEventData"/>: eventos de efeito do GAS.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Effect Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class EffectEventChannel : EventChannel<EffectEventData>
    {
    }
}
