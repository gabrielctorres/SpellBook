using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Effects
{
    /// <summary>
    /// Ligação de um modificador ao vivo às fontes do seu valor (Spec B-007, P15 e P16): o <c>OnValueChanged</c> de cada
    /// atributo lido (do instigador, mesmo em outro ASC, ou do alvo) e o <c>OnAbilityLevelChanged</c> do instigador quando o
    /// valor depende do nível. Cada evento recalcula o modificador, sem polling e sem alocação. Uma trava de reentrada corta
    /// ciclos: a segunda avaliação no mesmo ciclo mantém o último valor e avisa uma vez por ligação.
    /// </summary>
    internal sealed class LiveModifierBinding
    {
        private readonly ActiveEffectInstance _instance;
        private readonly int _trackedIndex;
        private readonly ModifierValueSource _source;
        private readonly AttributeState[] _states;
        private readonly AbilitySystemComponent _levelSource;

        // Handlers em campo: assinar e desfazer usam a mesma instância de delegate, sem alocação por evento
        private readonly Action<float, float> _onSourceChanged;
        private readonly Action<AbilitySpec, int, int> _onLevelChanged;

        private bool _evaluating;
        private bool _warnedCycle;
        private bool _disposed;

        private LiveModifierBinding(
            ActiveEffectInstance instance,
            int trackedIndex,
            ModifierValueSource source,
            AttributeState[] states,
            AbilitySystemComponent levelSource)
        {
            _instance = instance;
            _trackedIndex = trackedIndex;
            _source = source;
            _states = states;
            _levelSource = levelSource;
            _onSourceChanged = OnSourceChanged;
            _onLevelChanged = OnLevelChanged;

            for (int i = 0; i < _states.Length; i++) _states[i].OnValueChanged += _onSourceChanged;
            if (_levelSource != null) _levelSource.OnAbilityLevelChanged += _onLevelChanged;
        }

        /// <summary>
        /// Liga o modificador <paramref name="trackedIndex"/> da <paramref name="instance"/> às fontes de <paramref name="source"/>.
        /// Devolve nulo se o valor não depende de nada que muda (constante, valor do chamador): não há o que assinar.
        /// </summary>
        internal static LiveModifierBinding Create(ActiveEffectInstance instance, int trackedIndex, ModifierValueSource source)
        {
            if (source == null) return null;

            EffectContext context = instance.Context;
            var states = new List<AttributeState>();
            AbilitySystemComponent levelSource = null;
            switch (source.Type)
            {
                case ModifierValueSource.SourceType.Attribute:
                    AddState(states, context.Instigator, source.Attribute);
                    break;
                case ModifierValueSource.SourceType.TargetAttribute:
                    AddState(states, context.Target, source.TargetAttributeDefinition);
                    break;
                case ModifierValueSource.SourceType.Formula:
                    if (source.Formula != null)
                    {
                        foreach (AttributeDefinition dependency in source.Formula.GetSourceDependencies())
                        {
                            AddState(states, context.Instigator, dependency);
                        }
                    }
                    break;
                case ModifierValueSource.SourceType.Value:
                    bool dependsOnLevel = source.Value != null && source.Value.Mode != ScalableFloat.ScalableMode.Constant;
                    if (dependsOnLevel && context.Ability != null) levelSource = context.Instigator;
                    break;
            }

            if (states.Count == 0 && levelSource == null) return null;
            return new LiveModifierBinding(instance, trackedIndex, source, states.ToArray(), levelSource);
        }

        private static void AddState(List<AttributeState> states, AbilitySystemComponent owner, AttributeDefinition definition)
        {
            if (owner == null || definition == null || owner.AttributeSet == null) return;
            if (!owner.AttributeSet.TryGetState(definition, out AttributeState state) || states.Contains(state)) return;
            states.Add(state);
        }

        private void OnSourceChanged(float oldValue, float newValue) => Recalculate();

        private void OnLevelChanged(AbilitySpec spec, int before, int after)
        {
            if (spec == null || spec.Ability != _instance.Context.Ability) return;
            _instance.SetContextLevel(after);
            Recalculate();
        }

        private void Recalculate()
        {
            // Instigador destruído: o modificador fica com o último valor (P8). Alvo destruído: não há o que recalcular
            if (_disposed || _instance.Context.Instigator == null || _instance.Context.Target == null) return;

            if (_evaluating)
            {
                if (!_warnedCycle)
                {
                    _warnedCycle = true;
                    Debug.LogWarning($"[GAS] Magnitude ao vivo em ciclo no efeito '{_instance.Effect.name}': a reavaliação foi cortada e o modificador mantém o último valor.");
                }
                return;
            }

            _evaluating = true;
            try
            {
                _instance.RecalculateLiveModifier(_trackedIndex, _source);
            }
            finally
            {
                _evaluating = false;
            }
        }

        /// <summary>Desfaz as assinaturas. Idempotente.</summary>
        internal void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _states.Length; i++) _states[i].OnValueChanged -= _onSourceChanged;
            // Por referência: um ASC destruído ainda guarda a assinatura no evento C#
            if (!ReferenceEquals(_levelSource, null)) _levelSource.OnAbilityLevelChanged -= _onLevelChanged;
        }
    }
}
