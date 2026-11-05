using System;
using System.IO;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Nova tag filha do gerenciador (CA13): a tag é um asset com o nome inteiro (<c>Ability.IceBolt.Charged</c>) e o
    /// pai preenchido (Q7), gravado na pasta do pai. O nome começa pelo do pai, e mudar o pai não é oferecido. Sem UI.
    /// </summary>
    internal static class GasChildTag
    {
        /// <summary>O começo do nome de toda filha: o nome do pai e o ponto (<c>Ability.</c>).</summary>
        public static string Prefix(GameplayTagDefinition parent) => parent.name + ".";

        /// <summary>A mensagem que impede criar a filha com este nome; <c>null</c> se pode.</summary>
        public static string Problem(GameplayTagDefinition parent, string name)
        {
            string prefix = Prefix(parent);
            if (name == null || !name.StartsWith(prefix, StringComparison.Ordinal)) return $"Comece o nome por {prefix}: a tag nova é filha de {parent.name}.";
            string problem = AbilitySetPlan.CheckName(name.Substring(prefix.Length)) ?? AbilitySetPlan.CheckFolder(FolderOf(parent));
            if (problem != null) return problem;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayTagDefinition)))
            {
                if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == name) return $"Já existe a tag {name}. Escolha outro nome.";
            }

            return File.Exists(PathOf(parent, name)) ? $"Já existe um asset {name} nesta pasta. Escolha outro nome." : null;
        }

        /// <summary>Grava a filha na pasta do pai e a devolve.</summary>
        /// <exception cref="InvalidOperationException">O nome tem um problema.</exception>
        public static GameplayTagDefinition Create(GameplayTagDefinition parent, string name)
        {
            string problem = Problem(parent, name);
            if (problem != null) throw new InvalidOperationException(problem);
            var tag = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("parent").objectReferenceValue = parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            tag.name = name;
            AssetDatabase.CreateAsset(tag, PathOf(parent, name));
            AssetDatabase.SaveAssets();
            return tag;
        }

        private static string FolderOf(GameplayTagDefinition parent) => Path.GetDirectoryName(AssetDatabase.GetAssetPath(parent))?.Replace('\\', '/');

        private static string PathOf(GameplayTagDefinition parent, string name) => $"{FolderOf(parent)}/{name}.asset";
    }
}
