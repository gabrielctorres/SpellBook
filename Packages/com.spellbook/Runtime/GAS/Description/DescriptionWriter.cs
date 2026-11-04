using System;
using SpellBook.Core.Text;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// Texto simples de uma <see cref="DescriptionLine"/>, escrito num <see cref="TextBuffer"/> sem marcação (CA8). Usado
    /// pela prévia do Inspector e pelas fontes de tooltip. Não aloca.
    /// </summary>
    public static class DescriptionWriter
    {
        /// <summary>
        /// Texto do valor no fim de <paramref name="into"/>: nas linhas de atributo, modificador e custo, pelo formatador do
        /// <paramref name="context"/>. As outras linhas não têm texto de valor.
        /// </summary>
        public static void WriteValue(in DescriptionLine line, DescriptionContext context, TextBuffer into)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (into == null) throw new ArgumentNullException(nameof(into));

            if (!HasValueText(line.Kind)) return;

            ModifierTextFormatter formatter = context.Formatter ?? DefaultModifierTextFormatter.Instance;
            formatter.WriteValue(line, into, context.Culture);
        }

        /// <summary>Linha inteira no fim de <paramref name="into"/>: rótulo pelo provedor do contexto, espaço e texto do valor.</summary>
        public static void WriteLine(in DescriptionLine line, DescriptionContext context, TextBuffer into)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (into == null) throw new ArgumentNullException(nameof(into));

            TextSpan label = into.Append(line.Label.Resolve(context.Text));
            if (!HasValueText(line.Kind)) return;

            if (!label.IsEmpty) into.Append(" ");
            WriteValue(line, context, into);
        }

        private static bool HasValueText(DescriptionLineKind kind)
        {
            return kind == DescriptionLineKind.Attribute || kind == DescriptionLineKind.Modifier || kind == DescriptionLineKind.Cost;
        }
    }
}
