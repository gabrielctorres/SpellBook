using System;
using System.Collections.Generic;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Actions
{
    /// <summary>
    /// Passo de uma lista de ações, rodado pelo executor do <see cref="Core.AbilitySystemComponent"/> no <c>Tick</c>.
    /// A ação é dado compartilhado por todas as execuções: o estado de uma execução fica no
    /// <see cref="ActionFrame"/> e no <see cref="ActionContext"/>, nunca em campos da ação.
    /// </summary>
    [Serializable]
    public abstract class GameplayAction : IValidatable
    {
        /// <summary>Se a ação usa alvos. O Inspector mostra o alvo só quando é verdadeiro.</summary>
        public virtual bool UsesTargets => true;

        /// <summary>
        /// Avança a ação. Na primeira chamada, <c>frame</c> vem zerado. Devolva <see cref="ActionStatus.Done"/> para
        /// terminar, ou uma espera para ser chamada de novo depois dela.
        /// </summary>
        protected internal abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame);

        /// <summary>Acusa a configuração inválida da ação. O padrão não acusa nada.</summary>
        public virtual void Validate(ValidationResult result) { }

        /// <summary>Acrescenta a <paramref name="results"/> os efeitos que a ação aplica. O padrão não acrescenta nada.</summary>
        public virtual void CollectEffects(List<GameplayEffect> results) { }
    }
}
