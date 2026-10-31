using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.Pooling.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do Pooling (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(PooledSpawnerAsset))]
    [CanEditMultipleObjects]
    internal sealed class PooledSpawnerAssetEditor : SpellBookEditor { }

    [CustomEditor(typeof(PoolManager))]
    [CanEditMultipleObjects]
    internal sealed class PoolManagerEditor : SpellBookEditor { }

    [CustomEditor(typeof(PoolConfiguration))]
    [CanEditMultipleObjects]
    internal sealed class PoolConfigurationEditor : SpellBookEditor { }

    [CustomEditor(typeof(PoolableObject))]
    [CanEditMultipleObjects]
    internal sealed class PoolableObjectEditor : SpellBookEditor { }

    [CustomEditor(typeof(ComponentPooler))]
    [CanEditMultipleObjects]
    internal sealed class ComponentPoolerEditor : SpellBookEditor { }
}
