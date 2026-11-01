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

        /// <summary>
        /// Mínimo efetivo: o valor atual do atributo-fonte quando ligado, senão a constante da definição.
        /// Antes da ligação pelo <see cref="AttributeSet"/>, um lado vindo de atributo ainda não limita.
        /// </summary>
        public float MinValue => _minSource != null ? _minSource.CurrentValue
            : _limitsBound || Definition.MinSource == LimitSource.Constant ? Definition.MinValue : float.NegativeInfinity;

        /// <summary>Máximo efetivo, com a mesma regra do <see cref="MinValue"/>.</summary>
        public float MaxValue => _maxSource != null ? _maxSource.CurrentValue
            : _limitsBound || Definition.MaxSource == LimitSource.Constant ? Definition.MaxValue : float.PositiveInfinity;

        private readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();
        private readonly AbilitySystemComponent _owner;

        private AttributeState _minSource;
        private AttributeState _maxSource;
        private bool _limitsBound;
        private float _lastMax;
        private bool _updatingLimits;
        private bool _atMin;
        private bool _atMax;

        internal AttributeState MinSourceState => _minSource;
        internal AttributeState MaxSourceState => _maxSource;
        internal bool LimitsBound => _limitsBound;

        /// <summary>Modificadores ativos na ordem em que entraram, sem cópia. O <c>Source</c> diz quem aplicou cada um.</summary>
        public IReadOnlyList<AttributeModifier> Modifiers => _modifiers;

        public event Action<float, float> OnValueChanged; // OldValue, NewValue

        /// <summary>
        /// O valor atual entrou no mínimo vindo de fora dele (dano ou limite que mudou). Ficar no mínimo não repete;
        /// sair e voltar dispara de novo. Não dispara na criação. Vem depois do <see cref="OnValueChanged"/>.
        /// </summary>
        public event Action<AttributeState> OnDepleted;

        /// <summary>O valor atual entrou no máximo, com as mesmas regras do <see cref="OnDepleted"/>. Vem depois dele.</summary>
        public event Action<AttributeState> OnReachedMax;

        public AttributeState(AttributeDefinition definition, float baseValue, AbilitySystemComponent owner)
        {
            Definition = definition;
            BaseValue = ClampToLimits(baseValue);
            _owner = owner;
            Recalculate();
            // Quem nasce no limite não dispara: os indicadores partem do valor inicial
            _atMin = CurrentValue <= MinValue;
            _atMax = CurrentValue >= MaxValue;
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

            // O Clamp devolve o limite exato. As transições ficam em locais e os indicadores são gravados antes
            // dos eventos: um ouvinte que muda o atributo roda um recálculo inteiro, e este só dispara o que decidiu
            float newVal = CurrentValue;
            bool atMin = newVal <= MinValue;
            bool atMax = newVal >= MaxValue;
            bool depleted = atMin && !_atMin;
            bool reachedMax = atMax && !_atMax;
            _atMin = atMin;
            _atMax = atMax;

            if (!Mathf.Approximately(oldVal, newVal))
            {
                OnValueChanged?.Invoke(oldVal, newVal);
            }
            if (depleted) OnDepleted?.Invoke(this);
            if (reachedMax) OnReachedMax?.Invoke(this);
        }

        /// <summary>
        /// Liga as fontes dos limites (nulas: a constante vale) e limita a base aos limites efetivos.
        /// O <see cref="AttributeSet"/> chama uma vez, com as fontes já ligadas, e depois assina o <see cref="OnValueChanged"/> delas.
        /// </summary>
        internal void BindLimitSources(AttributeState minSource, AttributeState maxSource)
        {
            _minSource = minSource;
            _maxSource = maxSource;
            _limitsBound = true;
            _lastMax = MaxValue;
            BaseValue = ClampToLimits(BaseValue);
            Recalculate();
        }

        /// <summary>Um limite ligado a outro atributo mudou: aplica a política do máximo à base e recalcula uma vez.</summary>
        internal void OnLimitSourceChanged()
        {
            // Segunda barreira contra ciclos, além do corte na ligação
            if (_updatingLimits) return;
            _updatingLimits = true;
            try
            {
                float oldMax = _lastMax;
                float newMax = MaxValue;
                _lastMax = newMax;

                // Base acima do novo máximo vai ao máximo pelo limite, qualquer que seja a política
                if (newMax != oldMax && BaseValue <= newMax)
                {
                    float min = MinValue;
                    switch (Definition.OnMaxChange)
                    {
                        case MaxChangePolicy.KeepRatio:
                            if (oldMax - min > 0f) BaseValue = min + (BaseValue - min) * (newMax - min) / (oldMax - min);
                            break;
                        case MaxChangePolicy.AddDelta:
                            BaseValue += newMax - oldMax;
                            break;
                    }
                }

                BaseValue = ClampToLimits(BaseValue);
                Recalculate();
            }
            finally
            {
                _updatingLimits = false;
            }
        }

        // Único ponto que aplica os limites efetivos, na base e no valor atual. Mínimo acima do máximo: vale o mínimo
        private float ClampToLimits(float value)
        {
            float min = MinValue;
            float max = MaxValue;
            if (min > max) return min;
            return Mathf.Clamp(value, min, max);
        }
    }
}

