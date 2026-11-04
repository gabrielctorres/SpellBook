using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.GAS.UI.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do GAS.UI (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(FloatingTextRules))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextRulesEditor : SpellBookEditor { }

    [CustomEditor(typeof(AbilityLoadout))]
    [CanEditMultipleObjects]
    internal sealed class AbilityLoadoutEditor : SpellBookEditor { }
}
