using System;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.UI;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Monta o <see cref="SlotModel"/> de uma habilidade num ASC (Spec do B-016, Design 3, P1 a P3). O estado vem da
    /// consulta com escopo <see cref="ActivationCheckScope.WithoutConditions"/>, o mesmo caminho da ativação, a cada
    /// evento do ASC que pode mudá-lo. A recarga é lida do ASC a cada tick, pedido só enquanto ela corre. Ligar e soltar
    /// assinam com delegates criados uma vez.
    /// </summary>
    internal sealed class SlotReader
    {
        private readonly Action _changed;
        private readonly Action<GameplayTag, bool> _onTagChanged;
        private readonly Action<AbilitySpec> _onSpecChanged;
        private readonly Action<AbilitySpec, bool> _onSpecEnded;
        private readonly Action<AbilitySpec, CooldownChange> _onCooldownChanged;

        private AbilitySystemComponent _asc;
        private GameplayAbility _ability;
        private int _readyPulses;

        /// <summary>Cria o leitor; <paramref name="changed"/> é chamado a cada modelo novo.</summary>
        public SlotReader(Action changed)
        {
            _changed = changed;
            _onTagChanged = (_, _) => Refresh();
            _onSpecChanged = _ => Refresh();
            _onSpecEnded = (_, _) => Refresh();
            _onCooldownChanged = OnCooldownChanged;
        }

        /// <summary>O último modelo montado.</summary>
        public SlotModel Model { get; private set; }

        /// <summary>Verdadeiro enquanto a recarga corre e o leitor precisa de <see cref="Tick"/>.</summary>
        public bool WantsTick { get; private set; }

        /// <summary>
        /// Liga à <paramref name="ability"/> (nula: slot vazio) no <paramref name="asc"/>, assina os eventos e monta o
        /// primeiro modelo.
        /// </summary>
        public void Attach(AbilitySystemComponent asc, GameplayAbility ability)
        {
            Detach();
            _asc = asc;
            _ability = ability;

            _asc.OnTagChanged += _onTagChanged;
            _asc.OnAbilityGranted += _onSpecChanged;
            _asc.OnAbilityRemoved += _onSpecChanged;
            _asc.OnAbilityActivated += _onSpecChanged;
            _asc.OnAbilityEnded += _onSpecEnded;
            _asc.OnCooldownChanged += _onCooldownChanged;
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
            _asc = null;
            _ability = null;
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
    }
}
