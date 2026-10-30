using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <c>bool</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/Bool Channel")]
    [HelpURL(DocUrls.Events + "#booleventchannel")]
    public sealed class BoolEventChannel : EventChannel<bool>
    {
    }
}
