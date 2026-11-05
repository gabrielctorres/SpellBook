using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// O <see cref="GasIndex"/> lido do projeto pelo <see cref="AssetDatabase"/> (ME7, CA7). O índice é remontado
    /// depois que um <c>.asset</c> é importado, removido ou movido (a renomeação é um movimento), e o
    /// <see cref="Changed"/> avisa a janela para ela se atualizar sem reabrir. Chame <see cref="Dispose"/> ao fechar.
    /// </summary>
    internal sealed class GasProjectIndex : IDisposable
    {
        private static readonly string[] TypeFilters =
        {
            "t:GameplayAbility", "t:GameplayEffect", "t:AttributeDefinition", "t:GameplayTagDefinition"
        };

        private readonly string[] _folders;
        private GasIndex _index;

        /// <param name="folders">Pastas lidas; sem pastas, o projeto inteiro.</param>
        public GasProjectIndex(params string[] folders)
        {
            _folders = folders != null && folders.Length > 0 ? folders : null;
            GasIndexPostprocessor.AssetsChanged += OnAssetsChanged;
        }

        /// <summary>Um asset mudou; o próximo <see cref="Index"/> já vem remontado.</summary>
        public event Action Changed;

        public GasIndex Index => _index ??= GasIndex.Build(LoadAssets());

        public void Dispose()
        {
            GasIndexPostprocessor.AssetsChanged -= OnAssetsChanged;
            Changed = null;
        }

        private void OnAssetsChanged()
        {
            _index = null;
            Changed?.Invoke();
        }

        private IEnumerable<Object> LoadAssets()
        {
            var seen = new HashSet<string>();
            foreach (string filter in TypeFilters)
            {
                string[] guids = _folders == null
                    ? AssetDatabase.FindAssets(filter)
                    : AssetDatabase.FindAssets(filter, _folders);
                foreach (string guid in guids)
                {
                    if (!seen.Add(guid)) continue;
                    yield return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                }
            }
        }
    }

    /// <summary>Avisa os <see cref="GasProjectIndex"/> quando um <c>.asset</c> é importado, removido ou movido.</summary>
    internal sealed class GasIndexPostprocessor : AssetPostprocessor
    {
        public static event Action AssetsChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (AssetsChanged == null) return;
            if (HasAsset(importedAssets) || HasAsset(deletedAssets) || HasAsset(movedAssets)) AssetsChanged();
        }

        private static bool HasAsset(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
