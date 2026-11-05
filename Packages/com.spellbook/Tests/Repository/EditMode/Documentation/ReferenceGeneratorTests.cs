using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// A geração escreve a página do catálogo e expande os blocos C# marcados do manual (Spec migracao-gitbook, GB3 e
    /// GB4; ADR 0044, itens 6, 7 e 8).
    /// </summary>
    public class ReferenceGeneratorTests
    {
        private const string CatalogRelative = "manual/referencia/catalogo.md";
        private const string Source = "Src/Exemplos.cs";

        private static readonly string Root = TestScratch.For("reference-generator");
        private static readonly string Docs = Path.Combine(Root, "Documentation~");

        [SetUp]
        public void SetUp()
        {
            TestScratch.Recreate(Root);
        }

        [TearDown]
        public void TearDown()
        {
            TestScratch.Delete(Root);
        }

        [Test]
        public void MenuPath_IsGerarCatalogoEExemplos()
        {
            Assert.That(ReferenceGenerator.MenuPath, Is.EqualTo("SpellBook/Documentação/Gerar catálogo e exemplos"));
        }

        [Test]
        public void Generate_WritesCatalogFromTypePicker()
        {
            ReferenceGenerator.Generate(Docs);

            byte[] expected = new UTF8Encoding(false).GetBytes(TypeCatalogPage.Render(TypeCatalog.Build()));
            Assert.That(File.ReadAllBytes(Path.Combine(Docs, CatalogRelative)), Is.EqualTo(expected));
        }

        [Test]
        public void Generate_WritesOnlyTheCatalog()
        {
            ReferenceGenerator.Generate(Docs);

            string[] files = Directory.GetFiles(Docs, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Docs, f).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();
            Assert.That(files, Is.EqualTo(new[] { CatalogRelative }));
        }

        [Test]
        public void Generate_ExpandsManualPages()
        {
            WriteFile(Path.Combine(Root, Source), Sections(("um", "    int a = 1;")));
            string page = Path.Combine(Docs, "manual", "modulos", "pagina.md");
            WriteFile(page, Marked(Source + ":um"));

            ReferenceGenerator.Generate(Docs);

            Assert.That(File.ReadAllText(page), Is.EqualTo(Marked(Source + ":um", "int a = 1;")));
        }

        [Test]
        public void Generate_BrokenMarker_Throws()
        {
            WriteFile(Path.Combine(Docs, "manual", "pagina.md"), Marked("Src/NaoExiste.cs:um"));

            var exception = Assert.Throws<InvalidOperationException>(() => ReferenceGenerator.Generate(Docs));

            Assert.That(exception.Message, Does.Contain("pagina.md").And.Contain("Src/NaoExiste.cs"));
        }

        [Test]
        public void ExpandExamples_FillsEmptyBlockWithSection()
        {
            string page = "Texto.\n\n" + Marked(Source + ":um") + "\nDepois.\n";

            string expanded = Expand(page, Sections(("um", "int a = 1;\nint b = 2;")), out List<string> problems);

            Assert.That(expanded, Is.EqualTo("Texto.\n\n" + Marked(Source + ":um", "int a = 1;", "int b = 2;") + "\nDepois.\n"));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ExpandExamples_JoinsSectionsInTitleOrder()
        {
            string source = Sections(("corpo", "A.Fazer();"), ("usings", "using A;\n"));

            string expanded = Expand(Marked(Source + ":usings,corpo"), source, out List<string> problems);

            Assert.That(expanded, Is.EqualTo(Marked(Source + ":usings,corpo", "using A;", "", "A.Fazer();")));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ExpandExamples_RemovesCommonIndentOfEachSection()
        {
            string source = Sections(("a", "        if (x)\n            y();"), ("b", "    z();"));

            string expanded = Expand(Marked(Source + ":a,b"), source, out _);

            Assert.That(expanded, Is.EqualTo(Marked(Source + ":a,b", "if (x)", "    y();", "z();")));
        }

        [Test]
        public void ExpandExamples_IndentsCodeToFence()
        {
            string page = "1. Passo:\n\n   {% code title=\"" + Source + ":a\" %}\n   ```csharp\n   ```\n   {% endcode %}\n";

            string expanded = Expand(page, Sections(("a", "    x();\n    \n    y();")), out List<string> problems);

            Assert.That(expanded, Is.EqualTo(
                "1. Passo:\n\n   {% code title=\"" + Source + ":a\" %}\n   ```csharp\n   x();\n\n   y();\n   ```\n   {% endcode %}\n"));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ExpandExamples_ReplacesOutdatedBlock()
        {
            string page = Marked(Source + ":um", "int velho = 0;", "int outro = 0;");

            string expanded = Expand(page, Sections(("um", "int novo = 1;")), out _);

            Assert.That(expanded, Is.EqualTo(Marked(Source + ":um", "int novo = 1;")));
        }

        [Test]
        public void ExpandExamples_ExpandedPageIsStable()
        {
            string source = Sections(("usings", "using A;\n"), ("corpo", "    A.Fazer();"));
            string once = Expand(Marked(Source + ":usings,corpo"), source, out _);

            string twice = Expand(once, source, out List<string> problems);

            Assert.That(twice, Is.EqualTo(once));
            Assert.That(once, Does.Contain("A.Fazer();"));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ExpandExamples_LeavesUnmarkedBlocksAlone()
        {
            string page = "# Título\n\n```csharp\nint escrito = 0;\n```\n\n```text\n{% code title=\"x\" %}\n```\n";

            string expanded = Expand(page, Sections(("um", "int a = 1;")), out List<string> problems);

            Assert.That(expanded, Is.EqualTo(page));
            Assert.That(problems, Is.Empty);
        }

        [Test]
        public void ExpandExamples_MissingFile_ReportsAndKeepsBlock()
        {
            string page = Marked("Src/NaoExiste.cs:um", "int a = 1;");

            string expanded = Expand(page, Sections(("um", "int a = 1;")), out List<string> problems);

            Assert.That(expanded, Is.EqualTo(page));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("linha 1").And.Contain("Src/NaoExiste.cs").And.Contain("arquivo não existe"));
        }

        [Test]
        public void ExpandExamples_MissingSection_Reports()
        {
            string page = Marked(Source + ":um,falta");

            string expanded = Expand(page, Sections(("um", "int a = 1;")), out List<string> problems);

            Assert.That(expanded, Is.EqualTo(page));
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("falta"));
        }

        [Test]
        public void ExpandExamples_MarkerWithoutCSharpBlock_Reports()
        {
            string page = "{% code title=\"" + Source + ":um\" %}\n```text\n```\n{% endcode %}\n";

            Expand(page, Sections(("um", "int a = 1;")), out List<string> problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("linha 1").And.Contain("bloco C#"));
        }

        [Test]
        public void ExpandExamples_MissingEndcode_Reports()
        {
            string page = "{% code title=\"" + Source + ":um\" %}\n```csharp\n```\nTexto.\n";

            Expand(page, Sections(("um", "int a = 1;")), out List<string> problems);

            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.Contain("{% endcode %}"));
        }

        private static string Expand(string page, string source, out List<string> problems)
        {
            var found = new List<string>();
            string result = ReferenceGenerator.ExpandExamples(page, path => path == Source ? source : null, found);
            problems = found;
            return result;
        }

        /// <summary>Página com um bloco marcado, com o código dado (vazio se não houver linhas).</summary>
        private static string Marked(string title, params string[] code)
        {
            var text = new StringBuilder();
            text.Append("{% code title=\"").Append(title).Append("\" %}\n```csharp\n");
            foreach (string line in code)
            {
                text.Append(line).Append('\n');
            }

            return text.Append("```\n{% endcode %}\n").ToString();
        }

        /// <summary>Arquivo de exemplos com as seções dadas, cada uma entre os comentários de início e fim.</summary>
        private static string Sections(params (string Name, string Code)[] sections)
        {
            var text = new StringBuilder("namespace Exemplos\n{\n");
            foreach ((string name, string code) in sections)
            {
                text.Append("    // --8<-- [start:").Append(name).Append("]\n");
                text.Append(code).Append('\n');
                text.Append("    // --8<-- [end:").Append(name).Append("]\n");
            }

            return text.Append("}\n").ToString();
        }

        private static void WriteFile(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
