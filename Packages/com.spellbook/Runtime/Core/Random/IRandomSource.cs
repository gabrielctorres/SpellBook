using System;

namespace SpellBook.Core
{
    /// <summary>
    /// Fonte de números aleatórios. Troque a do ASC por uma com semente fixa para repetir sorteios em testes e replays.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Próximo valor em [0, 1).</summary>
        float Value();

        /// <summary>Próximo inteiro em [0, <paramref name="maxExclusive"/>). Com 0 ou menos, lança <see cref="ArgumentOutOfRangeException"/>.</summary>
        int Range(int maxExclusive);
    }

    /// <summary>
    /// <see cref="IRandomSource"/> com xorshift de 32 bits. A mesma semente dá sempre a mesma sequência. Não aloca.
    /// </summary>
    public sealed class SeededRandomSource : IRandomSource
    {
        // O xorshift fica preso em zero: a semente 0 usa esta constante no lugar
        private const uint ZeroSeedState = 0x9E3779B9u;

        private uint _state;

        public SeededRandomSource(int seed)
        {
            _state = seed == 0 ? ZeroSeedState : unchecked((uint)seed);
        }

        public float Value()
        {
            // Os 24 bits altos cabem exatos na mantissa do float: o resultado nunca chega a 1
            return (Next() >> 8) * (1f / 16777216f);
        }

        public int Range(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Use um máximo maior que 0.");
            return (int)(((ulong)Next() * (uint)maxExclusive) >> 32);
        }

        private uint Next()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
