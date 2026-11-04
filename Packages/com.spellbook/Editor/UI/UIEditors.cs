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

    [CustomEditor(typeof(BarBinder))]
    [CanEditMultipleObjects]
    internal sealed class BarBinderEditor : SpellBookEditor { }

    [CustomEditor(typeof(TextBinder))]
    [CanEditMultipleObjects]
    internal sealed class TextBinderEditor : SpellBookEditor { }

    [CustomEditor(typeof(TagIndicatorBinder))]
    [CanEditMultipleObjects]
    internal sealed class TagIndicatorBinderEditor : SpellBookEditor { }

    [CustomEditor(typeof(FloatingTextStyle))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextStyleEditor : SpellBookEditor { }

    [CustomEditor(typeof(FloatingTextBinder))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextBinderEditor : SpellBookEditor { }
}
