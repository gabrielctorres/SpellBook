using System;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// Texto simples de uma <see cref="DescriptionLine"/>, escrito num <see cref="TextBuffer"/> sem marcação (CA8). Usado
    /// pela prévia do Inspector e pelas fontes de tooltip. Não aloca.
    /// </summary>
    public static class DescriptionWriter
    {
        // Segundos de recarga, duração e cargas: sem casas quando o valor, numa casa, é inteiro; senão, uma casa.
        private static readonly AttributeDisplay WholeSeconds = new AttributeDisplay { Kind = ValueKind.Seconds, Decimals = 0 };
        private static readonly AttributeDisplay TenthSeconds = new AttributeDisplay { Kind = ValueKind.Seconds, Decimals = 1 };
        private static readonly AttributeDisplay Count = new AttributeDisplay { Kind = ValueKind.Flat, Decimals = 0 };

        /// <summary>
        /// Texto do valor no fim de <paramref name="into"/>: nas linhas de atributo, modificador e custo, pelo formatador do
        /// <paramref name="context"/>; na recarga e na duração, em segundos (permanente, nada); nas cargas, o máximo. As
        /// outras linhas não têm texto de valor.
        /// </summary>
        public static void WriteValue(in DescriptionLine line, DescriptionContext context, TextBuffer into)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (into == null) throw new ArgumentNullException(nameof(into));

            switch (line.Kind)
            {
                case DescriptionLineKind.Attribute:
                case DescriptionLineKind.Modifier:
                case DescriptionLineKind.Cost:
                    ModifierTextFormatter formatter = context.Formatter ?? DefaultModifierTextFormatter.Instance;
                    formatter.WriteValue(line, into, context.Culture);
                    break;
                case DescriptionLineKind.Cooldown:
                case DescriptionLineKind.Duration:
                    if ((line.Flags & DescriptionLineFlags.Permanent) == 0) WriteSeconds(line.Value, context, into);
                    break;
                case DescriptionLineKind.Charges:
                    Count.Format(into, line.Value, context.Culture);
                    break;
            }
        }

        /// <summary>
        /// Linha inteira no fim de <paramref name="into"/>: rótulo pelo provedor do contexto, espaço e texto do valor. Quando
        /// o rótulo tem <c>{0}</c> ou <c>{1}</c>, eles viram o texto do valor e o do segundo valor (segundos, nas cargas), e
        /// o valor não se repete no fim.
        /// </summary>
        public static void WriteLine(in DescriptionLine line, DescriptionContext context, TextBuffer into)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (into == null) throw new ArgumentNullException(nameof(into));

            string label = line.Label.Resolve(context.Text);
            if (HasPlaceholder(label))
            {
                WriteExpanded(label, line, context, into);
                return;
            }

            TextSpan written = into.Append(label);
            if (!HasValueText(line)) return;

            if (!written.IsEmpty) into.Append(" ");
            WriteValue(line, context, into);
        }

        private static void WriteExpanded(string label, in DescriptionLine line, DescriptionContext context, TextBuffer into)
        {
            int copied = 0;
            for (int i = 0; i < label.Length - 2; i++)
            {
                int slot = PlaceholderAt(label, i);
                if (slot < 0) continue;

                into.Append(label, copied, i - copied);
                if (slot == 0) WriteValue(line, context, into);
                else if (line.Kind == DescriptionLineKind.Charges) WriteSeconds(line.SecondaryValue, context, into);
                copied = i + 3;
                i += 2;
            }

            into.Append(label, copied, label.Length - copied);
        }

        private static void WriteSeconds(float seconds, DescriptionContext context, TextBuffer into)
        {
            float tenths = (float)Math.Round(seconds, 1);
            AttributeDisplay display = tenths == (float)Math.Floor(tenths) ? WholeSeconds : TenthSeconds;
            display.Format(into, tenths, context.Culture);
        }

        private static bool HasPlaceholder(string label)
        {
            if (label == null) return false;

            for (int i = 0; i < label.Length - 2; i++)
            {
                if (PlaceholderAt(label, i) >= 0) return true;
            }
            return false;
        }

        // 0 para "{0}", 1 para "{1}" começando em index; -1 se não há marcador ali.
        private static int PlaceholderAt(string label, int index)
        {
            if (label[index] != '{' || label[index + 2] != '}') return -1;

            char digit = label[index + 1];
            return digit == '0' ? 0 : digit == '1' ? 1 : -1;
        }

        private static bool HasValueText(in DescriptionLine line)
        {
            switch (line.Kind)
            {
                case DescriptionLineKind.Attribute:
                case DescriptionLineKind.Modifier:
                case DescriptionLineKind.Cost:
                case DescriptionLineKind.Charges:
                    return true;
                case DescriptionLineKind.Cooldown:
                case DescriptionLineKind.Duration:
                    return (line.Flags & DescriptionLineFlags.Permanent) == 0;
                default:
                    return false;
            }
        }
    }
}
