using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    public class AttributeState
    {
        public AttributeDefinition Definition { get; private set; }
        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();
        private readonly AbilitySystemComponent _owner;
        public event Action<float, float> OnValueChanged; // OldValue, NewValue

        public AttributeState(AttributeDefinition definition, float baseValue, AbilitySystemComponent owner)
        {
            Definition = definition;
            BaseValue = baseValue;
            _owner = owner;
            Recalculate();
        }

        public void SetBaseValue(float newValue)
        {
            if (Definition.Type == AttributeType.Derived)
            {
                Debug.LogWarning($"Trying to set base value of a Derived attribute: {Definition.DisplayName}. This will be overwritten by formula.");
            }
            BaseValue = newValue;
            Recalculate();
        }

        public void AddModifier(AttributeModifier mod)
        {
            _modifiers.Add(mod);
            Recalculate();
        }

        public void RemoveModifier(AttributeModifier mod)
        {
            _modifiers.Remove(mod);
            Recalculate();
        }

        public void RemoveModifiersFromSource(object source)
        {
            _modifiers.RemoveAll(m => m.Source == source);
            Recalculate();
        }

        public void Recalculate()
        {
            float oldVal = CurrentValue;
            
            float baseToUse = BaseValue;
            if (Definition.Type == AttributeType.Derived && Definition.Formula != null)
            {
                baseToUse = Definition.Formula.Calculate(_owner, _owner);
            }

            float finalValue = baseToUse;
            float multiplierSum = 1f;

            // 1. Additive Modifiers
            foreach (var mod in _modifiers.Where(m => m.Operation == ModifierOperation.Add))
            {
                finalValue += mod.Value;
            }

            // 2. Multiplicative Modifiers (Summing percentages first: 1 + 0.1 + 0.2 = 1.3x)
            foreach (var mod in _modifiers.Where(m => m.Operation == ModifierOperation.Multiply))
            {
                multiplierSum += mod.Value;
            }
            finalValue *= multiplierSum;

            // 3. Override Modifiers (Last applied wins)
            var overrideMod = _modifiers.LastOrDefault(m => m.Operation == ModifierOperation.Override);
            if (overrideMod != null)
            {
                finalValue = overrideMod.Value;
            }

            CurrentValue = Mathf.Clamp(finalValue, Definition.MinValue, Definition.MaxValue);

            if (!Mathf.Approximately(oldVal, CurrentValue))
            {
                OnValueChanged?.Invoke(oldVal, CurrentValue);
            }
        }
    }
}

