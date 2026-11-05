using System;
using System.Collections.Generic;
using System.IO;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>O que uma criação gravou: os caminhos, na ordem, e o asset principal.</summary>
    public sealed class CreatedSet
    {
        internal CreatedSet(IReadOnlyList<string> paths, Object main) => (Paths, Main) = (paths, main);

        /// <summary>Assets e tags novos. As tags reutilizadas ficam fora.</summary>
        public IReadOnlyList<string> Paths { get; }

        public Object Main { get; }
    }

    /// <summary>
    /// Grava um <see cref="AbilitySetPlan"/>: cria as tags novas, copia os modelos e liga as cópias entre si e às tags,
    /// também dentro das listas <c>[SerializeReference]</c>. Os modelos não mudam, e o que não está no plano, como os
    /// prefabs, continua apontando para o original. Se um passo falha, nada do conjunto fica no projeto.
    /// </summary>
    public static class AbilitySetCreator
    {
        /// <exception cref="InvalidOperationException">O plano tem um problema, ou um caminho foi ocupado depois do plano.</exception>
        public static CreatedSet Create(AbilitySetPlan plan)
        {
            if (!plan.CanCreate) throw new InvalidOperationException(plan.Problem);
            if (plan.Assets.Count == 0) throw new InvalidOperationException(AbilitySetPlan.NoAssets);

            var created = new List<string>();
            var map = new Dictionary<Object, Object>();
            var copies = new List<ScriptableObject>();
            try
            {
                var tags = new Dictionary<string, GameplayTagDefinition>();
                foreach (PlannedTag planned in plan.Tags)
                {
                    GameplayTagDefinition tag = planned.Existing;
                    if (tag == null)
                    {
                        tag = ScriptableObject.CreateInstance<GameplayTagDefinition>();
                        var serialized = new SerializedObject(tag);
                        serialized.FindProperty("parent").objectReferenceValue = planned.ParentName != null ? tags[planned.ParentName] : null;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        Write(tag, planned.Name, planned.Path, created);
                    }

                    tags[planned.Name] = tag;
                    if (planned.Source != null) map[planned.Source] = tag;
                }

                foreach (PlannedAsset planned in plan.Assets)
                {
                    ScriptableObject copy = Object.Instantiate(planned.Source);
                    Write(copy, planned.Name, planned.Path, created);
                    map[planned.Source] = copy;
                    copies.Add(copy);
                }

                foreach (ScriptableObject copy in copies) Relink(copy, map);
                AssetDatabase.SaveAssets();
                Selection.activeObject = copies[0];
                return new CreatedSet(created, copies[0]);
            }
            catch
            {
                Delete(new CreatedSet(created, null));
                throw;
            }
        }

        /// <summary>Apaga os assets e as tags que a criação gravou. As tags reutilizadas ficam.</summary>
        public static void Delete(CreatedSet set)
        {
            for (int i = set.Paths.Count - 1; i >= 0; i--) AssetDatabase.DeleteAsset(set.Paths[i]);
        }

        private static void Write(Object asset, string assetName, string path, List<string> created)
        {
            if (File.Exists(path)) throw new InvalidOperationException($"Já existe um asset {assetName} nesta pasta. Escolha outro nome.");
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            created.Add(path);
        }

        // Troca, em todo campo de objeto da cópia, o modelo pela cópia e a tag-modelo pela tag do conjunto.
        private static void Relink(ScriptableObject copy, Dictionary<Object, Object> map)
        {
            var serialized = new SerializedObject(copy);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null
                    && map.TryGetValue(property.objectReferenceValue, out Object replacement))
                {
                    property.objectReferenceValue = replacement;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
