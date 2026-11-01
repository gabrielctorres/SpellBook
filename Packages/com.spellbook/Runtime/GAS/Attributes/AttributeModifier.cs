namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Operação de um modificador. Ordem única (ADR 0029):
    /// <c>((base + ΣAdd) × (1 + ΣMultiply)) × Π(1 + MultiplyCompound) + ΣAddFinal</c>, depois o último
    /// <c>Override</c> e, por fim, o limite. Serializado como inteiro: valores novos entram só no fim.
    /// </summary>
    public enum ModifierOperation
    {
        Add,              // somado à base
        Multiply,         // percentuais somados entre si (0,1 = +10 %)
        Override,         // o último vence, depois da fórmula
        MultiplyCompound, // percentuais que compõem entre si (0,1 = ×1,1)
        AddFinal          // somado depois das multiplicações
    }

    public class AttributeModifier
    {
        public float Value;
        public ModifierOperation Operation;
        public object Source; // Reference to what applied this (e.g., GameplayEffect)

        public AttributeModifier(float value, ModifierOperation op, object source = null)
        {
            Value = value;
            Operation = op;
            Source = source;
        }
    }
}

