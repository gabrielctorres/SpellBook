using System;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>Como as cargas voltam. Só existe a recarga sequencial; o enum guarda o formato serializado.</summary>
    public enum ChargeRechargeMode
    {
        /// <summary>Uma carga por vez: ao voltar uma, a recarga da seguinte recomeça.</summary>
        Sequential
    }

    /// <summary>
    /// Cargas de uma habilidade: com <see cref="Enabled"/>, cada ativação gasta uma carga, e a checagem de recarga vira
    /// "tem carga?". O estado (cargas atuais e recarga) fica no <see cref="AbilitySpec"/>.
    /// </summary>
    [Serializable]
    public sealed class ChargesConfig
    {
        [Tooltip("Liga as cargas. Desligado, a habilidade se comporta como sem cargas.")]
        [SerializeField] private bool enabled;

        [Tooltip("Máximo de cargas: valor por nível, %nome da tabela, atributo ou fórmula. Arredondado para baixo.")]
        [SerializeField] private ModifierValueSource maxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(1f) };

        [Tooltip("Segundos para voltar uma carga, antes da redução de recarga da habilidade.")]
        [SerializeField] private ScalableFloat rechargeTime = ScalableFloat.Constant(1f);

        [Tooltip("Sequential: uma carga por vez.")]
        [SerializeField] private ChargeRechargeMode mode = ChargeRechargeMode.Sequential;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>Fonte do máximo de cargas. Atribuir nulo volta ao padrão (1).</summary>
        public ModifierValueSource MaxCharges
        {
            get => maxCharges ??= new ModifierValueSource { Value = ScalableFloat.Constant(1f) };
            set => maxCharges = value;
        }

        /// <summary>Tempo de recarga de uma carga, por nível. Atribuir nulo volta ao padrão (1 s).</summary>
        public ScalableFloat RechargeTime
        {
            get => rechargeTime ??= ScalableFloat.Constant(1f);
            set => rechargeTime = value;
        }

        public ChargeRechargeMode Mode
        {
            get => mode;
            set => mode = value;
        }
    }
}
