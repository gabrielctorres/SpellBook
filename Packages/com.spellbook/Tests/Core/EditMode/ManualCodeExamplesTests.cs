using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Todo bloco C# do manual sai de um arquivo de exemplos que compila e roda nos testes, pelo
    /// <c>pymdownx.snippets</c> (Spec <c>referencia-catalogo-e-api.md</c>, D9; ADR 0041; CA10).
    /// </summary>
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

        private static readonly Regex Fence = new Regex(@"^\s*(?<fence>`{3,}|~{3,})\s*(?<lang>[^\s`]*)");
        private static readonly Regex SnippetLine = new Regex(@"^\s*-+8<-+\s+""(?<target>[^""]+)""\s*$");
        private static readonly string[] CSharpLanguages = { "csharp", "cs", "c#" };

        [Test]
        public void NoHandWrittenCSharpBlocks()
        {
            var handWritten = new List<string>();
            var cleanPending = new List<string>();

            foreach (string page in ManualPages())
            {
                List<string> lines = CSharpBlocks(page)
                    .SelectMany(block => block.Lines)
                    .Where(l => l.Text.Trim().Length > 0 && !SnippetLine.IsMatch(l.Text))
                    .Select(l => $"{page}:{l.Number}")
                    .ToList();

                bool pending = PendingPages.Contains(page);
                if (!pending)
                {
                    handWritten.AddRange(lines);
                }
                else if (lines.Count == 0)
                {
                    cleanPending.Add(page);
                }
            }

            Assert.That(handWritten, Is.Empty,
                "bloco C# escrito à mão no manual; inclua o trecho de um arquivo de exemplos (guia do manual, parte 9):\n"
                + string.Join("\n", handWritten));
            Assert.That(cleanPending, Is.Empty,
                "página sem bloco escrito à mão ainda está na lista de pendências; tire-a da lista:\n" + string.Join("\n", cleanPending));
        }

        [Test]
        public void SnippetsPointToExistingSections()
        {
            List<Snippet> snippets = AllSnippets().ToList();
            var broken = new List<string>();

            foreach (Snippet snippet in snippets)
            {
                string reason = BrokenReason(snippet);
                if (reason != null)
                {
                    broken.Add($"{snippet.Page}:{snippet.Line}: {snippet.Target}: {reason}");
                }
            }

            Assert.That(snippets, Is.Not.Empty, "o manual não inclui nenhum exemplo");
            Assert.That(broken, Is.Empty, "inclusão sem arquivo ou sem seção:\n" + string.Join("\n", broken));
        }

        [Test]
        public void SnippetSourcesAreCompiledCode()
        {
            string root = PackageFiles.RepositoryRoot;
            var outside = new List<string>();

            foreach (Snippet snippet in AllSnippets())
            {
                string allowed = snippet.Page == SandboxPage ? SandboxFolder : TestsFolder;
                string full = Path.GetFullPath(Path.Combine(root, snippet.Path));
                string folder = Path.GetFullPath(Path.Combine(root, allowed));
                bool inside = full.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
                if (!inside || !snippet.Path.EndsWith(".cs", StringComparison.Ordinal))
                {
                    outside.Add($"{snippet.Page}:{snippet.Line}: {snippet.Path} (permitido: {allowed}*.cs)");
                }
            }

            Assert.That(outside, Is.Empty, "inclusão de arquivo que não é código compilado dos testes:\n" + string.Join("\n", outside));
        }

        private static string BrokenReason(Snippet snippet)
        {
            if (snippet.Section == null)
            {
                return "sem seção (use arquivo.cs:nome)";
            }

            string file = Path.Combine(PackageFiles.RepositoryRoot, snippet.Path);
            if (!File.Exists(file))
            {
                return "arquivo não existe";
            }

            string[] lines = File.ReadAllLines(file);
            int start = Array.FindIndex(lines, l => IsMarker(l, "start", snippet.Section));
            int end = Array.FindIndex(lines, l => IsMarker(l, "end", snippet.Section));
            if (start < 0 || end < 0)
            {
                return $"seção {snippet.Section} sem start ou sem end";
            }

            return end > start ? null : $"seção {snippet.Section} com end antes do start";
        }

        private static bool IsMarker(string line, string type, string section)
        {
            return Regex.IsMatch(line, $@"-+8<-+\s+\[\s*{type}\s*:\s*{Regex.Escape(section)}\s*\]");
        }

        private static IEnumerable<Snippet> AllSnippets()
        {
            foreach (string page in ManualPages())
            {
                string[] lines = File.ReadAllLines(PagePath(page));
                for (int i = 0; i < lines.Length; i++)
                {
                    Match match = SnippetLine.Match(lines[i]);
                    if (match.Success)
                    {
                        yield return new Snippet(page, i + 1, match.Groups["target"].Value);
                    }
                }
            }
        }

        /// <summary>Blocos de código cercados com linguagem C#, inclusive recuados; cerca dentro de cerca maior é conteúdo.</summary>
        private static IEnumerable<Block> CSharpBlocks(string page)
        {
            string[] lines = File.ReadAllLines(PagePath(page));
            Block current = null;
            string open = null;

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Fence.Match(lines[i]);
                if (open == null)
                {
                    if (match.Success)
                    {
                        open = match.Groups["fence"].Value;
                        bool csharp = CSharpLanguages.Contains(match.Groups["lang"].Value.ToLowerInvariant());
                        current = csharp ? new Block() : null;
                    }

                    continue;
                }

                string trimmed = lines[i].Trim();
                bool closes = trimmed.Length >= open.Length && trimmed.Trim(open[0]).Length == 0;
                if (closes)
                {
                    if (current != null)
                    {
                        yield return current;
                    }

                    open = null;
                    current = null;
                    continue;
                }

                current?.Lines.Add((i + 1, lines[i]));
            }
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

        private sealed class Block
        {
            public readonly List<(int Number, string Text)> Lines = new List<(int Number, string Text)>();
        }

        private readonly struct Snippet
        {
            public Snippet(string page, int line, string target)
            {
                Page = page;
                Line = line;
                Target = target;
                int colon = target.LastIndexOf(':');
                Path = colon < 0 ? target : target.Substring(0, colon);
                Section = colon < 0 ? null : target.Substring(colon + 1);
            }

            public string Page { get; }

            public int Line { get; }

            public string Target { get; }

            public string Path { get; }

            public string Section { get; }
        }
    }
}
