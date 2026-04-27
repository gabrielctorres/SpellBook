using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    public class AttributeSet : MonoBehaviour
    {
        [System.Serializable]
        public struct InitialAttribute
        {
            public AttributeDefinition Definition;
            public float InitialValue;
        }

        [SerializeField] private List<InitialAttribute> startingAttributes;
        
        private readonly Dictionary<AttributeDefinition, AttributeState> _attributeMap = 
            new Dictionary<AttributeDefinition, AttributeState>();

        private AbilitySystemComponent _owner;

        private void Awake()
        {
            _owner = GetComponent<AbilitySystemComponent>();

            // 1. Create all states
            foreach (var attr in startingAttributes)
            {
                if (attr.Definition != null)
                {
                    _attributeMap[attr.Definition] = new AttributeState(attr.Definition, attr.InitialValue, _owner);
                }
            }

            // 2. Setup dependencies for Derived attributes
            foreach (var state in _attributeMap.Values)
            {
                if (state.Definition.Type == AttributeType.Derived && state.Definition.Formula != null)
                {
                    foreach (var depDef in state.Definition.Formula.GetSourceDependencies())
                    {
                        if (_attributeMap.TryGetValue(depDef, out var depState))
                        {
                            depState.OnValueChanged += (oldVal, newVal) => state.Recalculate();
                        }
                    }
                }
            }
        }

        public bool TryGetState(AttributeDefinition def, out AttributeState state)
        {
            return _attributeMap.TryGetValue(def, out state);
        }

        public float GetValue(AttributeDefinition def)
        {
            return _attributeMap.TryGetValue(def, out var state) ? state.CurrentValue : 0f;
        }

        public void AddModifier(AttributeDefinition def, AttributeModifier mod)
        {
            if (_attributeMap.TryGetValue(def, out var state))
            {
                state.AddModifier(mod);
            }
        }

        public void RemoveModifier(AttributeDefinition def, AttributeModifier mod)
        {
            if (_attributeMap.TryGetValue(def, out var state))
            {
                state.RemoveModifier(mod);
            }
        }
    }
}

