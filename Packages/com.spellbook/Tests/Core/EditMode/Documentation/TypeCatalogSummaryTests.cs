using System;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Guarda do resumo obrigatório: todo tipo do catálogo tem <c>[TypeCategory]</c> com <c>Summary</c> não vazio
    /// (Spec referencia-catalogo-e-api, D7; ADR 0007, emenda de 2026-11-01).
    /// </summary>
    public class TypeCatalogSummaryTests
    {
        /// <summary>
        /// Namespaces ainda sem resumo, cada um com os seus sub-namespaces. A lista só diminui: o RC7 tira
        /// <c>SpellBook.GAS.Abilities</c> e o RC8 apaga a lista.
        /// </summary>
        private static readonly string[] PendingNamespaces =
        {
            "SpellBook.GAS.Abilities",
            "SpellBook.GAS.Effects",
        };

        [Test]
        public void EveryCatalogTypeHasSummary()
        {
            string[] missing = TypeCatalog.Build()
                .SelectMany(section => section.Entries)
                .Where(entry => string.IsNullOrWhiteSpace(entry.Summary) && !IsPending(entry.Type))
                .Select(entry => entry.Type.FullName)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(missing, Is.Empty,
                "tipos do catálogo sem resumo; ponha [TypeCategory(caminho, Summary = \"...\")] em:\n" + string.Join("\n", missing));
        }

        private static bool IsPending(Type type)
        {
            string ns = type.Namespace ?? string.Empty;
            return PendingNamespaces.Any(prefix => ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal));
        }
    }
}
