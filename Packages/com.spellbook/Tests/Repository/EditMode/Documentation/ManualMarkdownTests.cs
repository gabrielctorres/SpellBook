using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// A regra do slug medida no GitBook (Spec <c>migracao-gitbook.md</c>, Detalhes do GB5 e do GB6) e as conferências das
    /// guardas do manual (CA5), sobre páginas pequenas escritas no teste.
    /// </summary>
    public class ManualMarkdownTests
    {
        private const string Site = "https://exemplo.gitbook.io/spellbook/";

        [TestCase("Mapa dos módulos", "mapa-dos-modulos", TestName = "Slug_LowercasesAndJoinsWordsWithHyphen")]
        [TestCase("ÁRVORE Ç ñ", "arvore-c-n", TestName = "Slug_DropsAccentsAndCedilla")]
        [TestCase("Cast Range: alcance & custo", "cast-range-alcance-and-custo", TestName = "Slug_SpellsAmpersand")]
        [TestCase(@"EventBus\<T\>", "eventbus-less-than-t-greater-than", TestName = "Slug_SpellsAngleBrackets")]
        [TestCase("Regras de migração (2 → 3)", "regras-de-migracao-2-3", TestName = "Slug_DropsPunctuationAndCollapsesHyphens")]
        [TestCase("Regras da 3.0", "regras-da-3.0", TestName = "Slug_KeepsDotBetweenDigits")]
        [TestCase("Fim. Outra frase", "fim-outra-frase", TestName = "Slug_DropsDotAfterLetter")]
        [TestCase("4. Ciclo de vida, cooldown e custo", "id-4.-ciclo-de-vida-cooldown-e-custo", TestName = "Slug_KeepsDotAfterDigit")]
        [TestCase("1.5 AbilitySystemComponent (ASC)", "id-1.5-abilitysystemcomponent-asc", TestName = "Slug_StartingWithDigit_GetsIdPrefix")]
        [TestCase("`MathTools`", "mathtools", TestName = "Slug_KeepsOnlyTextOfInlineCode")]
        [TestCase("Fireball com `damage` e **cooldown**", "fireball-com-damage-e-cooldown", TestName = "Slug_KeepsOnlyTextOfEmphasis")]
        [TestCase("Veja o [Inspector](inicio/inspector.md)", "veja-o-inspector", TestName = "Slug_KeepsOnlyTextOfLink")]
        public void Slug(string title, string expected)
        {
            string result = ManualMarkdown.Slug(title);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Anchors_SkipsPageTitle()
        {
            HashSet<string> result = ManualMarkdown.Anchors(Page("# Página", "", "## Seção", "### Sub", "#### Fundo"));

            Assert.That(result, Is.EquivalentTo(new[] { "secao", "sub", "fundo" }));
        }

        [Test]
        public void Anchors_SkipsFifthAndSixthLevels()
        {
            HashSet<string> result = ManualMarkdown.Anchors(Page("# Página", "##### Cinco", "###### Seis", "## Dois"));

            Assert.That(result, Is.EquivalentTo(new[] { "dois" }));
        }

        [Test]
        public void Anchors_NumbersRepeatedTitles()
        {
            HashSet<string> result = ManualMarkdown.Anchors(Page("# Página", "## Uso", "### Uso", "## Uso"));

            Assert.That(result, Is.EquivalentTo(new[] { "uso", "uso-1", "uso-2" }));
        }

        [Test]
        public void Anchors_IgnoresTitlesInsideCodeBlocks()
        {
            HashSet<string> result = ManualMarkdown.Anchors(Page("# Página", "```text", "## Falso", "```", "## Real"));

            Assert.That(result, Is.EquivalentTo(new[] { "real" }));
        }

        [Test]
        public void BrokenLinks_AcceptsExistingPageAndAnchor()
        {
            string page = Page("# A", "## Aqui", "[b](../modulos/b.md#secao-b) e [c](c.md) e [aqui](#aqui)");

            List<string> result = ManualMarkdown.BrokenLinks("inicio/a.md", page, Site, Pages());

            Assert.That(result, Is.Empty);
        }

        [TestCase("[x](falta.md)", "arquivo não existe (inicio/falta.md)", TestName = "BrokenLinks_MissingFile_Reports")]
        [TestCase("[x](c.md#nao-tem)", "âncora #nao-tem não existe em inicio/c.md", TestName = "BrokenLinks_MissingAnchorInOtherPage_Reports")]
        [TestCase("[x](#nao-tem)", "âncora #nao-tem não existe em inicio/a.md", TestName = "BrokenLinks_MissingAnchorInSamePage_Reports")]
        [TestCase("[x](../../spec/b.md)", "fora da pasta do manual", TestName = "BrokenLinks_OutsideManual_Reports")]
        [TestCase("[x](/pages/b)", "caminho a partir da raiz do site", TestName = "BrokenLinks_RootRelative_Reports")]
        [TestCase("[x](https://exemplo.gitbook.io/spellbook/modulos/b#secao-b)", "próprio site", TestName = "BrokenLinks_AbsoluteToOwnSite_Reports")]
        [TestCase("[x](HTTPS://Exemplo.GitBook.io/spellbook/)", "próprio site", TestName = "BrokenLinks_AbsoluteToOwnSiteAnyCase_Reports")]
        [TestCase("<https://exemplo.gitbook.io/spellbook/>", "próprio site", TestName = "BrokenLinks_AutoLinkToOwnSite_Reports")]
        public void BrokenLinks_Reports(string line, string reason)
        {
            List<string> result = ManualMarkdown.BrokenLinks("inicio/a.md", Page("# A", "## Tem", line), Site, Pages());

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Does.StartWith("inicio/a.md:3: ").And.Contain(reason));
        }

        [Test]
        public void BrokenLinks_IgnoresExternalLinks()
        {
            string page = Page("# A", "[u](https://docs.unity3d.com/Manual/x.html) [m](mailto:a@b.c) <https://github.com/x>");

            List<string> result = ManualMarkdown.BrokenLinks("a.md", page, Site, Pages());

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BrokenLinks_IgnoresLinksInCode()
        {
            string page = Page("# A", "`[x](falta.md)`", "```csharp", "var s = \"[y](falta.md)\";", "```");

            List<string> result = ManualMarkdown.BrokenLinks("a.md", page, Site, Pages());

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void SummaryProblems_AcceptsEveryPageOnce()
        {
            string summary = Page("# Table of contents", "", "## Começar", "", "* [Início](README.md)", "  * [Sub](inicio/a.md)");

            List<string> result = ManualMarkdown.SummaryProblems(summary, new[] { "README.md", "inicio/a.md" });

            Assert.That(result, Is.Empty);
        }

        [TestCase("* [A](a.md)", "b.md: fora do SUMMARY.md", TestName = "SummaryProblems_PageOutsideSummary_Reports")]
        [TestCase("* [A](a.md)\n* [B](b.md)\n* [De novo](a.md)", "a.md: aparece 2 vezes", TestName = "SummaryProblems_RepeatedEntry_Reports")]
        [TestCase("* [A](a.md)\n* [B](b.md)\n* [Some](sumiu.md)", "sumiu.md: entrada do SUMMARY.md sem arquivo", TestName = "SummaryProblems_EntryWithoutFile_Reports")]
        public void SummaryProblems_Reports(string summary, string problem)
        {
            List<string> result = ManualMarkdown.SummaryProblems(summary, new[] { "a.md", "b.md" });

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Does.StartWith(problem));
        }

        [TestCase("## Título {#titulo}")]
        [TestCase("!!! note \"Nota\"")]
        [TestCase("??? tip \"Dica\"")]
        [TestCase("    --8<-- \"Tests/Exemplo.cs:secao\"")]
        public void MkDocsLeftovers_ReportsEachSyntax(string line)
        {
            List<string> result = ManualMarkdown.MkDocsLeftovers(Page("# Página", "", line));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0], Does.StartWith("3:"));
        }

        [Test]
        public void MkDocsLeftovers_IgnoresGitBookBlocks()
        {
            string page = Page("# Página", "{% hint style=\"info\" %}", "**Nota**", "{% endhint %}",
                "{% code title=\"Tests/A.cs:a\" %}", "```csharp", "```", "{% endcode %}", "Que bom!!! Sério???");

            List<string> result = ManualMarkdown.MkDocsLeftovers(page);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void MkDocsLeftovers_IgnoresSyntaxInsideCodeBlocks()
        {
            string page = Page("# Página", "```text", "!!! note", "{#id}", "```", "```csharp", "--8<-- \"a.cs\"", "```");

            List<string> result = ManualMarkdown.MkDocsLeftovers(page);

            Assert.That(result, Has.Count.EqualTo(1), "o --8<-- vale também dentro do bloco, onde o MkDocs o usava");
            Assert.That(result[0], Does.StartWith("7:"));
        }

        private static string Page(params string[] lines)
        {
            return string.Join("\n", lines) + "\n";
        }

        /// <summary>As páginas que os testes de link leem, além da página do próprio teste.</summary>
        private static Func<string, string> Pages()
        {
            var pages = new Dictionary<string, string>
            {
                ["modulos/b.md"] = Page("# B", "## Seção B"),
                ["inicio/c.md"] = Page("# C", "## Tem"),
            };

            return path => pages.TryGetValue(path, out string text) ? text : null;
        }
    }
}
