using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using UnityEngine;

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
        [SerializeReference, TypePicker] private TargetSelector target;

        /// <summary>
        /// Seletor dos alvos da ação, resolvido quando ela começa e entregue em <see cref="ActionContext.Targets"/>.
        /// Nulo usa o alvo padrão do evento: os alvos do evento que disparou a lista.
        /// </summary>
        public TargetSelector Target
        {
            get => target;
            set => target = value;
        }

        /// <summary>Se a ação usa alvos. O Inspector mostra o alvo só quando é verdadeiro.</summary>
        public virtual bool UsesTargets => true;

        /// <summary>
        /// Avança a ação. Na primeira chamada, <c>frame</c> vem zerado. Devolva <see cref="ActionStatus.Done"/> para
        /// terminar, ou uma espera ou lista filha para ser chamada de novo depois dela. Quem chama é o executor.
        /// </summary>
        protected abstract ActionStatus Execute(ActionContext context, ref ActionFrame frame);

        /// <summary>Chamado quando o dono da execução é cancelado com esta ação em curso.</summary>
        protected virtual void OnCancel(ActionContext context, ref ActionFrame frame) { }

        // Entradas do executor: só repassam a chamada, para que o override seja protected em qualquer assembly.
        internal ActionStatus InvokeExecute(ActionContext context, ref ActionFrame frame) => Execute(context, ref frame);

        internal void InvokeCancel(ActionContext context, ref ActionFrame frame) => OnCancel(context, ref frame);

        /// <summary>Acusa a configuração inválida da ação. O padrão não acusa nada.</summary>
        public virtual void Validate(ValidationResult result) { }

        /// <summary>Acrescenta a <paramref name="results"/> os efeitos que a ação aplica. O padrão não acrescenta nada.</summary>
        public virtual void CollectEffects(List<GameplayEffect> results) { }

        // Listas filhas, para a validação descer na árvore de ações. O padrão não tem filhas.
        internal virtual void CollectChildLists(List<ChildActionList> results) { }
    }

    /// <summary>Lista filha de uma ação: o nome do campo serializado e se ela roda num evento próprio, com alvos.</summary>
    internal readonly struct ChildActionList
    {
        public readonly string Field;
        public readonly List<GameplayAction> Actions;
        public readonly bool OwnEvent;

        public ChildActionList(string field, List<GameplayAction> actions, bool ownEvent = false)
        {
            Field = field;
            Actions = actions;
            OwnEvent = ownEvent;
        }
    }
}
