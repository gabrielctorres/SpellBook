using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Tests.Documentation;
using SpellBook.Core.Tests.Structure;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Cada <c>[HelpURL]</c> e cada constante de <see cref="DocUrls"/> tem página e âncora de título no manual, e nenhuma
    /// página é só título ou "em breve" (CA54; ADR 0007, I26). No lugar do <c>mkdocs build --strict</c>, o sumário, os links
    /// e a sintaxe seguem o GitBook (ADR 0044, item 10; Spec <c>migracao-gitbook.md</c>, CA5). O manual fica em
    /// <c>Documentation~/manual/</c>, na raiz do repositório, fora do pacote (ADR 0027).
    /// </summary>
    public class ManualLinksTests
    {
        /// <summary>A página inicial do space, que o GitBook chama de <c>README.md</c> (ADR 0044, emenda de 2026-11-05, decisão 4).</summary>
        private const string HomePage = "README.md";

        private const string Summary = "SUMMARY.md";

        [Test]
        public void PackageFiles_RepositoryRoot_HasManual()
        {
            string expected = Path.GetFullPath(Path.Combine(PackageFiles.Root, "..", ".."));

            Assert.That(PackageFiles.RepositoryRoot, Is.EqualTo(expected));
            Assert.That(Directory.Exists(ManualRoot()), Is.True);
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void EveryHelpUrlHasPageAndAnchor()
        {
            List<string> urls = PackageAssemblies()
                .SelectMany(a => a.GetTypes())
                .Select(t => t.GetCustomAttribute<HelpURLAttribute>(false))
                .Where(h => h != null)
                .Select(h => h.URL)
                .Distinct()
                .ToList();

            List<string> broken = urls.Select(BrokenReason).Where(r => r != null).ToList();

            Assert.That(urls, Is.Not.Empty, "a varredura não achou nenhum [HelpURL]");
            Assert.That(broken, Is.Empty, "[HelpURL] sem página ou âncora no manual:\n" + string.Join("\n", broken));
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void EveryDocUrlHasPage()
        {
            List<string> broken = typeof(DocUrls)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => BrokenReason((string)f.GetRawConstantValue()))
                .Where(r => r != null)
                .ToList();

            Assert.That(broken, Is.Empty, "constante de DocUrls sem página no manual:\n" + string.Join("\n", broken));
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void NoStubPages()
        {
            string root = ManualRoot();
            List<string> pages = Directory.GetFiles(root, "*.md", SearchOption.AllDirectories).ToList();
            var stubs = new List<string>();

            foreach (string page in pages)
            {
                string[] lines = File.ReadAllLines(page);
                bool onlyTitles = lines.All(l => l.Trim().Length == 0 || l.TrimStart().StartsWith("#", StringComparison.Ordinal));
                bool soon = lines.Any(l => l.IndexOf("em breve", StringComparison.OrdinalIgnoreCase) >= 0);
                if (onlyTitles || soon)
                {
                    stubs.Add(page.Substring(root.Length).Replace('\\', '/'));
                }
            }

            Assert.That(pages, Is.Not.Empty, "o manual não tem páginas");
            Assert.That(stubs, Is.Empty, "páginas só com título ou \"em breve\":\n" + string.Join("\n", stubs));
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void SummaryListsEveryPageOnce()
        {
            string summary = ReadManualFile(Summary);

            Assert.That(summary, Is.Not.Null, $"o manual não tem o {Summary} na raiz");
            List<string> problems = ManualMarkdown.SummaryProblems(summary, PagePaths());
            Assert.That(problems, Is.Empty, $"{Summary} fora de dia com as páginas:\n" + string.Join("\n", problems));
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void EveryRelativeLinkHasTarget()
        {
            List<string> broken = PagePaths()
                .SelectMany(p => ManualMarkdown.BrokenLinks(p, ReadManualFile(p), DocUrls.Base, ReadManualFile))
                .ToList();

            Assert.That(broken, Is.Empty, "link do manual sem destino:\n" + string.Join("\n", broken));
        }

        [Test]
        [Category(TestCategories.Documentation)]
        public void NoMkDocsSyntax()
        {
            List<string> leftovers = PagePaths()
                .SelectMany(p => ManualMarkdown.MkDocsLeftovers(ReadManualFile(p)).Select(l => $"{p}:{l}"))
                .ToList();

            Assert.That(leftovers, Is.Empty,
                "sintaxe do MkDocs no manual ({#, !!!, ???, --8<--); use a do GitBook (ADR 0044, item 11):\n"
                + string.Join("\n", leftovers));
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

        /// <summary>O motivo de a URL não ter destino no manual, ou <c>null</c> se a página e a âncora existem.</summary>
        private static string BrokenReason(string url)
        {
            if (!url.StartsWith(DocUrls.Base, StringComparison.Ordinal))
            {
                return $"{url}: fora de DocUrls.Base";
            }

            string[] parts = url.Substring(DocUrls.Base.Length).Split('#');
            string path = parts[0].Trim('/');
            string page = Path.Combine(ManualRoot(), path.Length == 0 ? HomePage : path + ".md");
            if (!File.Exists(page))
            {
                return $"{url}: página não existe ({page})";
            }

            if (parts.Length < 2 || parts[1].Length == 0)
            {
                return null;
            }

            return ManualMarkdown.Anchors(File.ReadAllText(page)).Contains(parts[1])
                ? null
                : $"{url}: nenhum título da página tem a âncora #{parts[1]}";
        }

        /// <summary>As páginas do manual, com o caminho a partir da raiz dele, sem o próprio sumário.</summary>
        private static List<string> PagePaths()
        {
            string root = ManualRoot();
            return Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)
                .Select(f => f.Substring(root.Length + 1).Replace('\\', '/'))
                .Where(p => p != Summary)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();
        }

        private static string ReadManualFile(string path)
        {
            string file = Path.Combine(ManualRoot(), path);
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }

        private static string ManualRoot()
        {
            string root = Path.Combine(PackageFiles.RepositoryRoot, "Documentation~", "manual");
            if (!Directory.Exists(root))
            {
                Assert.Fail($"manual não encontrado em {root}; os testes rodam só no projeto de desenvolvimento do repositório");
            }

            return root;
        }
    }
}
