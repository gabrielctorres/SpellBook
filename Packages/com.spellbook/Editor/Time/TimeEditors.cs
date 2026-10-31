using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.Time.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do Time (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(TimeSourceAsset), true)]
    [CanEditMultipleObjects]
    internal sealed class TimeSourceAssetEditor : SpellBookEditor { }
}
