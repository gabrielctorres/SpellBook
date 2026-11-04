using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;

namespace SpellBook.GAS.Description
{
    /// <summary>Linhas dos modificadores de um <see cref="GameplayEffect"/>.</summary>
    public static class EffectDescriber
    {
        /// <summary>
        /// Acrescenta a <c>context.Lines</c>, sem limpar, uma linha <see cref="DescriptionLineKind.Modifier"/> por
        /// modificador de <see cref="GameplayEffect.Modifiers"/> e, com <c>Period &gt; 0</c>, de
        /// <see cref="GameplayEffect.PeriodicModifiers"/>. Pula entrada nula, sem atributo ou de atributo oculto. O valor sai
        /// no nível do contexto e multiplicado pelas pilhas quando o efeito pede. Não aloca.
        /// </summary>
        public static void Describe(GameplayEffect effect, DescriptionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (effect == null) return;

            EffectContext evaluation = context.EvaluationContext;
            int stacks = effect.StacksMultiplyMagnitude && context.Stacks > 1 ? context.Stacks : 1;
            Describe(effect.Modifiers, context, in evaluation, stacks);
            if (effect.Period > 0f) Describe(effect.PeriodicModifiers, context, in evaluation, stacks);
        }

        private static void Describe(List<AttributeEffectModifier> modifiers, DescriptionContext context,
            in EffectContext evaluation, int stacks)
        {
            if (modifiers == null) return;

            for (int i = 0; i < modifiers.Count; i++)
            {
                AttributeEffectModifier modifier = modifiers[i];
                AttributeDefinition attribute = modifier?.Attribute;
                if (attribute == null || attribute.Display.Hidden) continue;

                float value = Evaluate(modifier.ValueSource, in evaluation, out bool variable) * stacks;
                context.Lines.Add(DescriptionLine.ForModifier(
                    DescriptionLineKind.Modifier,
                    attribute,
                    attribute.DisplayName,
                    value,
                    modifier.Operation,
                    RoleOf(attribute, value, modifier.Operation),
                    variable ? DescriptionLineFlags.Variable : DescriptionLineFlags.None));
            }
        }

        // Valor de uma pilha. O contexto de descrição não tem alvo nem valores do chamador: o que lê o alvo vale 0, o
        // valor do chamador vale o padrão, e o que lê o instigador vale 0 sem ele. Todos esses saem como variáveis.
        private static float Evaluate(ModifierValueSource source, in EffectContext evaluation, out bool variable)
        {
            if (source == null)
            {
                variable = false;
                return 0f;
            }

            switch (source.Type)
            {
                case ModifierValueSource.SourceType.Value:
                    variable = source.Value != null && source.Value.Mode != ScalableFloat.ScalableMode.Constant;
                    return source.GetValue(in evaluation);
                case ModifierValueSource.SourceType.SetByCaller:
                    variable = true;
                    return source.CallerHasDefault ? source.CallerDefault : 0f;
                case ModifierValueSource.SourceType.Attribute:
                case ModifierValueSource.SourceType.Formula:
                    variable = true;
                    return evaluation.Instigator != null ? source.GetValue(in evaluation) : 0f;
                default:
                    variable = true;
                    return 0f;
            }
        }

        // Override mostra um valor, não uma variação. As outras operações têm o sinal da variação.
        private static TextRole RoleOf(AttributeDefinition attribute, float value, ModifierOperation operation)
        {
            return operation == ModifierOperation.Override ? TextRole.Value : attribute.Display.GetDeltaRole(value);
        }
    }
}
