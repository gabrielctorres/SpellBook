using System;
using System.Collections.Generic;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Monta o <see cref="SlotModel"/> de uma habilidade num ASC (Spec do B-016, Design 3, P1 a P3). O estado vem da
    /// consulta com escopo <see cref="ActivationCheckScope.WithoutConditions"/>, o mesmo caminho da ativação, a cada
    /// evento do ASC que pode mudá-lo. A recarga é lida do ASC a cada tick, pedido só enquanto ela corre. Ligar e soltar
    /// assinam com delegates criados uma vez; atualizar não aloca.
    /// </summary>
    internal sealed class SlotReader
    {
        private readonly Action _changed;
        private readonly Action<GameplayTag, bool> _onTagChanged;
        private readonly Action<AbilitySpec> _onSpecChanged;
        private readonly Action<AbilitySpec, bool> _onSpecEnded;
        private readonly Action<AbilitySpec, CooldownChange> _onCooldownChanged;
        private readonly Action<float, float> _onValueChanged;
        private readonly Action _onLabelChanged;
        private readonly List<AttributeState> _costStates = new List<AttributeState>(4);
        private readonly TextBuffer _label = new TextBuffer(16);

        private AbilitySystemComponent _asc;
        private GameplayAbility _ability;
        private ScriptableObject _slotKey;
        private KeyLabelProvider _keyLabels;
        private ITextProvider _text;
        private TextSpan _labelSpan;
        private int _readyPulses;

        /// <summary>Cria o leitor; <paramref name="changed"/> é chamado a cada modelo novo.</summary>
        public SlotReader(Action changed)
        {
            _changed = changed;
            _onTagChanged = (_, _) => Refresh();
            _onSpecChanged = _ => Refresh();
            _onSpecEnded = (_, _) => Refresh();
            _onCooldownChanged = OnCooldownChanged;
            _onValueChanged = (_, _) => Refresh();
            _onLabelChanged = OnLabelChanged;
        }

        /// <summary>O último modelo montado.</summary>
        public SlotModel Model { get; private set; }

        /// <summary>Verdadeiro enquanto a recarga corre e o leitor precisa de <see cref="Tick"/>.</summary>
        public bool WantsTick { get; private set; }

        /// <summary>
        /// Liga à <paramref name="ability"/> (nula: slot vazio) no <paramref name="asc"/>, assina os eventos e monta o
        /// primeiro modelo. O rótulo vem de <paramref name="keyLabels"/> pela chave <paramref name="slotKey"/>.
        /// </summary>
        public void Attach(AbilitySystemComponent asc, GameplayAbility ability, ScriptableObject slotKey,
            KeyLabelProvider keyLabels, ITextProvider text)
        {
            Detach();
            _asc = asc;
            _ability = ability;
            _slotKey = slotKey;
            _keyLabels = keyLabels;
            _text = text;

            _asc.OnTagChanged += _onTagChanged;
            _asc.OnAbilityGranted += _onSpecChanged;
            _asc.OnAbilityRemoved += _onSpecChanged;
            _asc.OnAbilityActivated += _onSpecChanged;
            _asc.OnAbilityEnded += _onSpecEnded;
            _asc.OnCooldownChanged += _onCooldownChanged;
            ListenCostAttributes();
            if (_keyLabels != null) _keyLabels.Changed += _onLabelChanged;

            WriteLabel();
            Refresh();
        }

        /// <summary>Solta os eventos. Sem efeito se não estiver ligado.</summary>
        public void Detach()
        {
            if (_asc == null) return;

            _asc.OnTagChanged -= _onTagChanged;
            _asc.OnAbilityGranted -= _onSpecChanged;
            _asc.OnAbilityRemoved -= _onSpecChanged;
            _asc.OnAbilityActivated -= _onSpecChanged;
            _asc.OnAbilityEnded -= _onSpecEnded;
            _asc.OnCooldownChanged -= _onCooldownChanged;
            for (int i = 0; i < _costStates.Count; i++)
            {
                _costStates[i].OnValueChanged -= _onValueChanged;
            }

            _costStates.Clear();
            if (_keyLabels != null) _keyLabels.Changed -= _onLabelChanged;
            _asc = null;
            _ability = null;
            _keyLabels = null;
            WantsTick = false;
        }

        /// <summary>Lê a recarga de novo; quando ela acaba, o estado também muda.</summary>
        public void Tick()
        {
            if (_asc != null) Refresh();
        }

        private void Refresh()
        {
            AbilityActivationResult result = _asc.GetActivationResult(_ability, AbilityTarget.None,
                ActivationCheckScope.WithoutConditions, out _);
            GameplayEffect cooldown = _ability != null ? _ability.CooldownEffect : null;
            bool cooling = result != AbilityActivationResult.InvalidAbility && result != AbilityActivationResult.NotGranted
                && _asc.IsOnCooldown(cooldown);
            Model = new SlotModel(ToState(result), _ability != null ? _ability.Icon : null, _ability)
                .WithKeyLabel(_labelSpan)
                .WithCooldown(cooling ? _asc.GetCooldownNormalized(cooldown) : 0f,
                    cooling ? _asc.GetCooldownTimeRemaining(cooldown) : 0f)
                .WithReadyPulse(_readyPulses);
            WantsTick = cooling;
            _changed();
        }

        // Ordem da ADR 0022: o primeiro motivo que falha dá o estado; AlreadyActive é marca, não estado (Design 2)
        private static SlotState ToState(AbilityActivationResult result)
        {
            switch (result)
            {
                case AbilityActivationResult.Success:
                case AbilityActivationResult.AlreadyActive:
                    return SlotState.Ready;
                case AbilityActivationResult.InvalidAbility:
                case AbilityActivationResult.NotGranted:
                    return SlotState.Empty;
                case AbilityActivationResult.OnCooldown:
                    return SlotState.OnCooldown;
                case AbilityActivationResult.NoCharges:
                    return SlotState.NoCharges;
                case AbilityActivationResult.CannotAffordCost:
                    return SlotState.NoResource;
                default:
                    return SlotState.Blocked;
            }
        }

        // Pulso de pronto (R8): só no fim da recarga desta habilidade, nunca ao conceder nem no primeiro modelo
        private void OnCooldownChanged(AbilitySpec spec, CooldownChange change)
        {
            if (change == CooldownChange.Ended && spec.Ability == _ability) _readyPulses++;
            Refresh();
        }

        private void OnLabelChanged()
        {
            WriteLabel();
            Refresh();
        }

        private void WriteLabel()
        {
            _label.Clear();
            _labelSpan = _keyLabels != null && _keyLabels.TryWriteLabel(_slotKey, _text, _label) ? _label.SpanFrom(0) : default;
        }

        // Atributos que o custo mexe e de que o valor dele depende, coletados uma vez por ligação (P1)
        private void ListenCostAttributes()
        {
            GameplayEffect cost = _ability != null ? _ability.CostEffect : null;
            if (cost == null || cost.Modifiers == null || _asc.AttributeSet == null) return;

            foreach (AttributeEffectModifier modifier in cost.Modifiers)
            {
                if (modifier == null) continue;

                Listen(modifier.Attribute);
                ModifierValueSource value = modifier.ValueSource;
                switch (value?.Type)
                {
                    case ModifierValueSource.SourceType.Attribute:
                        Listen(value.Attribute);
                        break;
                    case ModifierValueSource.SourceType.TargetAttribute:
                        Listen(value.TargetAttributeDefinition);
                        break;
                    case ModifierValueSource.SourceType.Formula when value.Formula != null:
                        foreach (AttributeDefinition dependency in value.Formula.GetSourceDependencies())
                        {
                            Listen(dependency);
                        }

                        break;
                }
            }
        }

        private void Listen(AttributeDefinition definition)
        {
            if (definition == null || !_asc.AttributeSet.TryGetState(definition, out AttributeState state)
                || _costStates.Contains(state)) return;

            _costStates.Add(state);
            state.OnValueChanged += _onValueChanged;
        }
    }
}
