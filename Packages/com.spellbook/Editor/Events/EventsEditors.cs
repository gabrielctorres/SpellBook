using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Inspectors de uma linha dos tipos do Events (Spec, seção 4.10, "Um editor por tipo"). O canal e o emissor usam
    /// <see cref="EventChannelEditor"/> e <see cref="EventEmitterEditor"/>.
    /// </summary>
    [CustomEditor(typeof(EventListener))]
    [CanEditMultipleObjects]
    internal sealed class EventListenerEditor : SpellBookEditor { }
}
