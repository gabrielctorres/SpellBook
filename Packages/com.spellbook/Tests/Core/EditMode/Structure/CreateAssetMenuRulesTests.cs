using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Guarda do menu Create dos ScriptableObjects do pacote (issue #344, B-034; ADR 0043, item 5).
    /// Os tipos de exemplo dos assemblies de teste ficam de fora.
    /// </summary>
    public class CreateAssetMenuRulesTests
    {
        private const string PackageAssemblyPrefix = "SpellBook.";
        private const string MenuRoot = "SpellBook/";

        [Test]
        public void EveryPackageCreateAssetMenu_StartsWithSpellBookRoot()
        {
            List<(Type Type, string Menu)> entries = PackageMenus();

            Assert.That(entries.Select(entry => entry.Type.Name), Does.Contain("GameplayAbility")
                .And.Contain("GameplayTagDefinition").And.Contain("AbilitySystemAnchor"),
                "a varredura não achou os tipos esperados");

            IEnumerable<string> violations = entries
                .Where(entry => !entry.Menu.StartsWith(MenuRoot, StringComparison.Ordinal))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");
            Assert.That(violations, Is.Empty, $"todo menu do pacote começa por \"{MenuRoot}\"");
        }

        [Test]
        public void NoTwoPackageTypes_ShareMenuName()
        {
            IEnumerable<string> duplicates = PackageMenus()
                .GroupBy(entry => entry.Menu, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"\"{group.Key}\": {string.Join(", ", group.Select(entry => entry.Type.FullName))}");

            Assert.That(duplicates, Is.Empty);
        }

        private static List<(Type Type, string Menu)> PackageMenus()
        {
            return TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>()
                .Where(IsPackageRuntimeType)
                .Select(type => (Type: type, Menu: type.GetCustomAttribute<CreateAssetMenuAttribute>(false)))
                .Where(entry => entry.Menu != null)
                .Select(entry => (entry.Type, Menu: entry.Menu.menuName ?? string.Empty))
                .ToList();
        }

        private static bool IsPackageRuntimeType(Type type)
        {
            string assembly = type.Assembly.GetName().Name;
            return assembly.StartsWith(PackageAssemblyPrefix, StringComparison.Ordinal)
                && !assembly.Contains(".Tests");
        }
    }
}
