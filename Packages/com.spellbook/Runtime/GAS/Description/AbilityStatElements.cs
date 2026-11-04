using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Description
{
    /// <summary>Uma linha por modificador do <see cref="GameplayAbility.CostEffect"/>, com o valor absoluto no nível.</summary>
    [Serializable]
    [TypeCategory("Descrição/Custo", Summary = "Custo da habilidade: uma linha por atributo gasto, no nível.")]
    public sealed class CostElement : DescriptionElement
    {
        /// <summary>Sem habilidade ou sem efeito de custo, nada. Pula modificador nulo, sem atributo ou de atributo oculto.</summary>
        public override void Describe(DescriptionContext context)
        {
            List<AttributeEffectModifier> modifiers = ModifiersOf(context);
            if (modifiers == null) return;

            EffectContext evaluation = context.EvaluationContext;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (TryLine(modifiers[i], in evaluation, this, out DescriptionLine line)) context.Lines.Add(line);
            }
        }

        /// <summary>Modificadores do custo da habilidade do contexto; nulo sem habilidade ou sem custo.</summary>
        internal static List<AttributeEffectModifier> ModifiersOf(DescriptionContext context)
        {
            GameplayEffect cost = context.Ability != null ? context.Ability.CostEffect : null;
            return cost != null ? cost.Modifiers : null;
        }

        /// <summary>Linha de custo de um modificador; <c>false</c> se ele é nulo, sem atributo ou de atributo oculto.</summary>
        internal static bool TryLine(
            AttributeEffectModifier modifier, in EffectContext evaluation, DescriptionElement source, out DescriptionLine line)
        {
            AttributeDefinition attribute = modifier?.Attribute;
            if (attribute == null || attribute.Display.Hidden)
            {
                line = default;
                return false;
            }

            float value = EffectDescriber.Evaluate(modifier.ValueSource, in evaluation, out bool variable);
            line = new DescriptionLine(
                DescriptionLineKind.Cost,
                attribute.DisplayName,
                Mathf.Abs(value),
                TextRole.Value,
                variable ? DescriptionLineFlags.Variable : DescriptionLineFlags.None,
                attribute,
                source: source);
            return true;
        }
    }

    /// <summary>
    /// Recarga da habilidade no nível: com o ASC de origem, após a redução de recarga; sem ele, a base (CA6).
    /// </summary>
    [Serializable]
    [TypeCategory("Descrição/Recarga", Summary = "Recarga da habilidade no nível, com a redução de quem lança.")]
    public sealed class CooldownElement : DescriptionElement
    {
        internal static readonly LocalizedText Label = new LocalizedText("gas.description.cooldown", "Recarga");

        /// <summary>Sem habilidade ou sem efeito de recarga, nada.</summary>
        public override void Describe(DescriptionContext context)
        {
            if (TryLine(context, this, out DescriptionLine line)) context.Lines.Add(line);
        }

        /// <summary>Linha de recarga; <c>false</c> sem habilidade ou sem efeito de recarga.</summary>
        internal static bool TryLine(DescriptionContext context, DescriptionElement source, out DescriptionLine line)
        {
            GameplayAbility ability = context.Ability;
            ScalableFloat duration = ability != null && ability.CooldownEffect != null ? ability.CooldownEffect.Duration : null;
            if (duration == null)
            {
                line = default;
                return false;
            }

            float seconds = ability.ApplyCooldownReduction(duration.Evaluate(context.Level, ability.Values), context.Source);
            line = new DescriptionLine(
                DescriptionLineKind.Cooldown, Label, seconds, TextRole.Value, RechargeFlags(ability, duration), source: source);
            return true;
        }

        /// <summary>Variável se a recarga escala ou se a redução de recarga de quem lança pode mudá-la, com ou sem ASC.</summary>
        internal static DescriptionLineFlags RechargeFlags(GameplayAbility ability, ScalableFloat value)
        {
            return ability.CooldownReductionAttribute != null ? DescriptionLineFlags.Variable : FlagsOf(value);
        }

        internal static DescriptionLineFlags FlagsOf(ScalableFloat value)
        {
            return value.Mode != ScalableFloat.ScalableMode.Constant ? DescriptionLineFlags.Variable : DescriptionLineFlags.None;
        }
    }

    /// <summary>Duração de um efeito no nível. Infinito: "permanente"; instantâneo: nada.</summary>
    [Serializable]
    [TypeCategory("Descrição/Duração", Summary = "Duração de um efeito no nível, ou permanente.", Line = "{effect}")]
    public sealed class DurationElement : DescriptionElement
    {
        internal static readonly LocalizedText Label = new LocalizedText("gas.description.duration", "Duração");
        internal static readonly LocalizedText PermanentLabel = new LocalizedText("gas.description.permanent", "Permanente");

        [SerializeField] private GameplayEffect effect;

        public GameplayEffect Effect
        {
            get => effect;
            set => effect = value;
        }

        public override void Describe(DescriptionContext context)
        {
            if (effect == null) return;

            switch (effect.DurationPolicy)
            {
                case EffectDurationPolicy.Infinite:
                    context.Lines.Add(new DescriptionLine(
                        DescriptionLineKind.Duration, PermanentLabel, 0f, TextRole.Value, DescriptionLineFlags.Permanent, source: this));
                    break;
                case EffectDurationPolicy.HasDuration when effect.Duration != null:
                    AbilityValues table = context.Ability != null ? context.Ability.Values : null;
                    context.Lines.Add(new DescriptionLine(
                        DescriptionLineKind.Duration,
                        Label,
                        effect.Duration.Evaluate(context.Level, table),
                        TextRole.Value,
                        CooldownElement.FlagsOf(effect.Duration),
                        source: this));
                    break;
            }
        }
    }

    /// <summary>
    /// Cargas no nível: o máximo (para baixo, pelo menos 0) e a recarga de uma carga, com a redução de quem lança.
    /// </summary>
    [Serializable]
    [TypeCategory("Descrição/Cargas", Summary = "Máximo de cargas e a recarga de cada uma, no nível.")]
    public sealed class ChargesElement : DescriptionElement
    {
        internal static readonly LocalizedText Label = new LocalizedText("gas.description.charges", "{0} cargas, uma a cada {1}");

        /// <summary>Sem habilidade ou com as cargas desligadas, nada.</summary>
        public override void Describe(DescriptionContext context)
        {
            if (TryLine(context, this, out DescriptionLine line)) context.Lines.Add(line);
        }

        /// <summary>Linha de cargas; <c>false</c> sem habilidade ou com as cargas desligadas.</summary>
        internal static bool TryLine(DescriptionContext context, DescriptionElement source, out DescriptionLine line)
        {
            GameplayAbility ability = context.Ability;
            if (ability == null || !ability.Charges.Enabled)
            {
                line = default;
                return false;
            }

            ChargesConfig charges = ability.Charges;
            EffectContext evaluation = context.EvaluationContext;
            float max = Mathf.Max(0f, Mathf.Floor(EffectDescriber.Evaluate(charges.MaxCharges, in evaluation, out bool variable)));
            float recharge = ability.ApplyCooldownReduction(charges.RechargeTime.Evaluate(context.Level, ability.Values), context.Source);
            if (CooldownElement.RechargeFlags(ability, charges.RechargeTime) != DescriptionLineFlags.None) variable = true;

            line = new DescriptionLine(
                DescriptionLineKind.Charges,
                Label,
                max,
                TextRole.Value,
                variable ? DescriptionLineFlags.Variable : DescriptionLineFlags.None,
                secondaryValue: recharge,
                source: source);
            return true;
        }
    }
}
