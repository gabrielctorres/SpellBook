using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="AttributeChangedData"/>: mudança de atributo do GAS.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Attribute Changed Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class AttributeChangedEventChannel : EventChannel<AttributeChangedData>
    {
    }
}
