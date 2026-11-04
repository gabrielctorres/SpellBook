using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.Localization.Editor
{
    /// <summary>Inspector de uma linha do adaptador do Unity Localization (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(LocalizationTextProvider))]
    [CanEditMultipleObjects]
    internal sealed class LocalizationTextProviderEditor : SpellBookEditor { }
}
