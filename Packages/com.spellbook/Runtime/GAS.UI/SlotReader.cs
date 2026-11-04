using System;
using System.Collections.Generic;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Monta o <see cref="SlotModel"/> de uma habilidade num ASC (Spec do B-016, Design 3, P1 a P3). O estado vem da
    /// consulta com escopo <see cref="ActivationCheckScope.WithoutConditions"/>, o mesmo caminho da ativação, a cada
    /// evento do ASC que pode mudá-lo. A recarga e a próxima carga são lidas do ASC a cada tick, pedido só enquanto uma
    /// delas corre ou o motivo da última falha aparece. As marcas de ativa e de autocast vêm do spec (B-012), por
    /// evento, e não mudam o estado (R6). Ligar e soltar assinam com delegates criados uma vez; atualizar não aloca.
    /// </summary>
    internal sealed class SlotReader
    {
        private readonly Action _changed;
        private readonly Action<GameplayTag, bool> _onTagChanged;
        private readonly Action<AbilitySpec> _onSpecChanged;
        private readonly Action<AbilitySpec, bool> _onSpecEnded;
        private readonly Action<AbilitySpec, CooldownChange> _onCooldownChanged;
        private readonly Action<AbilitySpec, int, int> _onChargesChanged;
        private readonly Action<AbilitySpec, int, int> _onLevelChanged;
        private readonly Action<GameplayAbility, ActivationFailure> _onFailed;
        private readonly Action<AbilitySpec, bool> _onAutocastChanged;
        private readonly Action<float, float> _onValueChanged;
        private readonly Action _onLabelChanged;
        private readonly List<AttributeState> _costStates = new List<AttributeState>(4);
        private readonly TextBuffer _label = new TextBuffer(16);
        private readonly TextBuffer _failureText = new TextBuffer(32);
        private readonly DescriptionContext _describe = new DescriptionContext();

        private AbilitySystemComponent _asc;
        private GameplayAbility _ability;
        private ScriptableObject _slotKey;
        private KeyLabelProvider _keyLabels;
        private ITextProvider _text;
        private TextSpan _labelSpan;
        private int _readyPulses;
        private float _failureSeconds;
        private string _failureKey;
        private ScriptableObject _failureTag;
        private TextSpan _failureSpan;
        private int _failureCount;
        private bool _failureVisible;
        private double _failureUntil;

        /// <summary>Cria o leitor; <paramref name="changed"/> é chamado a cada modelo novo.</summary>
        public SlotReader(Action changed)
        {
            _changed = changed;
            _onTagChanged = (_, _) => Refresh();
            _onSpecChanged = _ => Refresh();
            _onSpecEnded = (_, _) => Refresh();
            _onCooldownChanged = OnCooldownChanged;
            _onChargesChanged = OnChargesChanged;
            _onLevelChanged = (_, _, _) => Refresh();
            _onFailed = OnFailed;
            _onAutocastChanged = (_, _) => Refresh();
            _onValueChanged = (_, _) => Refresh();
            _onLabelChanged = OnLabelChanged;
            _describe.Lines.Capacity = 1;
        }

        /// <summary>O último modelo montado.</summary>
        public SlotModel Model { get; private set; }

        /// <summary>Verdadeiro enquanto a recarga, a próxima carga ou o motivo visível pedem <see cref="Tick"/>.</summary>
        public bool WantsTick { get; private set; }

        /// <summary>
        /// Liga à <paramref name="ability"/> (nula: slot vazio) no <paramref name="asc"/>, assina os eventos e monta o
        /// primeiro modelo. O rótulo vem de <paramref name="keyLabels"/> pela chave <paramref name="slotKey"/>; o motivo da
        /// falha aparece por <paramref name="failureSeconds"/>, medidos pelo tempo do ASC.
        /// </summary>
        public void Attach(AbilitySystemComponent asc, GameplayAbility ability, ScriptableObject slotKey,
            KeyLabelProvider keyLabels, ITextProvider text, float failureSeconds)
        {
            Detach();
            _asc = asc;
            _ability = ability;
            _slotKey = slotKey;
            _keyLabels = keyLabels;
            _text = text;
            _failureSeconds = failureSeconds;

            _asc.OnTagChanged += _onTagChanged;
            _asc.OnAbilityGranted += _onSpecChanged;
            _asc.OnAbilityRemoved += _onSpecChanged;
            _asc.OnAbilityActivated += _onSpecChanged;
            _asc.OnAbilityEnded += _onSpecEnded;
            _asc.OnCooldownChanged += _onCooldownChanged;
            _asc.OnChargesChanged += _onChargesChanged;
            _asc.OnAbilityLevelChanged += _onLevelChanged;
            _asc.OnAbilityFailed += _onFailed;
            _asc.OnAutocastChanged += _onAutocastChanged;
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
            _asc.OnChargesChanged -= _onChargesChanged;
            _asc.OnAbilityLevelChanged -= _onLevelChanged;
            _asc.OnAbilityFailed -= _onFailed;
            _asc.OnAutocastChanged -= _onAutocastChanged;
            for (int i = 0; i < _costStates.Count; i++)
            {
                _costStates[i].OnValueChanged -= _onValueChanged;
            }

            _costStates.Clear();
            if (_keyLabels != null) _keyLabels.Changed -= _onLabelChanged;
            _asc = null;
            _ability = null;
            _keyLabels = null;
            _failureKey = null;
            _failureTag = null;
            _failureSpan = default;
            _failureVisible = false;
            WantsTick = false;
        }

        /// <summary>Lê a recarga e a próxima carga de novo e esconde o motivo da falha quando o tempo dele acaba.</summary>
        public void Tick()
        {
            if (_asc == null) return;

            if (_failureVisible && _asc.TimeSource.ElapsedTime >= _failureUntil) _failureVisible = false;
            Refresh();
        }

        private void Refresh()
        {
            AbilityActivationResult result = _asc.GetActivationResult(_ability, AbilityTarget.None,
                ActivationCheckScope.WithoutConditions, out _);
            GameplayEffect cooldown = _ability != null ? _ability.CooldownEffect : null;
            bool cooling = result != AbilityActivationResult.InvalidAbility && result != AbilityActivationResult.NotGranted
                && _asc.IsOnCooldown(cooldown);
            AbilitySpec spec = _asc.FindSpec(_ability);
            bool granted = spec != null;
            bool charged = granted && _ability.Charges.Enabled;
            bool charging = charged && _asc.GetChargeRechargeRemaining(spec) > 0f;
            SlotModel model = new SlotModel(ToState(result), _ability != null ? _ability.Icon : null, _ability)
                .WithKeyLabel(_labelSpan)
                .WithCooldown(cooling ? _asc.GetCooldownNormalized(cooldown) : 0f,
                    cooling ? _asc.GetCooldownTimeRemaining(cooldown) : 0f)
                .WithFailure(_failureKey, _failureSpan, _failureVisible, _failureCount, _failureTag)
                .WithReadyPulse(_readyPulses)
                .WithActivity(granted && spec.IsActive, granted && spec.IsToggledOn, granted && spec.IsChanneling)
                .WithAutocast(granted && AcceptsAutocast(_ability), _asc.IsAutocastEnabled(spec));
            Model = charged
                ? model.WithCharges(_asc.GetCharges(spec), _asc.GetMaxCharges(spec), _asc.GetChargeRechargeNormalized(spec))
                : model;
            WantsTick = cooling || charging || _failureVisible;
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

        // O mesmo que o SetAutocast aceita: Allowed em Instant ou Channel (EV14, R40)
        private static bool AcceptsAutocast(GameplayAbility ability)
        {
            return ability.Autocast.Allowed && ability.Behavior != AbilityBehavior.Passive
                && ability.Behavior != AbilityBehavior.Toggle;
        }

        // Pulso de pronto (R8): só no fim da recarga desta habilidade, nunca ao conceder nem no primeiro modelo
        private void OnCooldownChanged(AbilitySpec spec, CooldownChange change)
        {
            if (change == CooldownChange.Ended && spec.Ability == _ability) _readyPulses++;
            Refresh();
        }

        // Pulso de pronto (R8) também quando volta uma carga desta habilidade; a concessão não dispara o evento
        private void OnChargesChanged(AbilitySpec spec, int before, int after)
        {
            if (spec.Ability == _ability && after > before) _readyPulses++;
            Refresh();
        }

        // Motivo da falha (R7): chave, tag e texto pelo DE11, com o provedor da ligação; a consulta não dispara o evento
        private void OnFailed(GameplayAbility ability, ActivationFailure failure)
        {
            if (ability == null || ability != _ability) return;

            _describe.Text = _text;
            _describe.Lines.Clear();
            ActivationFailureDescriber.Describe(in failure, _describe);
            if (_describe.Lines.Count == 0) return;

            DescriptionLine line = _describe.Lines[0];
            _failureKey = line.Label.Key;
            _failureTag = line.Tag.IsValid ? line.Tag.GetDefinition() : null;
            _failureText.Clear();
            DescriptionWriter.WriteLine(in line, _describe, _failureText);
            _failureSpan = _failureText.SpanFrom(0);
            _failureCount++;
            _failureVisible = _failureSeconds > 0f;
            _failureUntil = _asc.TimeSource.ElapsedTime + _failureSeconds;
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
