using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>Papel de quem referencia um canal no Find references.</summary>
    internal enum ChannelReferenceRole
    {
        /// <summary>Dispara o canal: o canal de uma <see cref="EventEmission"/> ou de uma <see cref="RaiseChannelReaction"/>.</summary>
        Emits,

        /// <summary>Ouve o canal: o canal de uma <see cref="EventResponse"/>.</summary>
        Listens,

        /// <summary>Guarda o canal num campo comum, sem emitir nem ouvir pelo Inspector.</summary>
        References,
    }

    /// <summary>Um objeto que referencia o canal, com o papel e onde ele está (caminho do asset ou da cena).</summary>
    internal readonly struct ChannelReference
    {
        public readonly Object Target;
        public readonly ChannelReferenceRole Role;
        public readonly string Location;

        public ChannelReference(Object target, ChannelReferenceRole role, string location)
        {
            Target = target;
            Role = role;
            Location = location;
        }
    }

    /// <summary>
    /// Find references de canal (CA24): varre os prefabs e os assets (<c>.prefab</c> e <c>.asset</c>) que dependem do
    /// canal e os objetos das cenas abertas. Cenas fechadas não são abertas nem lidas (I36).
    /// </summary>
    internal static class ChannelReferenceFinder
    {
        /// <summary>Uma entrada por objeto e papel, primeiro os assets e depois as cenas abertas.</summary>
        public static List<ChannelReference> Find(EventChannel channel)
        {
            var result = new List<ChannelReference>();
            if (channel == null) return result;

            string channelPath = AssetDatabase.GetAssetPath(channel);
            if (channelPath.Length > 0)
            {
                foreach (string path in AssetDatabase.GetAllAssetPaths())
                {
                    if (path != channelPath && IsScannedAsset(path) && DependsOn(path, channelPath))
                    {
                        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        {
                            Scan(asset, channel, path, result);
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
                        Scan(component, channel, location, result);
                    }
                }
            }

            return result;
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

        // Percorre os campos serializados do objeto, inclusive os [SerializeReference], atrás do canal.
        private static void Scan(Object target, EventChannel channel, string location, List<ChannelReference> result)
        {
            if (target == null || target is GameObject) return;

            using var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.GetIterator();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = property.propertyType != SerializedPropertyType.String;
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != channel)
                {
                    continue;
                }

                var reference = new ChannelReference(target, RoleOf(property), location);
                if (!Contains(result, reference))
                {
                    result.Add(reference);
                }
            }
        }

        // Papel pela referência gerenciada mais próxima que contém o campo, subindo pelo caminho.
        private static ChannelReferenceRole RoleOf(SerializedProperty property)
        {
            string path = property.propertyPath;
            for (int dot = path.LastIndexOf('.'); dot > 0; dot = path.LastIndexOf('.', dot - 1))
            {
                SerializedProperty owner = property.serializedObject.FindProperty(path.Substring(0, dot));
                if (owner == null || owner.propertyType != SerializedPropertyType.ManagedReference) continue;

                switch (owner.managedReferenceValue)
                {
                    case EventEmission _:
                    case RaiseChannelReaction _:
                        return ChannelReferenceRole.Emits;
                    case EventResponse _:
                        return ChannelReferenceRole.Listens;
                }
            }

            return ChannelReferenceRole.References;
        }

        private static bool Contains(List<ChannelReference> references, ChannelReference reference)
        {
            for (int i = 0; i < references.Count; i++)
            {
                if (references[i].Target == reference.Target && references[i].Role == reference.Role)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
