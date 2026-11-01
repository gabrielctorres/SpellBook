using System;
using System.Collections;
using System.Collections.Generic;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Passo de uma habilidade. A mesma instância é compartilhada por todos que usam a habilidade,
    /// então estado por execução deve ficar no AbilityContext (ex: CustomData), não em campos.
    /// </summary>
    [Serializable]
    public abstract class AbilityAction : IValidatable
    {
        public abstract IEnumerator Execute(AbilityContext context);

        /// <summary>
        /// Chamado quando a habilidade é cancelada enquanto esta ação está em execução.
        /// Use para restaurar estado (ex: reabilitar movimento após um dash interrompido).
        /// </summary>
        public virtual void OnCancel(AbilityContext context) { }

        /// <summary>
        /// Acusa a configuração inválida da ação (campo obrigatório vazio, por exemplo). O padrão não acusa nada.
        /// Chamado pelo <see cref="GameplayAbility"/> que contém a ação.
        /// </summary>
        public virtual void Validate(ValidationResult result) { }

        /// <summary>
        /// Acrescenta a <paramref name="effects"/> os efeitos que a ação aplica, inclusive os das ações filhas. Usado pelo
        /// validador da habilidade para conferir os nomes da tabela de valores. O padrão não acrescenta nada.
        /// </summary>
        public virtual void CollectEffects(List<GameplayEffect> effects) { }

        /// <summary>Repassa <see cref="CollectEffects"/> a cada ação não nula da lista.</summary>
        protected static void CollectEffects(IList<AbilityAction> actions, List<GameplayEffect> effects)
        {
            if (actions == null) return;

            for (int i = 0; i < actions.Count; i++)
            {
                actions[i]?.CollectEffects(effects);
            }
        }

        /// <summary>
        /// Executa uma ação registrando-a como em execução no contexto.
        /// </summary>
        public static IEnumerator Run(AbilityAction action, AbilityContext context)
        {
            if (action == null || context.IsCancelled) yield break;

            context.RunningActions.Add(action);
            yield return action.Execute(context);
            context.RunningActions.Remove(action);
        }

        /// <summary>
        /// Executa as ações em sequência, parando se o contexto for cancelado.
        /// </summary>
        public static IEnumerator RunSequence(IList<AbilityAction> actions, AbilityContext context)
        {
            if (actions == null) yield break;

            for (int i = 0; i < actions.Count; i++)
            {
                if (context.IsCancelled) yield break;
                if (actions[i] == null) continue;
                yield return Run(actions[i], context);
            }
        }
    }
}
