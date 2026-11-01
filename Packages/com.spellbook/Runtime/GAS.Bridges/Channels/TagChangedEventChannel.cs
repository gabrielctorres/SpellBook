using SpellBook.Core;
using SpellBook.Events;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Canal com payload <see cref="TagChangedData"/>: tag que entrou ou saiu do ASC.</summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/Events/Tag Changed Channel")]
    [HelpURL(DocUrls.Events + "#canais-do-gas")]
    public sealed class TagChangedEventChannel : EventChannel<TagChangedData>
    {
    }
}
