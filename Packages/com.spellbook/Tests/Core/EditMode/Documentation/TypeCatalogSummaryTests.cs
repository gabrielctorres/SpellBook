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
        [Test]
        public void EveryCatalogTypeHasSummary()
        {
            string[] missing = TypeCatalog.Build()
                .SelectMany(section => section.Entries)
                .Where(entry => string.IsNullOrWhiteSpace(entry.Summary))
                .Select(entry => entry.Type.FullName)
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.That(missing, Is.Empty,
                "tipos do catálogo sem resumo; ponha [TypeCategory(caminho, Summary = \"...\")] em:\n" + string.Join("\n", missing));
        }
    }
}
