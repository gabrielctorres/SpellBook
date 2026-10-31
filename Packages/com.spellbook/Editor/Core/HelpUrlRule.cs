using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SpellBook.Core.Editor
{
    /// <summary>Um tipo que fere a regra de <c>[HelpURL]</c> e o motivo.</summary>
    public readonly struct HelpUrlViolation
    {
        public HelpUrlViolation(Type type, string reason)
        {
            Type = type;
            Reason = reason;
        }

        public Type Type { get; }

        public string Reason { get; }

        public override string ToString() => $"{Type?.FullName}: {Reason}";
    }

    /// <summary>
    /// Regra de <c>[HelpURL]</c> (R35, último item; ADR 0007; Spec, seção 4.9): todo <see cref="MonoBehaviour"/> e
    /// <see cref="ScriptableObject"/> público e concreto tem o atributo, com URL iniciada por <see cref="DocUrls.Base"/>.
    /// </summary>
    public static class HelpUrlRule
    {
        public const string MissingReason = "sem [HelpURL]";
        public const string OutsideBaseReason = "URL fora de DocUrls.Base";

        /// <summary>Os tipos de <paramref name="assemblies"/> que ferem a regra, em ordem de nome.</summary>
        public static IReadOnlyList<HelpUrlViolation> FindViolations(IEnumerable<Assembly> assemblies)
        {
            var violations = new List<HelpUrlViolation>();
            foreach (Type type in assemblies.SelectMany(a => a.GetTypes()).Where(IsInScope))
            {
                var attribute = type.GetCustomAttribute<HelpURLAttribute>(false);
                if (attribute == null)
                {
                    violations.Add(new HelpUrlViolation(type, MissingReason));
                }
                else if (attribute.URL == null || !attribute.URL.StartsWith(DocUrls.Base, StringComparison.Ordinal))
                {
                    violations.Add(new HelpUrlViolation(type, OutsideBaseReason));
                }
            }

            violations.Sort((a, b) => string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            return violations;
        }

        /// <summary>Os assemblies de runtime do pacote carregados: <c>SpellBook.*</c> sem testes, editor e <c>SpellBook.Testing</c>.</summary>
        public static IEnumerable<Assembly> RuntimeAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a =>
            {
                string name = a.GetName().Name;
                return name.StartsWith("SpellBook.", StringComparison.Ordinal)
                    && !name.Contains(".Tests") && !name.Contains(".Editor") && name != "SpellBook.Testing";
            });
        }

        private static bool IsInScope(Type type)
        {
            return type.IsClass && type.IsVisible && !type.IsAbstract && !type.ContainsGenericParameters
                && (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type));
        }
    }
}
