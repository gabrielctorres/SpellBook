using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Conditions
{
    /// <summary>
    /// Contexto único das condições do GAS (ADR 0032, item 2): quem avalia, a habilidade, o nível, os alvos e, dentro
    /// de uma lista de ações, a execução em curso. Somente leitura; as condições não podem mudar o estado do
    /// <see cref="Owner"/>.
    /// </summary>
    public readonly struct ConditionContext
    {
        private static readonly AbilitySystemComponent[] NoTargets = Array.Empty<AbilitySystemComponent>();

        public ConditionContext(AbilitySystemComponent owner, GameplayAbility ability, int level, AbilitySpec spec,
            AbilityTarget target, IReadOnlyList<AbilitySystemComponent> targets, ActionContext action = null)
        {
            Owner = owner;
            Ability = ability;
            Level = level;
            Spec = spec;
            Target = target;
            Targets = targets ?? NoTargets;
            Action = action;
        }

        /// <summary>ASC que avalia a condição.</summary>
        public AbilitySystemComponent Owner { get; }

        /// <summary>Habilidade em avaliação; pode ser nula fora de uma habilidade.</summary>
        public GameplayAbility Ability { get; }

        /// <summary>Nível da habilidade usado na avaliação.</summary>
        public int Level { get; }

        /// <summary>Spec da habilidade concedida; pode ser nulo.</summary>
        public AbilitySpec Spec { get; }

        /// <summary>Alvo da ativação; <see cref="AbilityTarget.None"/> sem ponto.</summary>
        public AbilityTarget Target { get; }

        /// <summary>Unidades-alvo; nunca nulo (lista vazia sem alvos). Pode conter ASCs destruídos.</summary>
        public IReadOnlyList<AbilitySystemComponent> Targets { get; }

        /// <summary>Execução de ações em curso; nula fora de uma lista de ações, como nas regras de ativação.</summary>
        public ActionContext Action { get; }
    }
}
