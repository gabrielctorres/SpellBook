using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Core.Tests.Text
{
    /// <summary><c>TextBuffer</c> e <c>TextSpan</c> (Spec do B-008, Design 1; ADR 0038, item 1; CA3, CA12).</summary>
    public class TextBufferTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

        [Test]
        public void Append_Float_F1_PtBrComma_EnUsDot()
        {
            var buffer = new TextBuffer();

            TextSpan pt = buffer.Append(12.5f, "F1", PtBr);
            TextSpan en = buffer.Append(12.5f, "F1", EnUs);

            Assert.That(buffer.ToString(pt), Is.EqualTo("12,5"));
            Assert.That(buffer.ToString(en), Is.EqualTo("12.5"));
            Assert.That(buffer.ToString(), Is.EqualTo("12,512.5"));
        }

        [Test]
        public void Append_NullCulture_UsesInvariant()
        {
            var buffer = new TextBuffer();

            TextSpan span = buffer.Append(0.25f, "F2", null);

            Assert.That(buffer.ToString(span), Is.EqualTo("0.25"));
        }

        [Test]
        public void Append_10000Floats_ReusedBuffer_DoesNotAllocate()
        {
            var buffer = new TextBuffer();
            int total = 0;
            TestDelegate append10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    buffer.Clear();
                    TextSpan span = buffer.Append(i * 0.5f, "F1", PtBr);
                    buffer.Append(" s");
                    total += span.Length;
                }
            };
            append10000(); // aquecimento: o formatador do runtime inicializa tabelas na primeira chamada

            Assert.That(append10000, Is.Not.AllocatingGCMemory());
            Assert.That(total, Is.GreaterThan(0));
        }

        [Test]
        public void Span_ToString_OnlyThatSpan()
        {
            var buffer = new TextBuffer();
            buffer.Append("Dano: ");
            TextSpan value = buffer.Append(30f, "F0", EnUs);
            buffer.Append(" pontos");

            Assert.That(buffer.ToString(value), Is.EqualTo("30"));
            Assert.That(value.Start, Is.EqualTo(6));
            Assert.That(value.Length, Is.EqualTo(2));
            Assert.That(value.Buffer, Is.SameAs(buffer));
            Assert.That(value.IsEmpty, Is.False);
            Assert.That(buffer.Append(string.Empty).IsEmpty, Is.True);
            Assert.That(buffer.Length, Is.EqualTo(15));
        }

        [Test]
        public void Grow_KeepsContent()
        {
            var buffer = new TextBuffer(4);
            TextSpan first = buffer.Append("abc");
            TextSpan second = buffer.Append(new string('z', 100));
            TextSpan number = buffer.Append(1234.75f, "F2", EnUs);

            Assert.That(buffer.ToString(first), Is.EqualTo("abc"));
            Assert.That(buffer.ToString(second), Is.EqualTo(new string('z', 100)));
            Assert.That(buffer.ToString(number), Is.EqualTo("1234.75"));
            Assert.That(buffer.Length, Is.EqualTo(3 + 100 + 7));
            Assert.That(buffer.Chars.Length, Is.GreaterThanOrEqualTo(buffer.Length));
        }

        [Test]
        public void Append_SpanFromOtherBuffer_CopiesChars()
        {
            var source = new TextBuffer();
            source.Append("ab");
            TextSpan word = source.Append("cura");
            var target = new TextBuffer();
            target.Append("+");

            TextSpan copy = target.Append(word);

            Assert.That(target.ToString(copy), Is.EqualTo("cura"));
            Assert.That(target.ToString(), Is.EqualTo("+cura"));
        }

        [Test]
        public void Clear_ResetsLength_KeepsCapacity()
        {
            var buffer = new TextBuffer(4);
            buffer.Append("abcdefgh");
            int capacity = buffer.Chars.Length;

            buffer.Clear();

            Assert.That(buffer.Length, Is.EqualTo(0));
            Assert.That(buffer.Chars.Length, Is.EqualTo(capacity));
            Assert.That(buffer.ToString(), Is.Empty);
        }
    }
}
