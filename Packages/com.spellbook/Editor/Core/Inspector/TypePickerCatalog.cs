using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpellBook.Core.Inspector;
using UnityEditor;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>Um tipo oferecido pelo seletor de tipo.</summary>
    public readonly struct TypePickerEntry
    {
        public TypePickerEntry(Type type, string displayName, string categoryPath, string summary)
        {
            Type = type;
            DisplayName = displayName;
            CategoryPath = categoryPath;
            Summary = summary;
        }

        public Type Type { get; }

        /// <summary>Nome do tipo em <c>ObjectNames.NicifyVariableName</c>.</summary>
        public string DisplayName { get; }

        /// <summary>Caminho do <see cref="TypeCategoryAttribute"/>; vazio quando o tipo fica na raiz.</summary>
        public string CategoryPath { get; }

        /// <summary>Resumo do <see cref="TypeCategoryAttribute"/>; vazio quando não há.</summary>
        public string Summary { get; }
    }

    /// <summary>
    /// Parte lógica, sem UI, do seletor de tipo para campos <c>[SerializeReference]</c> (ADR 0026, item 4; Spec, seção 4.10).
    /// </summary>
    public static class TypePickerCatalog
    {
        private const BindingFlags InstanceCtor = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Dictionary<Type, IReadOnlyList<TypePickerEntry>> Cache =
            new Dictionary<Type, IReadOnlyList<TypePickerEntry>>();

        /// <summary>
        /// Tipos que o seletor oferece para o tipo base: o próprio tipo base e os derivados que são concretos, não genéricos,
        /// <c>[Serializable]</c>, com construtor sem parâmetros e fora de <c>UnityEngine.Object</c>. O resultado fica em cache.
        /// </summary>
        public static IReadOnlyList<TypePickerEntry> GetCandidates(Type baseType)
        {
            if (baseType == null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }

            if (!Cache.TryGetValue(baseType, out IReadOnlyList<TypePickerEntry> entries))
            {
                entries = TypeCache.GetTypesDerivedFrom(baseType)
                    .Prepend(baseType)
                    .Where(IsCandidate)
                    .Select(CreateEntry)
                    .OrderBy(e => e.DisplayName, StringComparer.Ordinal)
                    .ToList();
                Cache[baseType] = entries;
            }

            return entries;
        }

        /// <summary>Entradas cujo nome exibido, nome do tipo ou caminho da categoria contém a busca, sem diferenciar caixa.</summary>
        public static IEnumerable<TypePickerEntry> Filter(IEnumerable<TypePickerEntry> entries, string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return entries;
            }

            return entries.Where(e => Contains(e.DisplayName, query) || Contains(e.Type.Name, query)
                || Contains(e.CategoryPath, query));
        }

        /// <summary>Tipo declarado do campo, lido de <c>managedReferenceFieldTypename</c>; <c>null</c> se não for encontrado.</summary>
        public static Type ResolveFieldBaseType(SerializedProperty property)
        {
            string typename = property.managedReferenceFieldTypename;
            int space = typename.IndexOf(' ');
            if (space <= 0)
            {
                return null;
            }

            string assembly = typename.Substring(0, space);
            string fullName = typename.Substring(space + 1).Replace('/', '+');
            return Type.GetType($"{fullName}, {assembly}");
        }

        /// <summary>Põe no campo uma instância nova do tipo, ou nulo, com Undo.</summary>
        public static void Assign(SerializedProperty property, Type typeOrNull)
        {
            property.managedReferenceValue = typeOrNull == null ? null : Activator.CreateInstance(typeOrNull, true);
            property.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Troca por uma cópia independente o elemento de lista cujo <c>managedReferenceId</c> já aparece antes na mesma lista.
        /// Devolve <c>true</c> se trocou.
        /// </summary>
        public static bool EnsureUniqueInArray(SerializedProperty element)
        {
            object value = element.managedReferenceValue;
            SerializedProperty array = FindOwningArray(element, out int index);
            if (value == null || array == null)
            {
                return false;
            }

            long id = element.managedReferenceId;
            for (int i = 0; i < index; i++)
            {
                if (array.GetArrayElementAtIndex(i).managedReferenceId == id)
                {
                    object copy = Activator.CreateInstance(value.GetType(), true);
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), copy);
                    element.managedReferenceValue = copy;
                    element.serializedObject.ApplyModifiedProperties();
                    return true;
                }
            }

            return false;
        }

        private static bool IsCandidate(Type type)
        {
            return !type.IsAbstract && !type.IsInterface && !type.IsGenericType && !type.ContainsGenericParameters
                && !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.IsSerializable
                && type.GetConstructor(InstanceCtor, null, Type.EmptyTypes, null) != null;
        }

        private static TypePickerEntry CreateEntry(Type type)
        {
            var category = type.GetCustomAttribute<TypeCategoryAttribute>(false);
            return new TypePickerEntry(type, ObjectNames.NicifyVariableName(type.Name),
                category?.Path ?? string.Empty, category?.Summary ?? string.Empty);
        }

        private static bool Contains(string text, string query)
        {
            return text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static SerializedProperty FindOwningArray(SerializedProperty element, out int index)
        {
            const string marker = ".Array.data[";
            string path = element.propertyPath;
            int start = path.LastIndexOf(marker, StringComparison.Ordinal);
            index = -1;
            if (start < 0 || !path.EndsWith("]", StringComparison.Ordinal))
            {
                return null;
            }

            int numberStart = start + marker.Length;
            index = int.Parse(path.Substring(numberStart, path.Length - numberStart - 1));
            return element.serializedObject.FindProperty(path.Substring(0, start));
        }
    }
}
