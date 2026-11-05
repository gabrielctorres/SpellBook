using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Editor.Inspector;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>Página do catálogo em Markdown determinístico (Spec referencia-catalogo-e-api, D3; RC2).</summary>
    public class TypeCatalogPageTests
    {
        [Test]
        public void Render_StartsWithGeneratedNotice()
        {
            string text = TypeCatalogPage.Render(new[] { Section() });

            string firstLine = text.Substring(0, text.IndexOf('\n'));
            Assert.That(firstLine, Does.StartWith("<!--").And.EndWith("-->"));
            Assert.That(firstLine, Does.Contain("Não edite"));
            Assert.That(firstLine, Does.Contain("SpellBook > Documentação > Gerar referência"));
        }

        [Test]
        public void Render_SameSectionsTwice_SameText()
        {
            string first = TypeCatalogPage.Render(new[] { Section(), NestedSection() });
            string second = TypeCatalogPage.Render(new[] { Section(), NestedSection() });

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.Not.Empty);
        }

        [Test]
        public void Render_UsesLfAndEndsWithNewline()
        {
            string text = TypeCatalogPage.Render(new[] { Section(Entry(summary: "Linha um\r\nlinha dois")) });

            Assert.That(text, Does.Not.Contain("\r"));
            Assert.That(text, Does.EndWith("\n"));
            Assert.That(text, Does.Not.EndWith("\n\n"));
        }

        [Test]
        public void Render_PageTitleWithoutAnchor()
        {
            string text = TypeCatalogPage.Render(new[] { Section() });

            Assert.That(text, Does.Contain("\n# Catálogo de tipos\n"));
        }

        [Test]
        public void Render_SectionTitleWithoutAnchor()
        {
            string text = TypeCatalogPage.Render(new[] { NestedSection() });

            Assert.That(text, Does.Contain("\n## `NestedBase`\n"));
        }

        [Test]
        public void Render_TypeNameWithoutLink()
        {
            string text = TypeCatalogPage.Render(new[] { NestedSection() });

            Assert.That(text, Does.Contain("\n| Nested Entry | Raiz |  |\n"));
            Assert.That(text, Does.Not.Contain("](").And.Not.Contain("(API)"));
        }

        [Test]
        public void Render_EmptyCategory_ShowsRaiz()
        {
            string text = TypeCatalogPage.Render(new[] { Section(Entry(category: "", summary: "Resumo.")) });

            Assert.That(text, Does.Contain("\n| Page Entry | Raiz | Resumo. |\n"));
        }

        [Test]
        public void Render_EmptySummary_LeavesCellEmpty()
        {
            string text = TypeCatalogPage.Render(new[] { Section(Entry(category: "Fluxo", summary: "")) });

            Assert.That(text, Does.Contain("\n| Page Entry | Fluxo |  |\n"));
        }

        [Test]
        public void Render_EscapesPipeAndNewline()
        {
            string text = TypeCatalogPage.Render(new[] { Section(Entry(category: "A|B", summary: "um | dois\ntrês")) });

            Assert.That(text, Does.Contain("\n| Page Entry | A\\|B | um \\| dois três |\n"));
        }

        [Test]
        public void Render_ListsFieldsUnderSection()
        {
            var section = new TypeCatalogSection(typeof(PageBase), new[] { "Holder A > Items", "Holder B > Item" },
                new[] { Entry() });

            string text = TypeCatalogPage.Render(new[] { section });

            Assert.That(text, Does.Contain(
                "\n## `PageBase`\n\n"
                + "Aparece em: `Holder A > Items`, `Holder B > Item`.\n\n"
                + "| Tipo | Categoria | Resumo |\n|---|---|---|\n"));
        }

        [Test]
        public void Render_ClosedGenericBase_TitleInCSharpForm()
        {
            string text = TypeCatalogPage.Render(new[] { GenericSection() });

            Assert.That(text, Does.Contain("\n## `PageGenericBase<PageBase>`\n"));
        }

        private static TypePickerEntry Entry(string category = "Fluxo", string summary = "Faz algo.")
        {
            return new TypePickerEntry(typeof(PageEntry), "Page Entry", category, summary);
        }

        private static TypeCatalogSection Section(params TypePickerEntry[] entries)
        {
            IReadOnlyList<TypePickerEntry> list = entries.Length > 0 ? entries : new[] { Entry() };
            return new TypeCatalogSection(typeof(PageBase), new[] { "Holder > Items" }, list);
        }

        private static TypeCatalogSection NestedSection()
        {
            return new TypeCatalogSection(typeof(NestedBase), new[] { "Holder > Nested" },
                new[] { new TypePickerEntry(typeof(NestedEntry), "Nested Entry", "", "") });
        }

        private static TypeCatalogSection GenericSection()
        {
            return new TypeCatalogSection(typeof(PageGenericBase<PageBase>), new[] { "Holder > Generic" },
                new[] { new TypePickerEntry(typeof(PageGenericEntry), "Page Generic Entry", "", "") });
        }

        private abstract class NestedBase
        {
        }

        [Serializable]
        private sealed class NestedEntry : NestedBase
        {
        }
    }

    internal abstract class PageBase
    {
    }

    [Serializable]
    internal sealed class PageEntry : PageBase
    {
    }

    internal abstract class PageGenericBase<T>
    {
    }

    [Serializable]
    internal sealed class PageGenericEntry : PageGenericBase<PageBase>
    {
    }
}
