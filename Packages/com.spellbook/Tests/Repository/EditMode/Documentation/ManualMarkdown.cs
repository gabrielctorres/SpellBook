using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Regras do Markdown do GitBook que as guardas do manual conferem: o slug dos títulos, os links relativos, o sumário e
    /// as sobras de sintaxe do MkDocs (ADR 0044, itens 9 e 10; Spec <c>migracao-gitbook.md</c>, CA5 e GB7a). Os caminhos
    /// são relativos à raiz do manual, com <c>/</c>.
    /// </summary>
    public static class ManualMarkdown
    {
        private static readonly Regex Fence = new Regex(@"^\s*(?<fence>`{3,}|~{3,})(?<info>.*)$");
        private static readonly Regex Heading = new Regex(@"^(?<level>#{1,6})\s+(?<title>.*?)\s*$");
        private static readonly Regex InlineCode = new Regex(@"`[^`]*`");
        private static readonly Regex InlineLink = new Regex(@"\[(?<text>[^\]]*)\]\((?<target>[^)\s]*)(\s+""[^""]*"")?\)");
        private static readonly Regex AutoLink = new Regex(@"<(?<target>[a-zA-Z][a-zA-Z0-9+.-]*:[^>\s]*)>");
        private static readonly Regex Scheme = new Regex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:");
        private static readonly Regex SummaryEntry = new Regex(@"^\s*[*-]\s+\[[^\]]*\]\((?<target>[^)\s#]+)[^)]*\)");
        private static readonly Regex MkDocsSyntax = new Regex(@"\{#|^\s*(!!!|\?\?\?)");
        private const string SnippetsSyntax = "--8<--";

        /// <summary>A âncora que o GitBook gera para o título.</summary>
        public static string Slug(string title)
        {
            string text = InlineLink.Replace(title, "${text}");
            text = Regex.Replace(text, @"\\(.)", "$1").Replace("`", string.Empty).Replace("*", string.Empty);
            text = WithoutAccents(text).ToLowerInvariant();
            text = text.Replace("&", " and ").Replace("<", " less-than ").Replace(">", " greater-than ");

            var slug = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool keptDot = c == '.' && i > 0 && char.IsDigit(text[i - 1]);
                if ((c >= 'a' && c <= 'z') || char.IsDigit(c) || keptDot)
                {
                    slug.Append(c);
                }
                else if (char.IsWhiteSpace(c) || c == '-')
                {
                    slug.Append('-');
                }
            }

            string result = Regex.Replace(slug.ToString(), "-+", "-").Trim('-');
            return result.Length > 0 && char.IsDigit(result[0]) ? "id-" + result : result;
        }

        /// <summary>
        /// As âncoras da página: os títulos de <c>##</c> a <c>####</c> fora de bloco de código. O primeiro <c>#</c> é o
        /// título da página, e <c>#####</c> e <c>######</c> viram negrito, sem âncora. Título repetido ganha <c>-1</c>, <c>-2</c>.
        /// </summary>
        public static HashSet<string> Anchors(string page)
        {
            var anchors = new HashSet<string>();
            var seen = new Dictionary<string, int>();
            bool pageTitle = true;

            foreach ((string line, int _) in OutsideCode(page))
            {
                Match heading = Heading.Match(line);
                if (!heading.Success)
                {
                    continue;
                }

                int level = heading.Groups["level"].Length;
                if (level == 1 && pageTitle)
                {
                    pageTitle = false;
                    continue;
                }

                if (level > 4)
                {
                    continue;
                }

                string slug = Slug(heading.Groups["title"].Value);
                seen.TryGetValue(slug, out int count);
                seen[slug] = count + 1;
                anchors.Add(count == 0 ? slug : $"{slug}-{count}");
            }

            return anchors;
        }

        /// <summary>
        /// Os links da página sem destino: arquivo ou âncora que não existe, caminho fora do manual, caminho que começa
        /// em <c>/</c> e link absoluto para o próprio site. <paramref name="readPage"/> devolve o texto de um arquivo pelo
        /// caminho, ou <c>null</c> se ele não existe.
        /// </summary>
        public static List<string> BrokenLinks(string pagePath, string page, string siteBase, Func<string, string> readPage)
        {
            var broken = new List<string>();

            foreach ((string line, int number) in OutsideCode(page))
            {
                string text = InlineCode.Replace(line, string.Empty);
                IEnumerable<string> targets = InlineLink.Matches(text).Cast<Match>()
                    .Concat(AutoLink.Matches(text).Cast<Match>())
                    .Select(m => m.Groups["target"].Value);

                foreach (string target in targets)
                {
                    string reason = LinkProblem(pagePath, page, target, siteBase, readPage);
                    if (reason != null)
                    {
                        broken.Add($"{pagePath}:{number}: {target}: {reason}");
                    }
                }
            }

            return broken;
        }

        /// <summary>Página fora do sumário, entrada repetida e entrada sem arquivo.</summary>
        public static List<string> SummaryProblems(string summary, IEnumerable<string> pagePaths)
        {
            List<string> entries = Lines(summary)
                .Select(line => SummaryEntry.Match(line))
                .Where(m => m.Success)
                .Select(m => Normalize("", Uri.UnescapeDataString(m.Groups["target"].Value)))
                .ToList();
            List<string> pages = pagePaths.ToList();
            var problems = new List<string>();

            problems.AddRange(pages.Where(p => !entries.Contains(p)).Select(p => $"{p}: fora do SUMMARY.md"));
            problems.AddRange(entries.GroupBy(e => e).Where(g => g.Count() > 1)
                .Select(g => $"{g.Key}: aparece {g.Count()} vezes no SUMMARY.md"));
            problems.AddRange(entries.Distinct().Where(e => !pages.Contains(e)).Select(e => $"{e}: entrada do SUMMARY.md sem arquivo"));
            return problems;
        }

        /// <summary>
        /// As linhas com sintaxe do MkDocs: <c>{#</c>, <c>!!!</c> e <c>???</c> fora de bloco de código, e <c>--8<--</c> em
        /// qualquer lugar, porque o MkDocs o usava dentro dos blocos.
        /// </summary>
        public static List<string> MkDocsLeftovers(string page)
        {
            var outside = new HashSet<int>(OutsideCode(page).Select(l => l.Number));
            var leftovers = new List<string>();
            string[] lines = Lines(page);

            for (int i = 0; i < lines.Length; i++)
            {
                int number = i + 1;
                bool mkDocs = outside.Contains(number) && MkDocsSyntax.IsMatch(lines[i]);
                if (mkDocs || lines[i].Contains(SnippetsSyntax))
                {
                    leftovers.Add($"{number}: {lines[i].Trim()}");
                }
            }

            return leftovers;
        }

        private static string LinkProblem(string pagePath, string page, string target, string siteBase,
            Func<string, string> readPage)
        {
            if (target.StartsWith(siteBase, StringComparison.OrdinalIgnoreCase))
            {
                return "link absoluto para o próprio site; use o link relativo para o .md";
            }

            if (Scheme.IsMatch(target))
            {
                return null;
            }

            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                return "caminho a partir da raiz do site; use o link relativo para o .md";
            }

            string[] parts = target.Split(new[] { '#' }, 2);
            // O ".." tira o nome da página e deixa a pasta dela como ponto de partida.
            string path = Normalize(pagePath, "../" + Uri.UnescapeDataString(parts[0]));
            if (path == null)
            {
                return "fora da pasta do manual";
            }

            string destination = parts[0].Length == 0 ? page : readPage(path);
            if (destination == null)
            {
                return $"arquivo não existe ({path})";
            }

            bool hasAnchor = parts.Length > 1 && parts[1].Length > 0;
            bool markdown = parts[0].Length == 0 || path.EndsWith(".md", StringComparison.Ordinal);
            if (hasAnchor && markdown && !Anchors(destination).Contains(parts[1]))
            {
                return $"âncora #{parts[1]} não existe em {(parts[0].Length == 0 ? pagePath : path)}";
            }

            return null;
        }

        /// <summary>O caminho a partir da raiz do manual, ou <c>null</c> se ele sai do manual.</summary>
        private static string Normalize(string start, string relative)
        {
            var parts = new List<string>(start.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (string part in relative.Split('/'))
            {
                if (part == "..")
                {
                    if (parts.Count == 0)
                    {
                        return null;
                    }

                    parts.RemoveAt(parts.Count - 1);
                }
                else if (part.Length > 0 && part != ".")
                {
                    parts.Add(part);
                }
            }

            return string.Join("/", parts);
        }

        /// <summary>As linhas fora de bloco de código cercado, com o número de cada uma (a partir de 1).</summary>
        private static IEnumerable<(string Line, int Number)> OutsideCode(string page)
        {
            string[] lines = Lines(page);
            string fence = null;

            for (int i = 0; i < lines.Length; i++)
            {
                Match match = Fence.Match(lines[i]);
                if (fence == null && match.Success)
                {
                    fence = match.Groups["fence"].Value;
                }
                else if (fence != null && match.Success && match.Groups["info"].Value.Trim().Length == 0
                    && match.Groups["fence"].Value.StartsWith(fence, StringComparison.Ordinal))
                {
                    fence = null;
                }
                else if (fence == null)
                {
                    yield return (lines[i], i + 1);
                }
            }
        }

        private static string[] Lines(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n');
        }

        private static string WithoutAccents(string text)
        {
            return new string(text.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
        }
    }
}
