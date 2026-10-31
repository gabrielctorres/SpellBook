using UnityEditor;

namespace SpellBook.Core.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do Core (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(InstantiateSpawnerAsset))]
    [CanEditMultipleObjects]
    internal sealed class InstantiateSpawnerAssetEditor : SpellBookEditor { }
}
