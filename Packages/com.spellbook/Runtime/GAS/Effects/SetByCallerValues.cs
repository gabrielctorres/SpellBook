using System;
using System.Collections.Generic;

namespace SpellBook.GAS.Effects
{
    /// <summary>Leitura dos valores por nome passados por quem aplica o efeito (SetByCaller).</summary>
    public interface IReadOnlySetByCaller
    {
        int Count { get; }
        bool TryGet(string name, out float value);
    }

    /// <summary>
    /// Valores por nome passados por quem aplica o efeito (ADR 0030). É de quem chama e pode ser reutilizado entre aplicações:
    /// <see cref="Clear"/> não libera a memória já reservada.
    /// </summary>
    public sealed class SetByCallerValues : IReadOnlySetByCaller
    {
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

        public int Count => _values.Count;

        /// <summary>Grava <paramref name="value"/> em <paramref name="name"/>, trocando o valor anterior. Nome nulo lança.</summary>
        public void Set(string name, float value)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _values[name] = value;
        }

        public bool TryGet(string name, out float value)
        {
            if (name == null)
            {
                value = 0f;
                return false;
            }
            return _values.TryGetValue(name, out value);
        }

        public void Clear() => _values.Clear();
    }
}
