using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.GAS.Bridges.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do GAS.Bridges (ADR 0026, "Um editor por tipo"; ADR 0031, item 1).</summary>
    [CustomEditor(typeof(AbilitySystemEventRelay))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySystemEventRelayEditor : SpellBookEditor { }
}
