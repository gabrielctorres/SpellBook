using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <c>float</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/Float Channel")]
    [HelpURL(DocUrls.Events + "#floateventchannel")]
    public sealed class FloatEventChannel : EventChannel<float>
    {
    }
}
