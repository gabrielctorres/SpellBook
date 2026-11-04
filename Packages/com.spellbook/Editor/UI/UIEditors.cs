using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.UI.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do UI (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(UIUpdateDriver))]
    [CanEditMultipleObjects]
    internal sealed class UIUpdateDriverEditor : SpellBookEditor { }

    [CustomEditor(typeof(UITheme))]
    [CanEditMultipleObjects]
    internal sealed class UIThemeEditor : SpellBookEditor { }
}
