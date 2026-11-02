using System;
using System.Collections.Generic;
using System.Text;
using SpellBook.Core.Editor.Inspector;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Monta a página Markdown do catálogo de tipos. A saída usa só <c>\n</c>, termina com uma quebra de linha e não tem
    /// data nem hora: as mesmas seções sempre geram o mesmo texto.
    /// </summary>
    public static class TypeCatalogPage
    {
        private const string Notice =
            "<!-- Página gerada do código por SpellBook.Core.Editor.Documentation. Não edite: mude o [TypeCategory] do tipo"
            + " e rode SpellBook > Documentação > Gerar referência. -->";

        private const string Intro =
            "Esta página lista os tipos que o seletor do Inspector oferece nos campos de ações, reações, respostas e"
            + " elementos do pacote. Clique no nome de um tipo para abrir a página dele na referência da interface de"
            + " programação (API).";

        private const string RootCategory = "Raiz";

        /// <summary>Texto da página para as seções dadas, na ordem em que vêm.</summary>
        public static string Render(IReadOnlyList<TypeCatalogSection> sections)
        {
            if (sections == null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var text = new StringBuilder();
            Line(text, Notice);
            Line(text, "# Catálogo de tipos {#catalogo}");
            Line(text, string.Empty);
            Line(text, Intro);
            foreach (TypeCatalogSection section in sections)
            {
                RenderSection(text, section);
            }

            return text.ToString();
        }

        private static void RenderSection(StringBuilder text, TypeCatalogSection section)
        {
            Line(text, string.Empty);
            Line(text, $"## `{section.BaseType.Name}` {{#{Anchor(section.BaseType)}}}");
            Line(text, string.Empty);
            Line(text, $"Aparece em: `{string.Join("`, `", section.Fields)}`.");
            Line(text, string.Empty);
            Line(text, "| Tipo | Categoria | Resumo |");
            Line(text, "|---|---|---|");
            foreach (TypePickerEntry entry in section.Entries)
            {
                string category = string.IsNullOrEmpty(entry.CategoryPath) ? RootCategory : entry.CategoryPath;
                Line(text, $"| [{Cell(entry.DisplayName)}]({ApiUrl(entry.Type)}) | {Cell(category)} | {Cell(entry.Summary)} |");
            }
        }

        private static string Anchor(Type type)
        {
            return type.FullName.ToLowerInvariant().Replace('.', '-').Replace('+', '-');
        }

        private static string ApiUrl(Type type)
        {
            return DocUrls.Base + "api/" + type.FullName.Replace('+', '.') + ".html";
        }

        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace("|", "\\|");
        }

        private static void Line(StringBuilder text, string line)
        {
            text.Append(line).Append('\n');
        }
    }
}
