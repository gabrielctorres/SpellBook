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
        private const string FileNamePrefix = "New ";
        private const string GameplayPrefix = "Gameplay";

        // Módulos já conferidos pela ADR 0043 (OA1 e OA2): assembly → pastas de menu permitidas (item 2).
        // Cada ticket do épico acrescenta os módulos dele; o OA4 passa a regra para o pacote inteiro.
        private static readonly Dictionary<string, string[]> CheckedModuleMenus = new Dictionary<string, string[]>
        {
            ["SpellBook.GAS"] = new[] { "SpellBook/GAS/", "SpellBook/GAS/Attributes/", "SpellBook/GAS/Attributes/Formulas/" },
            ["SpellBook.Tags"] = new[] { "SpellBook/Tags/" },
            ["SpellBook.Core"] = new[] { "SpellBook/Core/", "SpellBook/Text/", "SpellBook/Spawn/" },
            ["SpellBook.Time"] = new[] { "SpellBook/Time/" },
            ["SpellBook.Pooling"] = new[] { "SpellBook/Pooling/", "SpellBook/Spawn/" },
        };

        [Test]
        public void EveryPackageCreateAssetMenu_StartsWithSpellBookRoot()
        {
            List<(Type Type, string Menu, string FileName)> entries = PackageMenus();

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

        [Test]
        public void CheckedModuleMenu_UsesFolderAllowedForItsAssembly()
        {
            List<(Type Type, string Menu, string FileName)> entries = CheckedModuleEntries();

            Assert.That(entries.Select(entry => entry.Type.Name), Does.Contain("GameplayAbility")
                .And.Contain("SimpleAttributeFormula").And.Contain("GameplayTagDefinition")
                .And.Contain("TransformAnchor").And.Contain("PausableTimeSource").And.Contain("PoolConfiguration"),
                "a varredura não achou os tipos esperados");

            IEnumerable<string> violations = entries
                .Where(entry => !CheckedModuleMenus[AssemblyOf(entry.Type)].Contains(MenuFolder(entry.Menu)))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");
            Assert.That(violations, Is.Empty, "o menu fica numa pasta do grupo do assembly (ADR 0043, item 2)");
        }

        [Test]
        public void CheckedModuleMenuItem_DropsGameplayPrefix()
        {
            IEnumerable<string> violations = CheckedModuleEntries()
                .Where(entry => MenuItemName(entry.Menu).StartsWith(GameplayPrefix, StringComparison.Ordinal))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");

            Assert.That(violations, Is.Empty, "o nome do item vem sem o \"Gameplay\" (ADR 0043, item 1)");
        }

        [Test]
        public void CheckedModuleFileName_IsNewPlusMenuItemName()
        {
            IEnumerable<string> violations = CheckedModuleEntries()
                .Where(entry => entry.FileName != FileNamePrefix + MenuItemName(entry.Menu))
                .Select(entry => $"{entry.Type.FullName}: menu \"{entry.Menu}\", fileName \"{entry.FileName}\"");

            Assert.That(violations, Is.Empty, "o fileName é \"New <Nome>\", com o nome do item do menu (ADR 0043, item 3)");
        }

        private static List<(Type Type, string Menu, string FileName)> CheckedModuleEntries()
        {
            return PackageMenus().Where(entry => CheckedModuleMenus.ContainsKey(AssemblyOf(entry.Type))).ToList();
        }

        private static List<(Type Type, string Menu, string FileName)> PackageMenus()
        {
            return TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>()
                .Where(IsPackageRuntimeType)
                .Select(type => (Type: type, Menu: type.GetCustomAttribute<CreateAssetMenuAttribute>(false)))
                .Where(entry => entry.Menu != null)
                .Select(entry => (entry.Type, Menu: entry.Menu.menuName ?? string.Empty, FileName: entry.Menu.fileName ?? string.Empty))
                .ToList();
        }

        private static string MenuFolder(string menu)
        {
            return menu.Substring(0, menu.LastIndexOf('/') + 1);
        }

        private static string MenuItemName(string menu)
        {
            return menu.Substring(menu.LastIndexOf('/') + 1);
        }

        private static string AssemblyOf(Type type)
        {
            return type.Assembly.GetName().Name;
        }

        private static bool IsPackageRuntimeType(Type type)
        {
            string assembly = AssemblyOf(type);
            return assembly.StartsWith(PackageAssemblyPrefix, StringComparison.Ordinal)
                && !assembly.Contains(".Tests");
        }
    }
}
