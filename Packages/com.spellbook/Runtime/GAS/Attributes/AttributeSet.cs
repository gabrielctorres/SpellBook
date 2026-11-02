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
        private HashSet<AttributeDefinition> _warnedLimits;

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
            Dictionary<AttributeDefinition, float> dynamicInitial = null; // Limite por atributo: o aviso espera a ligação
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
                    if (HasDynamicLimit(attr.Definition))
                    {
                        if (dynamicInitial == null) dynamicInitial = new Dictionary<AttributeDefinition, float>();
                        dynamicInitial[attr.Definition] = attr.InitialValue;
                    }
                    else if (attr.Definition.Type == AttributeType.Primary && state.BaseValue != attr.InitialValue)
                    {
                        WarnClamped(state, attr.InitialValue);
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

            // 3. Limites por atributo (R1 a R6): só com alguma definição que os use
            if (dynamicInitial == null) return;
            var visiting = new HashSet<AttributeState>();
            var bound = new List<AttributeState>();
            for (int i = 0; i < _states.Count; i++)
            {
                BindLimits(_states[i], visiting, bound);
            }

            // Assina depois de ligar tudo, para que a ligação não aplique a política do máximo
            for (int i = 0; i < bound.Count; i++)
            {
                AttributeState dependent = bound[i];
                AttributeState min = dependent.MinSourceState;
                AttributeState max = dependent.MaxSourceState;
                if (min != null) min.OnValueChanged += (oldVal, newVal) => dependent.OnLimitSourceChanged();
                if (max != null && max != min) max.OnValueChanged += (oldVal, newVal) => dependent.OnLimitSourceChanged();
            }

            foreach (var pair in dynamicInitial)
            {
                AttributeState state = _attributeMap[pair.Key];
                if (pair.Key.Type == AttributeType.Primary && state.BaseValue != pair.Value) WarnClamped(state, pair.Value);
            }
        }

        private static bool HasDynamicLimit(AttributeDefinition def)
        {
            return def.MinSource == LimitSource.Attribute || def.MaxSource == LimitSource.Attribute;
        }

        // Busca em profundidade: liga as fontes antes do dependente e corta a aresta que fecha um ciclo
        private void BindLimits(AttributeState state, HashSet<AttributeState> visiting, List<AttributeState> bound)
        {
            if (state.LimitsBound || !visiting.Add(state)) return;
            AttributeDefinition def = state.Definition;
            AttributeState min = ResolveLimitSource(state, def.MinSource, def.MinAttribute, "mínimo", visiting, bound);
            AttributeState max = ResolveLimitSource(state, def.MaxSource, def.MaxAttribute, "máximo", visiting, bound);
            visiting.Remove(state);
            state.BindLimitSources(min, max);
            if (min != null || max != null) bound.Add(state);
        }

        private AttributeState ResolveLimitSource(AttributeState state, LimitSource source, AttributeDefinition sourceDef,
            string limit, HashSet<AttributeState> visiting, List<AttributeState> bound)
        {
            if (source != LimitSource.Attribute) return null;
            AttributeDefinition def = state.Definition;
            if (sourceDef == null || !_attributeMap.TryGetValue(sourceDef, out var sourceState))
            {
                string sourceName = sourceDef != null ? sourceDef.name : "(vazio)";
                WarnLimit(def, $"[GAS] Fonte do {limit} '{sourceName}' de '{def.name}' não está em startingAttributes de '{name}'. Usando a constante.");
                return null;
            }
            if (visiting.Contains(sourceState))
            {
                WarnLimit(def, $"[GAS] Ciclo nas fontes de limite de '{def.name}' em '{name}': o {limit} usa a constante.");
                return null;
            }
            BindLimits(sourceState, visiting, bound);
            return sourceState;
        }

        // Avisa uma única vez por definição, mesmo com o mínimo e o máximo afetados
        private void WarnLimit(AttributeDefinition def, string message)
        {
            if (_warnedLimits == null) _warnedLimits = new HashSet<AttributeDefinition>();
            if (!_warnedLimits.Add(def)) return;
            Debug.LogWarning(message, this);
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

        // Leitura do campo serializado sem criar os estados, para o validador do ASC no Editor.
        internal bool DeclaresAttribute(AttributeDefinition def)
        {
            if (def == null || startingAttributes == null) return false;
            for (int i = 0; i < startingAttributes.Count; i++)
            {
                if (startingAttributes[i].Definition == def) return true;
            }

            return false;
        }

        internal int StartingAttributeCount => startingAttributes != null ? startingAttributes.Count : 0;

        internal AttributeDefinition GetStartingAttribute(int index) => startingAttributes[index].Definition;

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
        private void WarnClamped(AttributeState state, float initialValue)
        {
            AttributeDefinition def = state.Definition;
            if (_warnedClamped == null) _warnedClamped = new HashSet<AttributeDefinition>();
            if (!_warnedClamped.Add(def)) return;
            Debug.LogWarning($"[GAS] Valor inicial {initialValue} de '{def.name}' em '{name}' está fora de [{state.MinValue}, {state.MaxValue}] e foi limitado para {state.BaseValue}.", this);
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

