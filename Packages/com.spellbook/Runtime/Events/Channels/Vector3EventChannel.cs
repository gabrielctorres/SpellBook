using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <see cref="Vector3"/>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/Vector3 Channel")]
    [HelpURL(DocUrls.Events + "#vector3eventchannel")]
    public sealed class Vector3EventChannel : EventChannel<Vector3>
    {
    }
}
