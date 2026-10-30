using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <c>int</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/Int Channel")]
    [HelpURL(DocUrls.Events + "#inteventchannel")]
    public sealed class IntEventChannel : EventChannel<int>
    {
    }
}
