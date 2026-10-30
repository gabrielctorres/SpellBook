using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <c>string</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/String Channel")]
    [HelpURL(DocUrls.Events + "#stringeventchannel")]
    public sealed class StringEventChannel : EventChannel<string>
    {
    }
}
