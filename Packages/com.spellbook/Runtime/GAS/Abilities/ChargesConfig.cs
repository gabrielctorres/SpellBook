using System;
using SpellBook.Core.Validation;
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

        /// <summary>
        /// Com as cargas ligadas, acusa o máximo constante menor que 1, o tempo de recarga constante menor ou igual a 0 e o
        /// nome do modo Table que não está na <paramref name="table"/> da habilidade. O caminho é o do campo dono.
        /// </summary>
        public void Validate(ValidationResult result, string propertyPath, AbilityValues table)
        {
            if (!enabled) return;

            string maxPath = propertyPath + "." + nameof(maxCharges);
            ScalableFloat max = MaxCharges.Type == ModifierValueSource.SourceType.Value ? MaxCharges.Value : null;
            if (max != null && max.Mode == ScalableFloat.ScalableMode.Constant && Mathf.Floor(max.Evaluate(1)) < 1f)
            {
                result.AddError($"Max Charges é {max.Evaluate(1)}: com cargas ligadas, o máximo é pelo menos 1.", maxPath);
            }
            ValidateTableName(result, max, table, maxPath);

            string rechargePath = propertyPath + "." + nameof(rechargeTime);
            if (RechargeTime.Mode == ScalableFloat.ScalableMode.Constant && RechargeTime.Evaluate(1) <= 0f)
            {
                result.AddError($"Recharge Time é {RechargeTime.Evaluate(1)}: o tempo de recarga é maior que 0.", rechargePath);
            }
            ValidateTableName(result, RechargeTime, table, rechargePath);
        }

        private static void ValidateTableName(ValidationResult result, ScalableFloat value, AbilityValues table, string propertyPath)
        {
            if (value == null || value.Mode != ScalableFloat.ScalableMode.Table || string.IsNullOrEmpty(value.TableName)) return;
            if (table != null && table.IndexOf(value.TableName) >= 0) return;

            result.AddError($"As cargas leem o valor \"{value.TableName}\" da tabela, que não existe nesta habilidade.", propertyPath);
        }
    }
}
