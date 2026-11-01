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

        private readonly List<AttributeState> _states = new List<AttributeState>();

        private AbilitySystemComponent _owner;
        private bool _initialized;
        private HashSet<AttributeDefinition> _warnedMissing;
        private HashSet<AttributeDefinition> _warnedClamped;

        /// <summary>
        /// Estados dos atributos na ordem de <c>startingAttributes</c>, sem cópia. Uma definição repetida aparece uma vez,
        /// na posição da primeira entrada, com o mesmo estado que <see cref="TryGetState"/> devolve.
        /// </summary>
        public IReadOnlyList<AttributeState> States
        {
            get
            {
                EnsureInitialized();
                return _states;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Cria os estados uma única vez. O <c>Awake</c> chama, e as leituras também, para que um componente que lê o
        /// <see cref="AttributeSet"/> no próprio <c>Awake</c> ou <c>OnEnable</c> não dependa da ordem dos componentes.
        /// </summary>
        internal void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _owner = GetComponent<AbilitySystemComponent>();

            // 1. Create all states
            if (startingAttributes == null) startingAttributes = new List<InitialAttribute>();
            foreach (var attr in startingAttributes)
            {
                if (attr.Definition != null)
                {
                    var state = new AttributeState(attr.Definition, attr.InitialValue, _owner);
                    if (_attributeMap.TryGetValue(attr.Definition, out var previous))
                    {
                        // O mapa fica com a última entrada repetida; a lista a mantém na posição da primeira
                        _states[_states.IndexOf(previous)] = state;
                    }
                    else
                    {
                        _states.Add(state);
                    }
                    _attributeMap[attr.Definition] = state;
                    if (attr.Definition.Type == AttributeType.Primary && state.BaseValue != attr.InitialValue)
                    {
                        WarnClamped(attr.Definition, attr.InitialValue, state.BaseValue);
                    }
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
            EnsureInitialized();
            return _attributeMap.TryGetValue(def, out state);
        }

        /// <summary>Valor atual do atributo, sem aviso quando ele não existe. <paramref name="def"/> nulo ou ausente: falso e 0.</summary>
        public bool TryGetValue(AttributeDefinition def, out float value)
        {
            if (TryGetState(def, out var state))
            {
                value = state.CurrentValue;
                return true;
            }
            value = 0f;
            return false;
        }

        /// <summary>Valor atual do atributo, ou <paramref name="fallback"/> sem aviso quando ele não existe ou <paramref name="def"/> é nulo.</summary>
        public float GetValueOrDefault(AttributeDefinition def, float fallback = 0f)
        {
            return TryGetState(def, out var state) ? state.CurrentValue : fallback;
        }

        /// <summary>Se o atributo está em <c>startingAttributes</c>. Sem aviso; <paramref name="def"/> nulo: falso.</summary>
        public bool HasAttribute(AttributeDefinition def)
        {
            return TryGetState(def, out _);
        }

        /// <summary>Valor base do atributo, sem modificadores. Sem aviso; ausente ou <paramref name="def"/> nulo: 0.</summary>
        public float GetBaseValue(AttributeDefinition def)
        {
            return TryGetState(def, out var state) ? state.BaseValue : 0f;
        }

        /// <summary>Valor atual do atributo. Ausente: avisa uma vez no Console e devolve 0.</summary>
        public float GetValue(AttributeDefinition def)
        {
            if (def == null) return 0f;
            EnsureInitialized();
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

        // Avisa uma única vez por atributo, mesmo que ele apareça repetido em startingAttributes
        private void WarnClamped(AttributeDefinition def, float initialValue, float clampedValue)
        {
            if (_warnedClamped == null) _warnedClamped = new HashSet<AttributeDefinition>();
            if (!_warnedClamped.Add(def)) return;
            Debug.LogWarning($"[GAS] Valor inicial {initialValue} de '{def.name}' em '{name}' está fora de [{def.MinValue}, {def.MaxValue}] e foi limitado para {clampedValue}.", this);
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

