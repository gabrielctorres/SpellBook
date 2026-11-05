using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Gera a página do catálogo de tipos em <c>manual/referencia/catalogo.md</c> e expande os exemplos C# marcados das
    /// páginas do manual (ADR 0040, itens 1 e 4; ADR 0044, itens 7 e 8). Não há referência da API gerada (ADR 0044,
    /// item 6).
    /// </summary>
    public static class ReferenceGenerator
    {
        /// <summary>Caminho do item de menu que gera o catálogo e os exemplos.</summary>
        public const string MenuPath = "SpellBook/Documentação/Gerar catálogo e exemplos";

        private static readonly Regex MarkerLine = new Regex(@"^\s*\{%\s*code\s+title=""(?<title>[^""]*)""\s*%\}\s*$");
        private static readonly Regex FenceLine = new Regex(@"^(?<indent>\s*)(?<fence>`{3,}|~{3,})\s*(?<lang>[^\s`]*)");
        private static readonly Regex EndcodeLine = new Regex(@"^\s*\{%\s*endcode\s*%\}\s*$");
        private static readonly string[] CSharpLanguages = { "csharp", "cs", "c#" };

        /// <summary>Gera o catálogo e os exemplos na pasta <c>Documentation~/</c> do projeto aberto e registra o resultado no log.</summary>
        [MenuItem(MenuPath)]
        public static void GenerateFromMenu()
        {
            try
            {
                Generate(DefaultDocumentationRoot());
                Debug.Log("Catálogo e exemplos gerados. Commite o manual/referencia/catalogo.md e as páginas do manual que mudaram.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>Entrada do <c>-executeMethod</c> em batchmode. Sai com 0 se gerou e com 1 se falhou.</summary>
        public static void GenerateFromCommandLine()
        {
            int code = 0;
            try
            {
                Generate(DefaultDocumentationRoot());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                code = 1;
            }

            EditorApplication.Exit(code);
        }

        /// <summary>
        /// Gera o catálogo e expande os exemplos das páginas de <c>manual/</c> em <paramref name="documentationRoot"/>, a
        /// pasta <c>Documentation~/</c> na raiz do repositório. Grava só a página que muda. Lança
        /// <see cref="InvalidOperationException"/> com a lista dos marcadores quebrados, depois de expandir os outros.
        /// </summary>
        public static void Generate(string documentationRoot)
        {
            string root = Path.GetFullPath(documentationRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            IReadOnlyList<TypeCatalogSection> sections = TypeCatalog.Build();
            WarnMissingSummaries(sections);
            Write(Path.Combine(root, "manual", "referencia", "catalogo.md"), TypeCatalogPage.Render(sections));

            List<string> problems = ExpandManualPages(Path.Combine(root, "manual"), Path.GetDirectoryName(root));
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("Marcadores de exemplo quebrados no manual:\n" + string.Join("\n", problems));
            }
        }

        /// <summary>
        /// Texto de <paramref name="page"/> com cada bloco C# marcado por <c>{% code title="caminho:seção,..." %}</c>
        /// trocado pelas seções do arquivo, na ordem do título, cada uma sem o seu recuo comum e com o recuo da cerca. A
        /// saída usa só <c>\n</c>. Um marcador quebrado deixa o bloco como está e entra em <paramref name="problems"/>.
        /// </summary>
        /// <param name="page">Texto da página.</param>
        /// <param name="readSource">Texto do arquivo no caminho a partir da raiz do repositório, ou <c>null</c> se não existe.</param>
        /// <param name="problems">Recebe uma linha por marcador quebrado.</param>
        public static string ExpandExamples(string page, Func<string, string> readSource, ICollection<string> problems)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (readSource == null)
            {
                throw new ArgumentNullException(nameof(readSource));
            }

            if (problems == null)
            {
                throw new ArgumentNullException(nameof(problems));
            }

            string[] lines = page.Replace("\r\n", "\n").Split('\n');
            var output = new List<string>(lines.Length);
            string open = null;

            for (int i = 0; i < lines.Length; i++)
            {
                if (open != null)
                {
                    output.Add(lines[i]);
                    open = Closes(lines[i], open) ? null : open;
                    continue;
                }

                Match marker = MarkerLine.Match(lines[i]);
                if (marker.Success)
                {
                    i = ExpandBlock(lines, i, marker.Groups["title"].Value, readSource, problems, output);
                    continue;
                }

                Match fence = FenceLine.Match(lines[i]);
                open = fence.Success ? fence.Groups["fence"].Value : null;
                output.Add(lines[i]);
            }

            return string.Join("\n", output);
        }

        /// <summary>Escreve o marcador da linha <paramref name="at"/> e o bloco dele, e devolve a última linha consumida.</summary>
        private static int ExpandBlock(string[] lines, int at, string title, Func<string, string> readSource,
            ICollection<string> problems, List<string> output)
        {
            output.Add(lines[at]);
            string where = $"linha {at + 1}: {title}";

            Match fence = at + 1 < lines.Length ? FenceLine.Match(lines[at + 1]) : Match.Empty;
            if (!fence.Success || !CSharpLanguages.Contains(fence.Groups["lang"].Value.ToLowerInvariant()))
            {
                problems.Add($"{where}: o marcador não vem logo antes de um bloco C#");
                return at;
            }

            int close = at + 2;
            while (close < lines.Length && !Closes(lines[close], fence.Groups["fence"].Value))
            {
                close++;
            }

            if (close + 1 >= lines.Length || !EndcodeLine.IsMatch(lines[close + 1]))
            {
                problems.Add($"{where}: falta o {{% endcode %}} logo depois do bloco");
                return at;
            }

            List<string> code = Sections(title, readSource, out string reason);
            if (reason != null)
            {
                problems.Add($"{where}: {reason}");
                output.AddRange(lines.Skip(at + 1).Take(close - at));
                return close;
            }

            string indent = fence.Groups["indent"].Value;
            output.Add(lines[at + 1]);
            output.AddRange(code.Select(line => line.Length == 0 ? line : indent + line));
            output.Add(lines[close]);
            return close;
        }

        /// <summary>Linhas das seções do título, na ordem, ou <c>null</c> com o motivo em <paramref name="reason"/>.</summary>
        private static List<string> Sections(string title, Func<string, string> readSource, out string reason)
        {
            int colon = title.LastIndexOf(':');
            if (colon < 0)
            {
                reason = "sem seção (use caminho:seção)";
                return null;
            }

            string text = readSource(title.Substring(0, colon));
            if (text == null)
            {
                reason = "arquivo não existe";
                return null;
            }

            string[] source = text.Replace("\r\n", "\n").Split('\n');
            var code = new List<string>();
            foreach (string name in title.Substring(colon + 1).Split(','))
            {
                int start = Array.FindIndex(source, l => IsSectionMarker(l, "start", name.Trim()));
                int end = Array.FindIndex(source, l => IsSectionMarker(l, "end", name.Trim()));
                if (name.Trim().Length == 0 || start < 0 || end < 0 || end < start)
                {
                    reason = $"seção {name.Trim()} sem start, sem end ou com end antes do start";
                    return null;
                }

                code.AddRange(Dedent(source.Skip(start + 1).Take(end - start - 1).ToList()));
            }

            reason = null;
            return code;
        }

        private static bool IsSectionMarker(string line, string type, string name)
        {
            return Regex.IsMatch(line, $@"-+8<-+\s+\[\s*{type}\s*:\s*{Regex.Escape(name)}\s*\]");
        }

        /// <summary>Tira o recuo comum das linhas não vazias; linha só de espaços vira vazia.</summary>
        private static IEnumerable<string> Dedent(List<string> lines)
        {
            List<string> filled = lines.Where(l => l.Trim().Length > 0).ToList();
            int common = filled.Count == 0 ? 0 : filled.Min(l => l.Length - l.TrimStart().Length);
            string prefix = filled.Count == 0 ? string.Empty : filled[0].Substring(0, common);
            while (prefix.Length > 0 && !filled.All(l => l.StartsWith(prefix, StringComparison.Ordinal)))
            {
                prefix = prefix.Substring(0, prefix.Length - 1);
            }

            return lines.Select(l => l.Trim().Length == 0 ? string.Empty : l.Substring(prefix.Length));
        }

        private static bool Closes(string line, string fence)
        {
            string trimmed = line.Trim();
            return trimmed.Length >= fence.Length && trimmed.Trim(fence[0]).Length == 0;
        }

        private static List<string> ExpandManualPages(string manualRoot, string repositoryRoot)
        {
            var problems = new List<string>();
            IEnumerable<string> pages = Directory.GetFiles(manualRoot, "*.md", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal);
            foreach (string page in pages)
            {
                string text = File.ReadAllText(page).Replace("\r\n", "\n");
                var pageProblems = new List<string>();
                string expanded = ExpandExamples(text, path => ReadSource(repositoryRoot, path), pageProblems);
                if (expanded != text)
                {
                    Write(page, expanded);
                }

                string relative = page.Substring(manualRoot.Length + 1).Replace('\\', '/');
                problems.AddRange(pageProblems.Select(p => $"{relative}: {p}"));
            }

            return problems;
        }

        private static string ReadSource(string repositoryRoot, string path)
        {
            string file = Path.Combine(repositoryRoot, path);
            return File.Exists(file) ? File.ReadAllText(file) : null;
        }

        private static string DefaultDocumentationRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Documentation~"));
        }

        private static void WarnMissingSummaries(IReadOnlyList<TypeCatalogSection> sections)
        {
            List<string> missing = sections.SelectMany(s => s.Entries)
                .Where(e => string.IsNullOrEmpty(e.Summary))
                .Select(e => e.Type.FullName)
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (missing.Count > 0)
            {
                Debug.LogWarning($"Tipos do catálogo sem Summary no [TypeCategory] ({missing.Count}):\n" + string.Join("\n", missing));
            }
        }

        private static void Write(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
