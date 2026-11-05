using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using UnityEditor;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Modelos do assistente de criação: todos os <see cref="AbilityTemplate"/> do projeto e dos pacotes, separados
    /// entre os disponíveis e os quebrados (com asset modelo ausente), que ficam fora da lista.
    /// </summary>
    public static class AbilityTemplateCatalog
    {
        /// <summary>Limpa as duas listas e as preenche em ordem de <see cref="AbilityTemplate.DisplayName"/>.</summary>
        public static void Find(List<AbilityTemplate> available, List<AbilityTemplate> broken)
        {
            available.Clear();
            broken.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(AbilityTemplate)))
            {
                var template = AssetDatabase.LoadAssetAtPath<AbilityTemplate>(AssetDatabase.GUIDToAssetPath(guid));
                if (template == null)
                {
                    continue;
                }

                (template.IsAvailable ? available : broken).Add(template);
            }

            Comparison<AbilityTemplate> byName = (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture);
            available.Sort(byName);
            broken.Sort(byName);
        }
    }
}
