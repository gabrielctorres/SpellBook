using System;
using System.Collections.Generic;
using UnityEditor;
using Void = SpellBook.Core.Void;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Lista de canais do seletor (Spec 4.7; CA21 e CA58). Regra do filtro: campo <c>EventChannel&lt;T&gt;</c> → canais
    /// de T; <see cref="RaiseChannelReaction"/> em <c>ForwardPayload</c> → o payload da resposta que contém a reação; em
    /// <c>RaiseVoid</c> → canais Void; qualquer outro campo do tipo base → todos os canais.
    /// </summary>
    public static class ChannelCatalog
    {
        /// <summary>Canais não nulos de <paramref name="channels"/> com payload <paramref name="payloadType"/>, na ordem dada. Com tipo nulo, todos.</summary>
        public static List<EventChannel> Filter(IEnumerable<EventChannel> channels, Type payloadType)
        {
            var result = new List<EventChannel>();
            foreach (EventChannel channel in channels)
            {
                if (channel != null && (payloadType == null || channel.PayloadType == payloadType))
                {
                    result.Add(channel);
                }
            }

            return result;
        }

        /// <summary>Todos os assets de canal do projeto, por nome.</summary>
        public static List<EventChannel> FindAll()
        {
            var result = new List<EventChannel>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EventChannel)))
            {
                var channel = AssetDatabase.LoadAssetAtPath<EventChannel>(AssetDatabase.GUIDToAssetPath(guid));
                if (channel != null)
                {
                    result.Add(channel);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        /// <summary>
        /// Payload que o campo de canal <paramref name="property"/>, declarado como <paramref name="fieldType"/> (ou lista
        /// dele), aceita pela regra do filtro. <c>null</c> quando o campo aceita canais de qualquer payload.
        /// </summary>
        public static Type PayloadTypeFor(SerializedProperty property, Type fieldType)
        {
            Type declared = PayloadOf(ElementType(fieldType), typeof(EventChannel<>));
            if (declared != null || property == null)
            {
                return declared;
            }

            SerializedProperty owner = Owner(property);
            if (owner == null || !(owner.managedReferenceValue is RaiseChannelReaction))
            {
                return null;
            }

            SerializedProperty mode = owner.FindPropertyRelative("mode");
            if (mode != null && mode.intValue == (int)RaiseChannelReaction.Mode.RaiseVoid)
            {
                return typeof(Void);
            }

            return EnclosingResponsePayload(property);
        }

        /// <summary>Tipo do elemento de um array ou <c>List&lt;T&gt;</c>, ou o próprio tipo.</summary>
        internal static Type ElementType(Type fieldType)
        {
            if (fieldType == null) return null;
            if (fieldType.IsArray) return fieldType.GetElementType();
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return fieldType;
        }

        // T de type ou da primeira base genérica igual a definition (EventChannel<T>, EventResponse<T>).
        private static Type PayloadOf(Type type, Type definition)
        {
            for (; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == definition)
                {
                    return type.GetGenericArguments()[0];
                }
            }

            return null;
        }

        // Objeto [SerializeReference] que declara o campo, ou null quando o dono é o próprio asset/componente.
        private static SerializedProperty Owner(SerializedProperty property)
        {
            string path = property.propertyPath;
            if (path.EndsWith("]", StringComparison.Ordinal))
            {
                int array = path.LastIndexOf(".Array.data[", StringComparison.Ordinal);
                if (array >= 0) path = path.Substring(0, array);
            }

            int dot = path.LastIndexOf('.');
            if (dot < 0) return null;
            SerializedProperty owner = property.serializedObject.FindProperty(path.Substring(0, dot));
            return owner != null && owner.propertyType == SerializedPropertyType.ManagedReference ? owner : null;
        }

        // Payload da EventResponse mais próxima que contém o campo, subindo pelo caminho.
        private static Type EnclosingResponsePayload(SerializedProperty property)
        {
            string path = property.propertyPath;
            for (int dot = path.LastIndexOf('.'); dot > 0; dot = path.LastIndexOf('.', dot - 1))
            {
                SerializedProperty candidate = property.serializedObject.FindProperty(path.Substring(0, dot));
                if (candidate != null && candidate.propertyType == SerializedPropertyType.ManagedReference
                    && candidate.managedReferenceValue is EventResponse response)
                {
                    return PayloadOf(response.GetType(), typeof(EventResponse<>));
                }
            }

            return null;
        }
    }
}
