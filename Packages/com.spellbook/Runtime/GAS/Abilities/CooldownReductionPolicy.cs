using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>Como o valor do atributo de redução de recarga vira redução.</summary>
    public enum CooldownReductionMode
    {
        /// <summary>O atributo é a fração de redução (0,2 = 20 %).</summary>
        Percent,

        /// <summary>O atributo é velocidade extra de recarga (1 = o dobro da velocidade, metade da duração).</summary>
        Haste
    }

    /// <summary>
    /// Regra que transforma a redução de recarga em duração: piso em segundos, teto da redução e modo.
    /// A habilidade sem política usa <see cref="ApplyDefault"/>, o comportamento de antes da política.
    /// </summary>
    [CreateAssetMenu(fileName = "New Cooldown Reduction Policy", menuName = "SpellBook/GAS/Cooldown Reduction Policy")]
    [HelpURL(DocUrls.Gas + "#cooldownreductionpolicy")]
    public sealed class CooldownReductionPolicy : ScriptableObject, IValidatable
    {
        private const float DefaultMinDuration = 0.1f;

        [Tooltip("Duração mínima da recarga, em segundos.")]
        [SerializeField] private float minDuration = DefaultMinDuration;

        [Tooltip("Redução máxima, de 0 a 1 (1 = 100 %).")]
        [SerializeField] private float maxReduction = 1f;

        [Tooltip("Percent: o atributo é a fração de redução. Haste: o atributo é velocidade extra de recarga.")]
        [SerializeField] private CooldownReductionMode mode = CooldownReductionMode.Percent;

        public float MinDuration => minDuration;

        public float MaxReduction => maxReduction;

        public CooldownReductionMode Mode => mode;

        /// <summary>Duração de <paramref name="baseDuration"/> com a redução vinda de <paramref name="attributeValue"/>.</summary>
        public float Apply(float baseDuration, float attributeValue)
        {
            float reduction = mode == CooldownReductionMode.Haste
                ? Mathf.Min(1f - 1f / (1f + Mathf.Max(attributeValue, 0f)), maxReduction)
                : Mathf.Clamp(attributeValue, 0f, maxReduction);

            return Mathf.Max(minDuration, baseDuration * (1f - reduction));
        }

        /// <summary>Regra sem política: piso de 0,1 s e redução limitada a [0, 1], em modo <see cref="CooldownReductionMode.Percent"/>.</summary>
        public static float ApplyDefault(float baseDuration, float attributeValue)
        {
            return Mathf.Max(DefaultMinDuration, baseDuration * (1f - Mathf.Clamp01(attributeValue)));
        }

        public void Validate(ValidationResult result)
        {
            if (minDuration < 0f)
            {
                result.AddError("Min Duration negativo: use 0 ou mais segundos.", nameof(minDuration));
            }

            if (maxReduction < 0f || maxReduction > 1f)
            {
                result.AddError("Max Reduction fora de 0 a 1: use uma fração (1 = 100 %).", nameof(maxReduction));
            }
        }
    }
}
