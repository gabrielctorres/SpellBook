using System;
using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Description
{
    /// <summary>
    /// O que quem descreve recebe: habilidade, efeito, nível, pilhas, ASC de origem, provedor de texto, formatador e a
    /// lista de saída. Reaproveite a instância com <see cref="Reset"/> entre usos; descrever não aloca.
    /// </summary>
    public sealed class DescriptionContext
    {
        public GameplayAbility Ability;

        /// <summary>Efeito em descrição; nulo fora de um efeito.</summary>
        public GameplayEffect Effect;

        public int Level = 1;

        /// <summary>Pilhas; multiplicam o valor quando o efeito tem <see cref="GameplayEffect.StacksMultiplyMagnitude"/>.</summary>
        public int Stacks = 1;

        /// <summary>ASC de origem; opcional. Sem ele, valores lidos de atributo valem 0 e saem como variáveis.</summary>
        public AbilitySystemComponent Source;

        /// <summary>Provedor de texto; nulo usa os textos padrão e a cultura invariante.</summary>
        public ITextProvider Text;

        /// <summary>Formatador do texto do valor; nulo usa o <see cref="DefaultModifierTextFormatter"/>.</summary>
        public ModifierTextFormatter Formatter;

        /// <summary>Linhas de saída. Quem descreve acrescenta e não limpa.</summary>
        public List<DescriptionLine> Lines { get; } = new List<DescriptionLine>();

        /// <summary>Cultura do <see cref="Text"/>; sem provedor, a invariante. O runtime não fixa cultura.</summary>
        public IFormatProvider Culture => Text?.Culture ?? CultureInfo.InvariantCulture;

        /// <summary>
        /// Contexto de avaliação dos valores: instigador = <see cref="Source"/>, a habilidade e o nível; sem alvo e sem
        /// valores do chamador.
        /// </summary>
        public EffectContext EvaluationContext => new EffectContext(Source, null, Ability, Level, false, Vector3.zero, null);

        /// <summary>Limpa as linhas, o efeito e as pilhas para um novo uso; mantém o provedor e o formatador.</summary>
        public void Reset(GameplayAbility ability, int level, AbilitySystemComponent source = null)
        {
            Ability = ability;
            Level = level;
            Source = source;
            Effect = null;
            Stacks = 1;
            Lines.Clear();
        }
    }
}
