using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="AttributeLimitEventData"/>: atributo do GAS no mínimo ou no máximo.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Attribute Limit Channel", fileName = "New Attribute Limit Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class AttributeLimitEventChannel : EventChannel<AttributeLimitEventData>
    {
    }
}
