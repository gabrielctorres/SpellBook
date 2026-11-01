namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Único ponto de cálculo dos modificadores de um atributo (ADR 0029), usado pelo recálculo e pela aplicação
    /// na base de instantâneos e tiques:
    /// <c>((base + ΣAdd) × (1 + ΣMultiply)) × Π(1 + MultiplyCompound) + ΣAddFinal</c>, depois o último <c>Override</c>.
    /// O limite fica com o <see cref="AttributeState"/>. Struct sem alocação.
    /// </summary>
    internal struct ModifierAccumulator
    {
        // A base entra no início e cada grupo soma na ordem da lista: sem MultiplyCompound e AddFinal,
        // o resultado é igual, bit a bit, ao da soma em sequência do B-002 (ADR 0029, item 6)
        private float _add;
        private float _multiplySum;
        private float _compoundProduct;
        private float _addFinal;
        private bool _hasOverride;
        private float _overrideValue;

        public ModifierAccumulator(float baseValue)
        {
            _add = baseValue;
            _multiplySum = 1f;
            _compoundProduct = 1f;
            _addFinal = 0f;
            _hasOverride = false;
            _overrideValue = 0f;
        }

        public void Accumulate(ModifierOperation operation, float value)
        {
            switch (operation)
            {
                case ModifierOperation.Add:
                    _add += value;
                    break;
                case ModifierOperation.Multiply:
                    _multiplySum += value;
                    break;
                case ModifierOperation.Override:
                    // O último da lista vence
                    _hasOverride = true;
                    _overrideValue = value;
                    break;
                case ModifierOperation.MultiplyCompound:
                    _compoundProduct *= 1f + value;
                    break;
                case ModifierOperation.AddFinal:
                    _addFinal += value;
                    break;
            }
        }

        /// <summary>O valor pela fórmula, sem limite.</summary>
        public float Result()
        {
            if (_hasOverride) return _overrideValue;
            return _add * _multiplySum * _compoundProduct + _addFinal;
        }
    }
}
