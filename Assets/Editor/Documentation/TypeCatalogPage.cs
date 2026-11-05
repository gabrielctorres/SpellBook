using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using SpellBook.Core.Editor.Inspector;

namespace SpellBook.Core.Editor.Documentation
{
    /// <summary>
    /// Monta a página Markdown do catálogo de tipos. A saída usa só <c>\n</c>, termina com uma quebra de linha e não tem
    /// data nem hora: as mesmas seções sempre geram o mesmo texto.
    /// </summary>
    public static class TypeCatalogPage
    {
        private const string Intro =
            "Esta página lista os tipos que o seletor do Inspector oferece nos campos de ações, reações, respostas e"
            + " elementos do pacote.";

        private const string RootCategory = "Raiz";

        /// <summary>Texto da página para as seções dadas, na ordem em que vêm.</summary>
        public static string Render(IReadOnlyList<TypeCatalogSection> sections)
        {
            if (sections == null)
            {
                throw new ArgumentNullException(nameof(sections));
            }

            var text = new StringBuilder();
            Line(text, "# Catálogo de tipos");
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
            Line(text, $"## `{Title(section.BaseType)}`");
            Line(text, string.Empty);
            Line(text, $"Aparece em: `{string.Join("`, `", section.Fields)}`.");
            Line(text, string.Empty);
            Line(text, "| Tipo | Categoria | Resumo |");
            Line(text, "|---|---|---|");
            foreach (TypePickerEntry entry in section.Entries)
            {
                string category = string.IsNullOrEmpty(entry.CategoryPath) ? RootCategory : entry.CategoryPath;
                Line(text, $"| {Cell(entry.DisplayName)} | {Cell(category)} | {Cell(entry.Summary)} |");
            }
        }

        private static string Title(Type type)
        {
            if (!type.IsConstructedGenericType)
            {
                return type.Name;
            }

            var title = new StringBuilder(WithoutArity(type.Name)).Append('<');
            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                title.Append(i == 0 ? string.Empty : ", ").Append(Title(arguments[i]));
            }

            return title.Append('>').ToString();
        }

        private static string WithoutArity(string name)
        {
            return Regex.Replace(name, "`[0-9]+", string.Empty);
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
