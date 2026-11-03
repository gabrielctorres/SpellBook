using NUnit.Framework;

namespace SpellBook.Core.Tests
{
    /// <summary>Fonte de aleatoriedade com semente (B-012, EV4a; ADR 0034, item 8).</summary>
    public class SeededRandomSourceTests
    {
        private const int Samples = 1000;

        [Test]
        public void Seeded_SameSeedSameSequence()
        {
            IRandomSource first = new SeededRandomSource(42);
            IRandomSource second = new SeededRandomSource(42);
            IRandomSource other = new SeededRandomSource(43);
            int differences = 0;

            for (int i = 0; i < Samples; i++)
            {
                float value = first.Value();
                int range = first.Range(7);
                Assert.That(second.Value(), Is.EqualTo(value), "Value() na amostra " + i);
                Assert.That(second.Range(7), Is.EqualTo(range), "Range(7) na amostra " + i);
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                Assert.That(range, Is.InRange(0, 6));
                if (other.Value() != value) differences++;
                other.Range(7);
            }

            Assert.That(differences, Is.GreaterThan(Samples / 2), "a semente 43 repetiu a sequência da 42");
        }
    }
}
