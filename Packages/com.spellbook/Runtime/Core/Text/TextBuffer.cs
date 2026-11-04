using System;
using System.Globalization;

namespace SpellBook.Core.Text
{
    /// <summary>
    /// Texto montado num <c>char[]</c> reaproveitado: escreve número com cultura e formato sem alocar e só cria
    /// <see cref="string"/> quando alguém pede (ADR 0038, item 1). Use <see cref="Clear"/> entre usos.
    /// </summary>
    public sealed class TextBuffer
    {
        private const int MinimumCapacity = 16;
        private const int FloatChars = 32;

        private char[] _chars;
        private int _length;

        /// <summary>Cria o buffer com a capacidade inicial dada; ele dobra quando falta espaço.</summary>
        public TextBuffer(int capacity = 64)
        {
            _chars = new char[Math.Max(capacity, 1)];
        }

        /// <summary>O array interno; só os primeiros <see cref="Length"/> caracteres valem. Muda ao crescer.</summary>
        public char[] Chars => _chars;

        /// <summary>Quantos caracteres foram escritos desde o último <see cref="Clear"/>.</summary>
        public int Length => _length;

        /// <summary>Volta o tamanho a zero e mantém a capacidade. Os <see cref="TextSpan"/> anteriores deixam de valer.</summary>
        public void Clear()
        {
            _length = 0;
        }

        /// <summary>Copia o texto para o fim do buffer; nulo ou vazio devolve um trecho vazio.</summary>
        public TextSpan Append(string text)
        {
            int start = _length;
            if (string.IsNullOrEmpty(text)) return new TextSpan(this, start, 0);

            EnsureCapacity(start + text.Length);
            text.CopyTo(0, _chars, start, text.Length);
            _length = start + text.Length;
            return new TextSpan(this, start, text.Length);
        }

        /// <summary>Copia <paramref name="length"/> caracteres de <paramref name="text"/>, a partir de <paramref name="start"/>, para o fim do buffer.</summary>
        public TextSpan Append(string text, int start, int length)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (start < 0 || length < 0 || start > text.Length - length) throw new ArgumentOutOfRangeException(nameof(length));

            int begin = _length;
            if (length == 0) return new TextSpan(this, begin, 0);

            EnsureCapacity(begin + length);
            text.CopyTo(start, _chars, begin, length);
            _length = begin + length;
            return new TextSpan(this, begin, length);
        }

        /// <summary>
        /// Escreve o número com o formato e a cultura dados, sem alocar. Cultura nula usa a invariante (mesma regra do
        /// <c>AttributeDisplay.Format</c>).
        /// </summary>
        public TextSpan Append(float value, string format, IFormatProvider culture)
        {
            int start = _length;
            IFormatProvider provider = culture ?? CultureInfo.InvariantCulture;
            EnsureCapacity(start + FloatChars);

            int written;
            while (!value.TryFormat(new Span<char>(_chars, start, _chars.Length - start), out written, format, provider))
            {
                EnsureCapacity(_chars.Length * 2);
            }

            _length = start + written;
            return new TextSpan(this, start, written);
        }

        /// <summary>Copia um trecho, deste ou de outro buffer, para o fim deste.</summary>
        public TextSpan Append(TextSpan other)
        {
            int start = _length;
            if (other.IsEmpty) return new TextSpan(this, start, 0);

            EnsureCapacity(start + other.Length);
            Array.Copy(other.Buffer._chars, other.Start, _chars, start, other.Length);
            _length = start + other.Length;
            return new TextSpan(this, start, other.Length);
        }

        /// <summary>
        /// O trecho escrito de <paramref name="start"/> até o fim, para quem escreveu por vários <c>Append</c> (ou por um
        /// método que escreve no buffer, como o <c>AttributeDisplay.Format</c>) e quer um trecho só. Não aloca.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> fora de 0 até <see cref="Length"/>.</exception>
        public TextSpan SpanFrom(int start)
        {
            if (start < 0 || start > _length) throw new ArgumentOutOfRangeException(nameof(start));

            return new TextSpan(this, start, _length - start);
        }

        /// <summary>Cria a <see cref="string"/> do trecho. Aloca; chame só quando a string for necessária.</summary>
        public string ToString(TextSpan span)
        {
            if (span.IsEmpty) return string.Empty;
            if (span.Buffer != this) throw new ArgumentException("O trecho é de outro TextBuffer.", nameof(span));
            if (span.Start + span.Length > _length)
                throw new ArgumentOutOfRangeException(nameof(span), "O trecho passa do fim do buffer.");

            return new string(_chars, span.Start, span.Length);
        }

        /// <summary>Cria a <see cref="string"/> de todo o conteúdo. Aloca.</summary>
        public override string ToString()
        {
            return _length == 0 ? string.Empty : new string(_chars, 0, _length);
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _chars.Length) return;

            int capacity = Math.Max(_chars.Length * 2, MinimumCapacity);
            while (capacity < required) capacity *= 2;

            var grown = new char[capacity];
            Array.Copy(_chars, grown, _length);
            _chars = grown;
        }
    }

    /// <summary>Um trecho escrito num <see cref="TextBuffer"/>: início e tamanho, sem copiar nem alocar.</summary>
    public readonly struct TextSpan
    {
        internal TextSpan(TextBuffer buffer, int start, int length)
        {
            Buffer = buffer;
            Start = start;
            Length = length;
        }

        /// <summary>O buffer em que o trecho foi escrito; nulo no valor padrão.</summary>
        public TextBuffer Buffer { get; }

        /// <summary>Posição do primeiro caractere em <see cref="TextBuffer.Chars"/>.</summary>
        public int Start { get; }

        /// <summary>Quantidade de caracteres.</summary>
        public int Length { get; }

        /// <summary>Verdadeiro sem caracteres ou sem buffer.</summary>
        public bool IsEmpty => Length == 0 || Buffer == null;
    }
}
