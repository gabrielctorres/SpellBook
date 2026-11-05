using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Editor
{
    /// <summary>Um campo que aponta para o alvo da busca: o objeto dono do campo, o caminho do campo e onde o dono está.</summary>
    public readonly struct ReferenceUser
    {
        /// <summary>Objeto que tem o campo: um asset, um componente de prefab ou um componente de cena aberta.</summary>
        public readonly Object Owner;

        /// <summary>Caminho do campo no <see cref="SerializedObject"/> do dono, também dentro de <c>[SerializeReference]</c>.</summary>
        public readonly string PropertyPath;

        /// <summary>Caminho do asset do dono ou, numa cena aberta, o caminho da cena (o nome, se ela não foi salva).</summary>
        public readonly string Location;

        public ReferenceUser(Object owner, string propertyPath, string location)
        {
            Owner = owner;
            PropertyPath = propertyPath;
            Location = location;
        }
    }

    /// <summary>
    /// Busca de referências entre objetos do projeto, usada pelo Find References dos canais e pelo gerenciador do GAS.
    /// Varre os <c>.prefab</c> e os <c>.asset</c> que dependem do alvo e os objetos das cenas abertas. Cenas fechadas não
    /// são abertas nem lidas.
    /// </summary>
    public static class ReferenceFinder
    {
        /// <summary>
        /// Quem usa o alvo: uma entrada por campo que aponta para ele, primeiro nos assets e depois nas cenas abertas. Um
        /// alvo que não é asset só é procurado nas cenas.
        /// </summary>
        public static List<ReferenceUser> FindUsers(Object target)
        {
            var result = new List<ReferenceUser>();
            if (target == null) return result;

            string targetPath = AssetDatabase.GetAssetPath(target);
            if (targetPath.Length > 0)
            {
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    if (path != targetPath && IsScannedAsset(path) && DependsOn(path, targetPath))
                    {
                        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        {
                            ScanUsers(asset, target, path, result);
                        }
                    }
                }
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                string location = scene.path.Length > 0 ? scene.path : scene.name;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Component component in root.GetComponentsInChildren<Component>(true))
                    {
                        ScanUsers(component, target, location, result);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// O que o objeto usa: os objetos dos campos dele, também dentro de <c>[SerializeReference]</c>, uma vez cada e na
        /// ordem dos campos. Ficam fora o próprio objeto e o script dele.
        /// </summary>
        public static List<Object> FindUses(Object source)
        {
            var result = new List<Object>();
            if (source == null) return result;

            using var serialized = new SerializedObject(source);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

                Object value = property.objectReferenceValue;
                if (value != null && value != source && !(value is MonoScript) && !result.Contains(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        /// <summary>Seleciona o objeto e o destaca no Project window ou na Hierarchy.</summary>
        public static void Reveal(Object target)
        {
            if (target != null)
            {
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
        }

        private static bool IsScannedAsset(string path)
        {
            return path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static bool DependsOn(string path, string dependency)
        {
            return Array.IndexOf(AssetDatabase.GetDependencies(path, false), dependency) >= 0;
        }

        // Percorre os campos serializados do dono, inclusive os [SerializeReference], atrás do alvo.
        private static void ScanUsers(Object owner, Object target, string location, List<ReferenceUser> result)
        {
            if (owner == null || owner is GameObject) return;

            using var serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == target)
                {
                    result.Add(new ReferenceUser(owner, property.propertyPath, location));
                }
            }
        }
    }
}
