using System;
using System.Collections.Generic;
using System.Reflection;
using SpellBook.Core.Conditions;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Abilities.Actions
{
    /// <summary>
    /// Executa a lista de ações várias vezes, com intervalo opcional entre repetições. Sem intervalo, as repetições
    /// rodam na mesma chamada.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Repete as ações várias vezes, com intervalo opcional.")]
    public class RepeatAction : GameplayAction
    {
        [Min(1)] public int Count = 2;
        [Min(0)] public float Interval = 0f;

        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Actions = new List<GameplayAction>();

        public override bool UsesTargets => false;

        // frame.Index: repetição atual; frame.Phase 1: as filhas da repetição acabaram de rodar
        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase == 1)
            {
                frame.Index++;
                if (frame.Index >= Count) return ActionStatus.Done;
                if (Interval > 0f)
                {
                    frame.Phase = 0;
                    return ActionStatus.Wait(Interval);
                }
            }
            else if (frame.Index >= Count)
            {
                return ActionStatus.Done;
            }

            frame.Phase = 1;
            return ActionStatus.Run(Actions);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Actions, effects);
        }

        internal override void CollectChildLists(List<ChildActionList> results)
        {
            results.Add(new ChildActionList(nameof(Actions), Actions));
        }
    }

    /// <summary>
    /// Executa todas as ações ao mesmo tempo e espera todas terminarem.
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações ao mesmo tempo e espera todas terminarem.")]
    public class ParallelAction : GameplayAction
    {
        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Actions = new List<GameplayAction>();

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0 || Actions == null) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.RunParallel(Actions);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Actions, effects);
        }

        internal override void CollectChildLists(List<ChildActionList> results)
        {
            results.Add(new ChildActionList(nameof(Actions), Actions));
        }
    }

    /// <summary>
    /// Executa "Then" ou "Else" conforme a <see cref="Condition"/>, avaliada no momento da execução com os alvos da ação e
    /// o nível da execução. Sem condição, executa "Else".
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda as ações de Then ou de Else conforme a condição.")]
    public class ConditionalAction : GameplayAction
    {
        [SerializeReference]
        [TypePicker]
        public Condition<ConditionContext> Condition;

        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Then = new List<GameplayAction>();

        [SerializeReference]
        [TypePicker]
        public List<GameplayAction> Else = new List<GameplayAction>();

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0) return ActionStatus.Done;

            frame.Phase = 1;
            ConditionContext conditionContext = context.ToConditionContext();
            bool passes = Condition != null && Condition.Evaluate(in conditionContext);
            return ActionStatus.Run(passes ? Then : Else);
        }

        public override void CollectEffects(List<GameplayEffect> effects)
        {
            ActionLists.CollectEffects(Then, effects);
            ActionLists.CollectEffects(Else, effects);
        }

        internal override void CollectChildLists(List<ChildActionList> results)
        {
            results.Add(new ChildActionList(nameof(Then), Then));
            results.Add(new ChildActionList(nameof(Else), Else));
        }
    }

    internal static class ActionLists
    {
        /// <summary>Profundidade de ações aninhadas acima da qual o validador avisa (DS, 4.4).</summary>
        public const int MaxDepth = 4;

        /// <summary>
        /// Valida a lista <paramref name="field"/> de <paramref name="owner"/> e as filhas, pelo <see cref="ActionEventAttribute"/>
        /// do campo: ação nula (erro), profundidade acima de <see cref="MaxDepth"/> (aviso), seletor que pede os alvos do evento
        /// num evento sem alvos (erro) e o <see cref="GameplayAction.Validate"/> de cada ação, com o caminho dela na frente.
        /// </summary>
        public static void Validate(UnityEngine.Object owner, string field, List<GameplayAction> actions, ValidationResult result)
        {
            var info = owner.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance)?.GetCustomAttribute<ActionEventAttribute>();
            bool hasEventTargets = info == null || info.HasEventTargets;
            Walk(actions, field, hasEventTargets, 1, result);
        }

        private static void Walk(List<GameplayAction> actions, string listPath, bool hasEventTargets, int depth, ValidationResult result)
        {
            if (actions == null) return;

            for (int i = 0; i < actions.Count; i++)
            {
                string path = $"{listPath}.Array.data[{i}]";
                GameplayAction action = actions[i];
                if (action == null)
                {
                    result.AddError("Ação vazia na lista: escolha o tipo ou remova a entrada.", path);
                    continue;
                }

                if (depth == MaxDepth + 1)
                {
                    result.AddWarning($"Ação no nível {depth}: considere extrair para outra habilidade ou efeito.", path);
                }

                if (!hasEventTargets && action.Target != null && action.Target.NeedsEventTargets)
                {
                    result.AddError("Seletor pede os alvos do evento, e este evento não tem alvos: escolha outro seletor.", path + ".target");
                }

                ValidateAction(action, path, result);

                var children = new List<ChildActionList>();
                action.CollectChildLists(children);
                for (int c = 0; c < children.Count; c++)
                {
                    ChildActionList child = children[c];
                    Walk(child.Actions, path + "." + child.Field, hasEventTargets || child.OwnEvent, depth + 1, result);
                }
            }
        }

        // A ação acusa o caminho relativo a ela; aqui ele ganha o caminho da ação no asset
        private static void ValidateAction(GameplayAction action, string path, ValidationResult result)
        {
            var own = new ValidationResult(result.Context);
            action.Validate(own);
            for (int i = 0; i < own.Messages.Count; i++)
            {
                ValidationMessage message = own.Messages[i];
                string full = string.IsNullOrEmpty(message.PropertyPath) ? path : path + "." + message.PropertyPath;
                if (message.Severity == ValidationSeverity.Error) result.AddError(message.Message, full);
                else result.AddWarning(message.Message, full);
            }
        }

        /// <summary>Repassa <see cref="GameplayAction.CollectEffects"/> a cada ação não nula de <paramref name="actions"/>.</summary>
        public static void CollectEffects(IList<GameplayAction> actions, List<GameplayEffect> effects)
        {
            if (actions == null) return;

            for (int i = 0; i < actions.Count; i++)
            {
                actions[i]?.CollectEffects(effects);
            }
        }
    }
}
