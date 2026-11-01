using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Regrava os <see cref="GameplayEffect"/> do projeto no formato 3.0 (ADR 0009, VN3). A conversão dos campos
    /// 2.x (<c>Duration</c> e <c>ConstantValue</c> em <c>float</c>) roda no carregamento; este menu só marca os
    /// efeitos como sujos e salva, para o YAML perder os valores legados. Rodar de novo não muda nada.
    /// </summary>
    public static class GasAssetMigration
    {
        private const string MenuPath = "SpellBook/Migrar assets do GAS (3.0)";

        [MenuItem(MenuPath)]
        private static void MigrateFromMenu()
        {
            int count = MigrateAll();
            Debug.Log($"[SpellBook] Migração do GAS (3.0): {count} GameplayEffect(s) regravado(s).");
        }

        /// <summary>
        /// Carrega e regrava todo <see cref="GameplayEffect"/> em <c>Assets/</c> e nos pacotes embutidos ou locais
        /// (os pacotes do registro são só leitura e ficam de fora). Devolve quantos efeitos foram regravados.
        /// </summary>
        public static int MigrateAll()
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayEffect)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsWritable(path)) continue;

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not GameplayEffect) continue;
                    EditorUtility.SetDirty(asset);
                    count++;
                }
            }

            AssetDatabase.SaveAssets();
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
