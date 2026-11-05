using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Todo bloco C# do manual tem o marcador <c>{% code title="caminho:seção,..." %}</c> e é igual às seções atuais de
    /// um arquivo de exemplos que compila e roda nos testes (ADR 0041, item 2; ADR 0044, item 8; Spec
    /// <c>migracao-gitbook.md</c>, CA4 e GB4).
    /// </summary>
    [Category(TestCategories.Documentation)]
    public class ManualCodeExamplesTests
    {
        /// <summary>
        /// Páginas que ainda têm bloco C# escrito à mão. A lista só diminui: cada ticket de migração tira as suas páginas,
        /// e a página que sai da lista não pode voltar a ter bloco escrito à mão.
        /// </summary>
        private static readonly string[] PendingPages =
        {
            "modulos/gas.md",
            "modulos/dano-escudo-e-morte.md",
            "regras/migracao-2-para-3.md",
        };

        private const string SandboxPage = "exemplos/sandbox.md";
        private const string TestsFolder = "Packages/com.spellbook/Tests/";
        private const string SandboxFolder = "Packages/com.spellbook/Samples~/Sandbox/Scripts/";
        private const string Instruction = "rode SpellBook > Documentação > Gerar catálogo e exemplos e commite o resultado";

        private static readonly Regex Fence = new Regex(@"^\s*(?<fence>`{3,}|~{3,})\s*(?<lang>[^\s`]*)");
        private static readonly Regex Marker = new Regex(@"^\s*\{%\s*code\s+title=""(?<title>[^""]*)""\s*%\}\s*$");
        private static readonly string[] CSharpLanguages = { "csharp", "cs", "c#" };

        [Test]
        public void NoHandWrittenCSharpBlocks()
        {
            var handWritten = new List<string>();
            var cleanPending = new List<string>();

            foreach (string page in ManualPages())
            {
                List<string> unmarked = CSharpBlocks(page)
                    .Where(block => !block.Marked)
                    .Select(block => $"{page}:{block.Line}")
                    .ToList();

                bool pending = PendingPages.Contains(page);
                if (!pending)
                {
                    handWritten.AddRange(unmarked);
                }
                else if (unmarked.Count == 0)
                {
                    cleanPending.Add(page);
                }
            }

            Assert.That(handWritten, Is.Empty,
                "bloco C# sem marcador no manual; ponha {% code title=\"caminho:seção\" %} antes da cerca (guia do manual, parte 9):\n"
                + string.Join("\n", handWritten));
            Assert.That(cleanPending, Is.Empty,
                "página sem bloco escrito à mão ainda está na lista de pendências; tire-a da lista:\n" + string.Join("\n", cleanPending));
        }

        [Test]
        public void SnippetsPointToExistingSections()
        {
            var broken = new List<string>();

            foreach (string page in ManualPages())
            {
                var problems = new List<string>();
                ReferenceGenerator.ExpandExamples(PageText(page), ReadSource, problems);
                broken.AddRange(problems.Select(p => $"{page}: {p}"));
            }

            Assert.That(AllMarkers().ToList(), Is.Not.Empty, "o manual não tem nenhum exemplo marcado");
            Assert.That(broken, Is.Empty, "marcador sem arquivo, sem seção ou sem bloco C#:\n" + string.Join("\n", broken));
        }

        [Test]
        public void SnippetSourcesAreCompiledCode()
        {
            string root = PackageFiles.RepositoryRoot;
            var outside = new List<string>();

            foreach ((string page, int line, string path) in AllMarkers())
            {
                string allowed = page == SandboxPage ? SandboxFolder : TestsFolder;
                string full = Path.GetFullPath(Path.Combine(root, path));
                string folder = Path.GetFullPath(Path.Combine(root, allowed));
                bool inside = full.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
                if (!inside || !path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    outside.Add($"{page}:{line}: {path} (permitido: {allowed}*.cs)");
                }
            }

            Assert.That(outside, Is.Empty, "marcador de arquivo que não é código compilado dos testes:\n" + string.Join("\n", outside));
        }

        [Test]
        public void ExpandedBlocksMatchSections()
        {
            List<string> outdated = ManualPages()
                .Where(page => ReferenceGenerator.ExpandExamples(PageText(page), ReadSource, new List<string>()) != PageText(page))
                .ToList();

            Assert.That(outdated, Is.Empty, $"bloco C# diferente da seção marcada; {Instruction}:\n" + string.Join("\n", outdated));
        }

        private static IEnumerable<(string Page, int Line, string Path)> AllMarkers()
        {
            foreach (string page in ManualPages())
            {
                string[] lines = File.ReadAllLines(PagePath(page));
                for (int i = 0; i < lines.Length; i++)
                {
                    Match match = Marker.Match(lines[i]);
                    if (match.Success)
                    {
                        string title = match.Groups["title"].Value;
                        int colon = title.LastIndexOf(':');
                        yield return (page, i + 1, colon < 0 ? title : title.Substring(0, colon));
                    }
                }
            }
        }

        /// <summary>Blocos de código cercados com linguagem C#, inclusive recuados; cerca dentro de cerca maior é conteúdo.</summary>
        private static IEnumerable<Block> CSharpBlocks(string page)
        {
            string[] lines = File.ReadAllLines(PagePath(page));
            string open = null;

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Fence.Match(lines[i]);
                if (open == null)
                {
                    if (match.Success)
                    {
                        open = match.Groups["fence"].Value;
                        if (CSharpLanguages.Contains(match.Groups["lang"].Value.ToLowerInvariant()))
                        {
                            yield return new Block(i + 1, i > 0 && Marker.IsMatch(lines[i - 1]));
                        }
                    }

                    continue;
                }

                string trimmed = lines[i].Trim();
                if (trimmed.Length >= open.Length && trimmed.Trim(open[0]).Length == 0)
                {
                    open = null;
                }
            }
        }

        private static string ReadSource(string path)
        {
            string file = Path.Combine(PackageFiles.RepositoryRoot, path);
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }

        private static string PageText(string page)
        {
            // O git pode trocar \n por \r\n na cópia de trabalho (core.autocrlf); a geração escreve só \n.
            return File.ReadAllText(PagePath(page)).Replace("\r\n", "\n");
        }

        private static IEnumerable<string> ManualPages()
        {
            string root = ManualRoot();
            return Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)
                .Select(p => p.Substring(root.Length + 1).Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        private static string PagePath(string page)
        {
            return Path.Combine(ManualRoot(), page);
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

        private readonly struct Block
        {
            public Block(int line, bool marked)
            {
                Line = line;
                Marked = marked;
            }

            public int Line { get; }

            public bool Marked { get; }
        }
    }
}
