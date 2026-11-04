using System;
using System.Globalization;
using SpellBook.Core;
using SpellBook.Core.Text;

namespace SpellBook.GAS.Attributes
{
    /// <summary>Como o número de um atributo é lido pelo jogador (R15).</summary>
    public enum ValueKind
    {
        Flat,          // número puro
        Percent,       // fração mostrada em porcentagem (0,25 = "25%")
        PercentPoints, // número já em pontos percentuais (25 = "25%")
        Multiplier,    // multiplicador ("x2")
        Seconds        // tempo em segundos ("1,5 s")
    }

    /// <summary>
    /// Exibição de um atributo: tipo de valor, casas, prefixo, sufixo, direção boa, ordem, visibilidade, categoria e papel
    /// de cor (R15 a R18). Asset antigo abre como <see cref="ValueKind.Flat"/>, 0 casas, visível e "mais é melhor".
    /// </summary>
    [Serializable]
    public struct AttributeDisplay
    {
        private const int MaxDecimals = 6;

        // Tabela fixa para não montar o texto de formato a cada chamada.
        private static readonly string[] DecimalFormats = { "F0", "F1", "F2", "F3", "F4", "F5", "F6" };

        // Buffer das sobrecargas que devolvem string: a regra de formato fica só no Compose.
        [ThreadStatic] private static TextBuffer _sharedBuffer;

        public ValueKind Kind;

        public int Decimals;

        public string Prefix;

        public string Suffix;

        /// <summary>Serializado ao contrário de <see cref="HigherIsBetter"/> para que o padrão (falso) seja "mais é melhor".</summary>
        public bool LowerIsBetter;

        public int Order;

        public bool Hidden;

        public AttributeCategory Category;

        public TextRole Role;

        /// <summary>Se um valor maior é bom para o jogador.</summary>
        public bool HigherIsBetter => !LowerIsBetter;

        /// <summary>
        /// Texto do valor pelo <see cref="Kind"/>, com <see cref="Decimals"/> (limitado a 0..6), <see cref="Prefix"/> e
        /// <see cref="Suffix"/>. Cultura nula usa <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public string Format(float value, IFormatProvider culture = null)
        {
            TextBuffer buffer = SharedBuffer();
            Format(buffer, value, culture);
            return buffer.ToString();
        }

        /// <summary>
        /// Texto do valor de um modificador: <c>Add</c> e <c>AddFinal</c> com sinal explícito, pelo tipo;
        /// <c>Multiply</c> e <c>MultiplyCompound</c> em porcentagem com sinal (0,5 = "+50%"), sem prefixo nem sufixo,
        /// porque são variação relativa; <c>Override</c> sem sinal, pelo tipo. Cultura nula usa <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public string Format(float value, ModifierOperation op, IFormatProvider culture = null)
        {
            TextBuffer buffer = SharedBuffer();
            Format(buffer, value, op, culture);
            return buffer.ToString();
        }

        /// <summary>Como <see cref="Format(float, IFormatProvider)"/>, escrito no fim de <paramref name="into"/>. Não aloca.</summary>
        public void Format(TextBuffer into, float value, IFormatProvider culture = null)
        {
            Compose(into, null, value, Kind, culture, true);
        }

        /// <summary>
        /// Como <see cref="Format(float, ModifierOperation, IFormatProvider)"/>, escrito no fim de <paramref name="into"/>.
        /// Não aloca.
        /// </summary>
        public void Format(TextBuffer into, float value, ModifierOperation op, IFormatProvider culture = null)
        {
            switch (op)
            {
                case ModifierOperation.Add:
                case ModifierOperation.AddFinal:
                    Compose(into, SignOf(value), Math.Abs(value), Kind, culture, true);
                    break;
                case ModifierOperation.Multiply:
                case ModifierOperation.MultiplyCompound:
                    Compose(into, SignOf(value), Math.Abs(value), ValueKind.Percent, culture, false);
                    break;
                default:
                    Compose(into, null, value, Kind, culture, true);
                    break;
            }
        }

        /// <summary>Papel de cor de uma variação: zero é neutro; senão benéfico quando concorda com <see cref="HigherIsBetter"/>. Não aloca.</summary>
        public TextRole GetDeltaRole(float delta)
        {
            if (delta == 0f) return TextRole.Neutral;
            return (delta > 0f) == HigherIsBetter ? TextRole.Beneficial : TextRole.Harmful;
        }

        /// <summary>
        /// Se a variação é boa para o jogador (R17). O sinal do delta decide para todas as operações; a operação fica na
        /// assinatura do R17. Não aloca.
        /// </summary>
        public bool IsBeneficial(float delta, ModifierOperation op)
        {
            return GetDeltaRole(delta) == TextRole.Beneficial;
        }

        private static string SignOf(float value) => value < 0f ? "-" : "+";

        private static TextBuffer SharedBuffer()
        {
            TextBuffer buffer = _sharedBuffer ??= new TextBuffer();
            buffer.Clear();
            return buffer;
        }

        private void Compose(TextBuffer into, string sign, float value, ValueKind kind, IFormatProvider culture, bool affixes)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));

            string format = DecimalFormats[Math.Clamp(Decimals, 0, MaxDecimals)];
            if (affixes) into.Append(Prefix);
            into.Append(sign);
            switch (kind)
            {
                case ValueKind.Percent:
                    into.Append(value * 100f, format, culture);
                    into.Append("%");
                    break;
                case ValueKind.PercentPoints:
                    into.Append(value, format, culture);
                    into.Append("%");
                    break;
                case ValueKind.Multiplier:
                    into.Append("x");
                    into.Append(value, format, culture);
                    break;
                case ValueKind.Seconds:
                    into.Append(value, format, culture);
                    into.Append(" s");
                    break;
                default:
                    into.Append(value, format, culture);
                    break;
            }

            if (affixes) into.Append(Suffix);
        }
    }
}
