using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using UnityEditor;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>Um tipo base do seletor de tipo, os campos que o usam e os tipos que o seletor oferece para ele.</summary>
    public sealed class TypeCatalogSection
    {
        public TypeCatalogSection(Type baseType, IReadOnlyList<string> fields, IReadOnlyList<TypePickerEntry> entries)
        {
            BaseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public Type BaseType { get; }

        /// <summary>Campos com o seletor, como o Inspector os mostra (<c>EventListener > Responses</c>), em ordem ordinal.</summary>
        public IReadOnlyList<string> Fields { get; }

        /// <summary>Tipos oferecidos pelo seletor, na mesma ordem dele, só dos assemblies do catálogo.</summary>
        public IReadOnlyList<TypePickerEntry> Entries { get; }
    }

    /// <summary>
    /// Catálogo dos tipos que o seletor do Inspector oferece. Não tem regra própria: lê os campos com
    /// <see cref="TypePickerAttribute"/> e pede os candidatos a <see cref="TypePickerCatalog.GetCandidates"/>.
    /// </summary>
    public static class TypeCatalog
    {
        private const BindingFlags DeclaredFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Seções dos assemblies do pacote, em ordem ordinal do nome completo do tipo base.</summary>
        public static IReadOnlyList<TypeCatalogSection> Build()
        {
            return Build(AppDomain.CurrentDomain.GetAssemblies().Where(a => IsPackageAssembly(a.GetName().Name)));
        }

        /// <summary>
        /// Seções dos campos declarados nos assemblies dados, com as entradas restritas a tipos desses assemblies. Uma base
        /// sem entradas não gera seção.
        /// </summary>
        internal static IReadOnlyList<TypeCatalogSection> Build(IEnumerable<Assembly> assemblies)
        {
            var included = new HashSet<Assembly>(assemblies);
            var fieldsByBase = new Dictionary<Type, SortedSet<string>>();
            foreach (FieldInfo field in included.SelectMany(LoadableTypes).SelectMany(t => t.GetFields(DeclaredFields)))
            {
                Type baseType = ElementType(field.FieldType);
                if (!field.IsDefined(typeof(TypePickerAttribute), false) || baseType.ContainsGenericParameters)
                {
                    continue;
                }

                if (!fieldsByBase.TryGetValue(baseType, out SortedSet<string> fields))
                {
                    fields = new SortedSet<string>(StringComparer.Ordinal);
                    fieldsByBase[baseType] = fields;
                }

                fields.Add(
                    $"{ObjectNames.NicifyVariableName(field.DeclaringType.Name)} > {ObjectNames.NicifyVariableName(field.Name)}");
            }

            var sections = new List<TypeCatalogSection>();
            foreach (KeyValuePair<Type, SortedSet<string>> pair in fieldsByBase.OrderBy(p => p.Key.FullName, StringComparer.Ordinal))
            {
                List<TypePickerEntry> entries = TypePickerCatalog.GetCandidates(pair.Key)
                    .Where(e => included.Contains(e.Type.Assembly))
                    .ToList();
                if (entries.Count > 0)
                {
                    sections.Add(new TypeCatalogSection(pair.Key, pair.Value.ToList(), entries));
                }
            }

            return sections;
        }

        /// <summary>
        /// Assembly do pacote: nome começa por <c>SpellBook.</c>, sem <c>.Tests</c>, sem <c>SpellBook.Testing</c> e sem
        /// <c>SpellBook.Samples.</c>.
        /// </summary>
        public static bool IsPackageAssembly(string assemblyName)
        {
            return assemblyName != null && assemblyName.StartsWith("SpellBook.", StringComparison.Ordinal)
                && !assemblyName.Contains(".Tests") && assemblyName != "SpellBook.Testing"
                && !assemblyName.StartsWith("SpellBook.Samples.", StringComparison.Ordinal);
        }

        private static Type ElementType(Type fieldType)
        {
            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            return fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>)
                ? fieldType.GetGenericArguments()[0]
                : fieldType;
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
    }
}
