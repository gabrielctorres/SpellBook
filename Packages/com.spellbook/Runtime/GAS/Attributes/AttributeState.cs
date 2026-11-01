using System;
using System.Collections.Generic;
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
            BaseValue = ClampToLimits(baseValue);
            _owner = owner;
            Recalculate();
        }

        public void SetBaseValue(float newValue)
        {
            if (Definition.Type == AttributeType.Derived)
            {
                Debug.LogWarning($"Trying to set base value of a Derived attribute: {Definition.DisplayName}. This will be overwritten by formula.");
            }
            BaseValue = ClampToLimits(newValue);
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
            // De trás para frente com RemoveAt: sem lambda, sem alocação
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_modifiers[i].Source, source))
                {
                    _modifiers.RemoveAt(i);
                }
            }
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

            // Um único for por índice, sem LINQ, para não alocar no recálculo.
            // Cada categoria soma na ordem da lista, como antes (mesmo resultado em ponto flutuante).
            float finalValue = baseToUse;
            float multiplierSum = 1f;
            bool hasOverride = false;
            float overrideValue = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                AttributeModifier mod = _modifiers[i];
                switch (mod.Operation)
                {
                    case ModifierOperation.Add:
                        finalValue += mod.Value;
                        break;
                    case ModifierOperation.Multiply:
                        // Percentuais somados: 1 + 0.1 + 0.2 = 1.3x
                        multiplierSum += mod.Value;
                        break;
                    case ModifierOperation.Override:
                        // O último da lista vence
                        hasOverride = true;
                        overrideValue = mod.Value;
                        break;
                }
            }

            finalValue *= multiplierSum;
            if (hasOverride)
            {
                finalValue = overrideValue;
            }

            CurrentValue = ClampToLimits(finalValue);

            if (!Mathf.Approximately(oldVal, CurrentValue))
            {
                OnValueChanged?.Invoke(oldVal, CurrentValue);
            }
        }

        // Único ponto que aplica os limites da definição, na base e no valor atual
        private float ClampToLimits(float value)
        {
            return Mathf.Clamp(value, Definition.MinValue, Definition.MaxValue);
        }
    }
}

