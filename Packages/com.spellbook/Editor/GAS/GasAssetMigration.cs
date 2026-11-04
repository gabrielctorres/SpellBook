using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Regrava os <see cref="GameplayEffect"/>, <see cref="GameplayAbility"/>, <see cref="AttributeDefinition"/> e
    /// <see cref="AttributeCategory"/> do projeto no formato 3.0 (ADR 0009, VN3, EV3d, DE5). A conversão dos campos 2.x
    /// (<c>Duration</c> e <c>ConstantValue</c> do efeito e <c>Duration</c> da <c>WaitAction</c>, em <c>float</c>, e o
    /// nome exibido em <c>string</c> dos quatro tipos) roda no carregamento; este menu só marca os assets como sujos e
    /// salva, para o YAML perder os valores legados. Rodar de novo não muda nada.
    /// </summary>
    public static class GasAssetMigration
    {
        private const string MenuPath = "SpellBook/Migrar assets do GAS (3.0)";

        [MenuItem(MenuPath)]
        private static void MigrateFromMenu()
        {
            int effects = MarkDirty<GameplayEffect>();
            int abilities = MarkDirty<GameplayAbility>();
            int attributes = MarkDirty<AttributeDefinition>();
            int categories = MarkDirty<AttributeCategory>();
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpellBook] Migração do GAS (3.0): {effects} GameplayEffect(s), {abilities} GameplayAbility(s), " +
                $"{attributes} AttributeDefinition(s) e {categories} AttributeCategory(s) regravado(s).");
        }

        /// <summary>
        /// Carrega e regrava todo <see cref="GameplayEffect"/>, <see cref="GameplayAbility"/>,
        /// <see cref="AttributeDefinition"/> e <see cref="AttributeCategory"/> em <c>Assets/</c> e nos pacotes embutidos
        /// ou locais (os pacotes do registro são só leitura e ficam de fora). Devolve a soma dos assets regravados.
        /// </summary>
        public static int MigrateAll()
        {
            int count = MarkDirty<GameplayEffect>() + MarkDirty<GameplayAbility>()
                + MarkDirty<AttributeDefinition>() + MarkDirty<AttributeCategory>();
            AssetDatabase.SaveAssets();
            return count;
        }

        private static int MarkDirty<T>() where T : Object
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsWritable(path)) continue;

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not T) continue;
                    EditorUtility.SetDirty(asset);
                    count++;
                }
            }

            return count;
        }

        private static bool IsWritable(string path)
        {
            if (path.StartsWith("Assets/")) return true;
            PackageInfo package = PackageInfo.FindForAssetPath(path);
            return package != null && (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }
    }
}
