using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Tests.Structure;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Cada <c>[HelpURL]</c> e cada constante de <see cref="DocUrls"/> tem página e âncora <c>{#id}</c> no manual, e nenhuma
    /// página é só título ou "em breve" (CA54; ADR 0007, I26). O manual fica em <c>Documentation~/manual/</c>, na raiz do
    /// repositório, fora do pacote (ADR 0027).
    /// </summary>
    public class ManualLinksTests
    {
        [Test]
        public void PackageFiles_RepositoryRoot_HasManual()
        {
            string expected = Path.GetFullPath(Path.Combine(PackageFiles.Root, "..", ".."));

            Assert.That(PackageFiles.RepositoryRoot, Is.EqualTo(expected));
            Assert.That(Directory.Exists(ManualRoot()), Is.True);
        }

        [Test]
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
            string page = Path.Combine(ManualRoot(), path.Length == 0 ? "index.md" : path + ".md");
            if (!File.Exists(page))
            {
                return $"{url}: página não existe ({page})";
            }

            if (parts.Length < 2 || parts[1].Length == 0)
            {
                return null;
            }

            var anchor = new Regex(@"\{\s*#" + Regex.Escape(parts[1]) + @"(\s[^}]*)?\}");
            return anchor.IsMatch(File.ReadAllText(page)) ? null : $"{url}: âncora {{#{parts[1]}}} não existe";
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
