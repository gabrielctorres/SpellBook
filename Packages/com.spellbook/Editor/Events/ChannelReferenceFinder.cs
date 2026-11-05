using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
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
    /// Find references de canal (CA24): a busca do <see cref="ReferenceFinder"/> nos prefabs, nos assets e nas cenas
    /// abertas, com o papel de cada referência. Cenas fechadas não são abertas nem lidas (I36).
    /// </summary>
    internal static class ChannelReferenceFinder
    {
        /// <summary>Uma entrada por objeto e papel, primeiro os assets e depois as cenas abertas.</summary>
        public static List<ChannelReference> Find(EventChannel channel)
        {
            var result = new List<ChannelReference>();
            if (channel == null) return result;

            foreach (ReferenceUser user in ReferenceFinder.FindUsers(channel))
            {
                using var serialized = new SerializedObject(user.Owner);
                var reference = new ChannelReference(user.Owner, RoleOf(serialized.FindProperty(user.PropertyPath)), user.Location);
                if (!Contains(result, reference))
                {
                    result.Add(reference);
                }
            }

            return result;
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
