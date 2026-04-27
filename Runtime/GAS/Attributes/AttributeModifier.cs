namespace SpellBook.GAS.Attributes
{
    public enum ModifierOperation
    {
        Add,       // finalValue += Value
        Multiply,  // multiplier += Value (e.g. 0.1 for 10%)
        Override   // finalValue = Value (last one wins)
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

