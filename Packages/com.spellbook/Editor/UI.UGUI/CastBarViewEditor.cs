using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.UI.UGUI.Editor
{
    /// <summary>Inspector de uma linha da view da barra de cast (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(CastBarView))]
    [CanEditMultipleObjects]
    internal sealed class CastBarViewEditor : SpellBookEditor { }
}
