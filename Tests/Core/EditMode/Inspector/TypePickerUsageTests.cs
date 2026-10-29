using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Uso do seletor de tipo nos campos <c>[SerializeReference]</c> do pacote (ADR 0026, item 4; CA63).</summary>
    public class TypePickerUsageTests
    {
        private const BindingFlags DeclaredFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // A EventEmission é criada por código (0013) e não usa o seletor (ADR 0026, item 4).
        private const string EventEmissionTypeName = "EventEmission";

        [Test]
        public void EverySerializeReferenceFieldInRuntime_HasTypePicker_ExceptEventEmission()
        {
            List<FieldInfo> fields = PackageAssemblies()
                .SelectMany(a => a.GetTypes())
                .SelectMany(t => t.GetFields(DeclaredFields))
                .Where(f => f.IsDefined(typeof(SerializeReference), false) && !HoldsEventEmission(f))
                .ToList();

            List<string> missing = fields
                .Where(f => !f.IsDefined(typeof(TypePickerAttribute), false))
                .Select(f => $"{f.DeclaringType.FullName}.{f.Name}")
                .ToList();

            Assert.That(fields, Is.Not.Empty, "Nenhum campo [SerializeReference] encontrado nos assemblies SpellBook.*.");
            Assert.That(missing, Is.Empty, "Campos [SerializeReference] sem [TypePicker]:\n" + string.Join("\n", missing));
        }

        private static IEnumerable<Assembly> PackageAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a =>
            {
                string name = a.GetName().Name;
                return name.StartsWith("SpellBook.", StringComparison.Ordinal)
                    && !name.Contains(".Tests") && name != "SpellBook.Testing";
            });
        }

        private static bool HoldsEventEmission(FieldInfo field)
        {
            Type type = field.FieldType.IsArray ? field.FieldType.GetElementType()
                : field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                    ? field.FieldType.GetGenericArguments()[0] : field.FieldType;

            for (; type != null; type = type.BaseType)
            {
                if (type.Name == EventEmissionTypeName || type.Name.StartsWith(EventEmissionTypeName + "`", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
