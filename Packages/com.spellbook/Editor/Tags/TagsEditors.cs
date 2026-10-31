using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.Tags.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do Tags (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(GameplayTagDefinition))]
    [CanEditMultipleObjects]
    internal sealed class GameplayTagDefinitionEditor : SpellBookEditor { }
}
