using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Attributes
{
    [HelpURL(DocUrls.Gas + "#attributeset")]
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
        private HashSet<AttributeDefinition> _warnedMissing;

        private void Awake()
        {
            _owner = GetComponent<AbilitySystemComponent>();

            // 1. Create all states
            if (startingAttributes == null) startingAttributes = new List<InitialAttribute>();
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
            if (def == null)
            {
                state = null;
                return false;
            }
            return _attributeMap.TryGetValue(def, out state);
        }

        public float GetValue(AttributeDefinition def)
        {
            if (def == null) return 0f;
            if (_attributeMap.TryGetValue(def, out var state)) return state.CurrentValue;

            WarnMissing(def);
            return 0f;
        }

        // Avisa uma única vez por atributo para não poluir o Console em leituras por frame
        private void WarnMissing(AttributeDefinition def)
        {
            if (_warnedMissing == null) _warnedMissing = new HashSet<AttributeDefinition>();
            if (!_warnedMissing.Add(def)) return;
            Debug.LogWarning($"[GAS] Atributo '{def.name}' não está em startingAttributes de '{name}'. Retornando 0.", this);
        }

        public void AddModifier(AttributeDefinition def, AttributeModifier mod)
        {
            if (def != null && _attributeMap.TryGetValue(def, out var state))
            {
                state.AddModifier(mod);
            }
        }

        public void RemoveModifier(AttributeDefinition def, AttributeModifier mod)
        {
            if (def != null && _attributeMap.TryGetValue(def, out var state))
            {
                state.RemoveModifier(mod);
            }
        }
    }
}

