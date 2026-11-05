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
        private const string EventChannelSuffix = "EventChannel";
        private const string ChannelSuffix = "Channel";
        private static readonly string[] TechnicalSuffixes = { "Asset", "Definition" };

        // Grupos do menu por assembly (ADR 0043, item 2): assembly → pastas de menu permitidas.
        // Um assembly do pacote fora desta tabela quebra a guarda.
        private static readonly Dictionary<string, string[]> ModuleMenus = new Dictionary<string, string[]>
        {
            ["SpellBook.GAS"] = new[] { "SpellBook/GAS/", "SpellBook/GAS/Attributes/", "SpellBook/GAS/Attributes/Formulas/" },
            ["SpellBook.Tags"] = new[] { "SpellBook/Tags/" },
            ["SpellBook.Core"] = new[] { "SpellBook/Core/", "SpellBook/Text/", "SpellBook/Spawn/" },
            ["SpellBook.Time"] = new[] { "SpellBook/Time/" },
            ["SpellBook.Pooling"] = new[] { "SpellBook/Pooling/", "SpellBook/Spawn/" },
            ["SpellBook.Events"] = new[] { "SpellBook/Events/" },
            ["SpellBook.Teams"] = new[] { "SpellBook/Teams/" },
            ["SpellBook.Localization"] = new[] { "SpellBook/Text/" },
            ["SpellBook.GAS.Bridges"] = new[] { "SpellBook/GAS/Events/" },
            ["SpellBook.GAS.UI"] = new[] { "SpellBook/GAS/UI/" },
            ["SpellBook.Cues"] = new[] { "SpellBook/Cues/" },
            ["SpellBook.UI"] = new[] { "SpellBook/UI/" },
            ["SpellBook.UI.UGUI"] = new[] { "SpellBook/UI/" },
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
        public void PackageMenu_UsesFolderAllowedForItsAssembly()
        {
            List<(Type Type, string Menu, string FileName)> entries = PackageMenus();

            Assert.That(entries.Select(entry => entry.Type.Name), Does.Contain("GameplayAbility")
                .And.Contain("SimpleAttributeFormula").And.Contain("GameplayTagDefinition")
                .And.Contain("TransformAnchor").And.Contain("PausableTimeSource").And.Contain("PoolConfiguration")
                .And.Contain("IntEventChannel").And.Contain("TeamRelationMatrix").And.Contain("LocalizationTextProvider")
                .And.Contain("AbilityEventChannel").And.Contain("AbilityLoadout")
                .And.Contain("CueDefinition").And.Contain("UITheme"),
                "a varredura não achou os tipos esperados");

            IEnumerable<string> violations = entries
                .Where(entry => !ModuleMenus.TryGetValue(AssemblyOf(entry.Type), out string[] folders)
                    || !folders.Contains(MenuFolder(entry.Menu)))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");
            Assert.That(violations, Is.Empty, "o menu fica numa pasta do grupo do assembly (ADR 0043, item 2)");
        }

        [Test]
        public void PackageMenuItem_DropsGameplayPrefix()
        {
            IEnumerable<string> violations = PackageMenus()
                .Where(entry => MenuItemName(entry.Menu).StartsWith(GameplayPrefix, StringComparison.Ordinal))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");

            Assert.That(violations, Is.Empty, "o nome do item vem sem o \"Gameplay\" (ADR 0043, item 1)");
        }

        [Test]
        public void PackageMenuItem_IsTypeNameWithoutTechnicalParts()
        {
            IEnumerable<string> violations = PackageMenus()
                .Where(entry => !AllowedItemNames(entry.Type.Name).Contains(MenuItemName(entry.Menu).Replace(" ", string.Empty)))
                .Select(entry => $"{entry.Type.FullName}: \"{entry.Menu}\"");

            Assert.That(violations, Is.Empty, "o nome do item é o nome do tipo sem o sufixo técnico (ADR 0043, item 1)");
        }

        [Test]
        public void PackageFileName_IsNewPlusMenuItemName()
        {
            IEnumerable<string> violations = PackageMenus()
                .Where(entry => entry.FileName != FileNamePrefix + MenuItemName(entry.Menu))
                .Select(entry => $"{entry.Type.FullName}: menu \"{entry.Menu}\", fileName \"{entry.FileName}\"");

            Assert.That(violations, Is.Empty, "o fileName é \"New <Nome>\", com o nome do item do menu (ADR 0043, item 3)");
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

        // Nomes aceitos pelo item 1 da 0043: sem o "Gameplay", com o "EventChannel" lido como "Channel"
        // (exemplo "New Int Channel" do item 3) e, se ajudar a ler, sem o sufixo "Asset" ou "Definition".
        private static IEnumerable<string> AllowedItemNames(string typeName)
        {
            string name = typeName.StartsWith(GameplayPrefix, StringComparison.Ordinal)
                ? typeName.Substring(GameplayPrefix.Length)
                : typeName;
            if (name.EndsWith(EventChannelSuffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - EventChannelSuffix.Length) + ChannelSuffix;
            }

            yield return name;
            foreach (string suffix in TechnicalSuffixes.Where(suffix => name.Length > suffix.Length
                && name.EndsWith(suffix, StringComparison.Ordinal)))
            {
                yield return name.Substring(0, name.Length - suffix.Length);
            }
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
