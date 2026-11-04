using System;
using System.Collections.Generic;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;

namespace SpellBook.GAS.Actions
{
    /// <summary>Referência a uma execução de ações. Continua válida depois que a execução termina, e então não roda.</summary>
    public readonly struct ActionExecutionHandle
    {
        private readonly ActionRunner.Execution _execution;
        private readonly int _version;

        internal ActionExecutionHandle(ActionRunner.Execution execution)
        {
            _execution = execution;
            _version = execution.Version;
        }

        /// <summary>Se a execução ainda está em curso.</summary>
        public bool IsRunning => _execution != null && _execution.Version == _version && _execution.Running;

        // A execução, enquanto ainda é esta e está em curso
        internal ActionRunner.Execution Live => IsRunning ? _execution : null;
    }

    /// <summary>
    /// Executor das ações de um ASC (ADR 0034): cada execução tem uma pilha de quadros e um cursor lógico de tempo.
    /// Execuções e contextos vêm de pools; em regime, rodar uma lista não aloca.
    /// </summary>
    internal sealed class ActionRunner
    {
        internal enum EntryState : byte { Ready, Waiting, Children, Parallel, Suspended }

        internal struct Entry
        {
            public IReadOnlyList<GameplayAction> List;
            public int Index;
            public int End;
            public ActionFrame Frame;
            public EntryState State;
            public double Deadline;
            // Os alvos da ação corrente já foram resolvidos
            public bool Started;
        }

        internal sealed class Execution
        {
            public Entry[] Stack = new Entry[4];
            // Buffer de alvos de cada quadro da pilha, reutilizado entre execuções
            public List<AbilitySystemComponent>[] Targets = new List<AbilitySystemComponent>[4];
            public int Depth;
            public double Cursor;
            public object Owner;
            public ExecutionLink Link;
            public ActionContext Context;
            public bool OwnsContext;
            public Execution Mother;
            // Alvos padrão herdados da DelayedAction que agendou esta execução
            public readonly List<AbilitySystemComponent> Inherited = new List<AbilitySystemComponent>();
            public bool HasInherited;
            public int Pending;
            public int Version;
            public bool Running;
            public bool Advancing;
        }

        private readonly AbilitySystemComponent _asc;
        private readonly List<Execution> _executions = new List<Execution>();
        private readonly Stack<Execution> _executionPool = new Stack<Execution>();
        private readonly Stack<ActionContext> _contextPool = new Stack<ActionContext>();
        // Execução cuja ação está em Execute agora; lida pela DelayedAction
        private Execution _current;

        public ActionRunner(AbilitySystemComponent asc) => _asc = asc;

        public ActionContext RentContext()
        {
            ActionContext context = _contextPool.Count > 0 ? _contextPool.Pop() : new ActionContext();
            context.Owner = _asc;
            context.Instigator = _asc;
            return context;
        }

        public ActionExecutionHandle Run(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner,
            ExecutionLink link = ExecutionLink.Holds)
        {
            return Run(actions, context, owner, true, link);
        }

        /// <summary>
        /// Como <see cref="Run(IReadOnlyList{GameplayAction}, ActionContext, object, ExecutionLink)"/>. Com
        /// <paramref name="ownsContext"/> falso, o contexto não volta ao pool no fim: quem chamou o devolve por
        /// <see cref="ReturnContext"/>.
        /// </summary>
        internal ActionExecutionHandle Run(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner, bool ownsContext,
            ExecutionLink link = ExecutionLink.Holds)
        {
            if (context == null) context = RentContext();
            else if (context.Owner == null) context.Owner = _asc;

            Execution execution = Start(actions, 0, actions != null ? actions.Count : 0, context, owner, link, null,
                _asc.TickedTime);
            execution.OwnsContext = ownsContext;
            var handle = new ActionExecutionHandle(execution);
            Advance(execution);
            return handle;
        }

        /// <summary>Se há execução em curso de <paramref name="owner"/> que o segura (<see cref="ExecutionLink.Holds"/>).</summary>
        public bool HasRunning(object owner)
        {
            for (int i = 0; i < _executions.Count; i++)
            {
                Execution execution = _executions[i];
                if (execution.Running && execution.Link == ExecutionLink.Holds && Equals(execution.Owner, owner)) return true;
            }
            return false;
        }

        /// <summary>Acrescenta a <paramref name="into"/> a ação de cada quadro da pilha das execuções em curso.</summary>
        public void CollectRunning(List<GameplayAction> into)
        {
            for (int i = 0; i < _executions.Count; i++)
            {
                Execution execution = _executions[i];
                if (!execution.Running) continue;
                for (int d = 0; d < execution.Depth; d++)
                {
                    ref Entry entry = ref execution.Stack[d];
                    if (entry.List == null || entry.Index >= entry.End) continue;
                    GameplayAction action = entry.List[entry.Index];
                    if (action != null) into.Add(action);
                }
            }
        }

        /// <summary>Avança todas as execuções até o <c>TickedTime</c> atual.</summary>
        public void Tick()
        {
            for (int i = _executions.Count - 1; i >= 0; i--)
            {
                // Uma ação pode terminar ou cancelar outras execuções durante o avanço
                if (i >= _executions.Count) continue;
                Execution execution = _executions[i];
                if (execution.Running && !execution.Advancing) Advance(execution);
            }
        }

        /// <summary>Cancela as execuções de <paramref name="owner"/>: filhas antes das mães, quadros do topo para a base.</summary>
        public void Cancel(object owner)
        {
            for (int i = _executions.Count - 1; i >= 0; i--)
            {
                // Um OnCancel pode iniciar ou cancelar outras execuções: recomeça do fim se a lista encolheu
                if (i >= _executions.Count)
                {
                    i = _executions.Count;
                    continue;
                }
                Execution execution = _executions[i];
                if (execution.Running && Equals(execution.Owner, owner)) CancelExecution(execution);
            }
        }

        /// <summary>Cancela só a execução de <paramref name="handle"/>, se ela é deste executor e ainda está em curso.</summary>
        public void Cancel(ActionExecutionHandle handle)
        {
            Execution execution = handle.Live;
            if (execution != null && _executions.IndexOf(execution) >= 0) CancelExecution(execution);
        }

        private Execution Start(IReadOnlyList<GameplayAction> list, int index, int end, ActionContext context,
            object owner, ExecutionLink link, Execution mother, double cursor)
        {
            Execution execution = _executionPool.Count > 0 ? _executionPool.Pop() : new Execution();
            execution.Running = true;
            execution.Owner = owner;
            execution.Link = link;
            execution.Context = context;
            execution.OwnsContext = false;
            execution.Mother = mother;
            execution.Cursor = cursor;
            execution.Pending = 0;
            execution.Depth = 0;
            Push(execution, list, index, end);
            _executions.Add(execution);
            return execution;
        }

        private static void Push(Execution execution, IReadOnlyList<GameplayAction> list, int index, int end)
        {
            if (execution.Depth == execution.Stack.Length)
            {
                Array.Resize(ref execution.Stack, execution.Depth * 2);
                Array.Resize(ref execution.Targets, execution.Stack.Length);
            }
            List<AbilitySystemComponent> targets = execution.Targets[execution.Depth];
            if (targets == null) execution.Targets[execution.Depth] = new List<AbilitySystemComponent>();
            else targets.Clear();
            execution.Stack[execution.Depth++] = new Entry { List = list, Index = index, End = end };
        }

        private void Advance(Execution execution)
        {
            int version = execution.Version;
            double now = _asc.TickedTime;
            execution.Advancing = true;
            while (execution.Depth > 0)
            {
                ref Entry top = ref execution.Stack[execution.Depth - 1];
                // Espera até cancelar: só o cancelamento tira a execução daqui
                if (top.State == EntryState.Suspended) break;
                if (top.State == EntryState.Waiting)
                {
                    if (now < top.Deadline) break;
                    execution.Cursor = top.Deadline;
                }
                else if (top.State == EntryState.Parallel && execution.Pending > 0)
                {
                    break;
                }
                top.State = EntryState.Ready;

                if (top.List == null || top.Index >= top.End)
                {
                    top = default;
                    execution.Targets[execution.Depth - 1].Clear();
                    execution.Depth--;
                    continue;
                }

                GameplayAction action = top.List[top.Index];
                if (action == null)
                {
                    top.Index++;
                    continue;
                }

                List<AbilitySystemComponent> targets = execution.Targets[execution.Depth - 1];
                if (!top.Started)
                {
                    top.Started = true;
                    ResolveTargets(action, execution, targets);
                }
                execution.Context.SetTargets(targets);

                Execution previous = _current;
                _current = execution;
                ActionStatus status;
                try { status = action.InvokeExecute(execution.Context, ref top.Frame); }
                finally { _current = previous; }
                // A própria ação pode ter cancelado esta execução
                if (execution.Version != version || !execution.Running) return;

                switch (status.Kind)
                {
                    case ActionStatus.StatusKind.Done:
                        top.Index++;
                        top.Frame = default;
                        top.Started = false;
                        break;
                    case ActionStatus.StatusKind.Wait:
                        double deadline = status.Seconds > 0f ? execution.Cursor + status.Seconds : execution.Cursor;
                        if (now < deadline)
                        {
                            top.Deadline = deadline;
                            top.State = EntryState.Waiting;
                        }
                        else execution.Cursor = deadline;
                        break;
                    case ActionStatus.StatusKind.WaitUntilCancelled:
                        top.State = EntryState.Suspended;
                        break;
                    case ActionStatus.StatusKind.Run:
                        top.State = EntryState.Children;
                        IReadOnlyList<GameplayAction> children = status.Children;
                        Push(execution, children, 0, children != null ? children.Count : 0);
                        break;
                    case ActionStatus.StatusKind.RunParallel:
                        top.State = EntryState.Parallel;
                        StartParallel(execution, status.Children);
                        if (execution.Version != version || !execution.Running) return;
                        break;
                }
            }
            execution.Advancing = false;
            if (execution.Depth == 0) Finish(execution);
        }

        /// <summary>
        /// Põe em <paramref name="targets"/> os alvos do seletor da ação. Sem seletor, o alvo corrente do
        /// <see cref="ForEachTargetAction"/> mais próximo que a contém ou, fora dele, o alvo padrão da lista: o dono, nas
        /// listas de efeito, ou os alvos do evento.
        /// </summary>
        private static void ResolveTargets(GameplayAction action, Execution execution, List<AbilitySystemComponent> targets)
        {
            targets.Clear();
            ActionContext context = execution.Context;
            TargetSelector selector = action.Target;
            if (selector != null)
            {
                selector.Select(context, targets);
                return;
            }
            if (TryAddForEachTarget(execution, targets)) return;

            Execution root = execution;
            while (root.Mother != null) root = root.Mother;
            if (root.HasInherited) AddLiving(root.Inherited, targets);
            else if (context.DefaultTarget == DefaultTarget.Owner && context.Owner != null) targets.Add(context.Owner);
            else AddLiving(context.EventTargets, targets);
        }

        private static void AddLiving(List<AbilitySystemComponent> source, List<AbilitySystemComponent> targets)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null) targets.Add(source[i]);
            }
        }

        /// <summary>
        /// Agenda <paramref name="actions"/> numa execução nova, com o cursor <paramref name="delay"/> segundos depois do
        /// cursor da execução corrente, numa cópia do contexto dela. As filhas sem seletor recebem os alvos da ação que
        /// agendou. A execução nova é do mesmo dono, com o mesmo vínculo, ou fica sem dono com <paramref name="withoutOwner"/>.
        /// </summary>
        internal void Schedule(IReadOnlyList<GameplayAction> actions, float delay, bool withoutOwner)
        {
            Execution current = _current;
            if (current == null || actions == null || actions.Count == 0) return;

            ActionContext source = current.Context;
            ActionContext context = RentContext();
            CopyContext(source, context);
            Execution execution = Start(actions, 0, actions.Count, context, withoutOwner ? null : current.Owner,
                withoutOwner ? ExecutionLink.Holds : current.Link, null, current.Cursor);
            execution.OwnsContext = true;
            execution.Inherited.AddRange(source.Targets);
            execution.HasInherited = true;
            if (delay > 0f)
            {
                ref Entry top = ref execution.Stack[0];
                top.Deadline = current.Cursor + delay;
                top.State = EntryState.Waiting;
            }
            Advance(execution);
        }

        /// <summary>
        /// Roda <paramref name="actions"/> numa execução nova <see cref="ExecutionLink.FollowsOwner"/> do dono da execução
        /// corrente, numa cópia do <paramref name="context"/>, com os alvos da ação corrente como herdados. Falso, sem rodar,
        /// se <paramref name="context"/> não é o da execução corrente, se ela não tem dono ou se o dono já terminou (a
        /// lista "Ao remover" roda depois do cancelamento das execuções da instância). <paramref name="handle"/> é o da
        /// execução nova, ou o vazio quando ela não começa.
        /// </summary>
        internal bool Follow(ActionContext context, IReadOnlyList<GameplayAction> actions, out ActionExecutionHandle handle)
        {
            handle = default;
            Execution current = _current;
            if (current == null || current.Context != context || current.Owner == null) return false;
            if (current.Owner is ActiveEffectInstance instance && !instance.IsActive) return false;
            if (actions == null || actions.Count == 0) return true;

            ActionContext copy = RentContext();
            CopyContext(context, copy);
            Execution execution = Start(actions, 0, actions.Count, copy, current.Owner, ExecutionLink.FollowsOwner, null,
                current.Cursor);
            execution.OwnsContext = true;
            execution.Inherited.AddRange(context.Targets);
            execution.HasInherited = true;
            handle = new ActionExecutionHandle(execution);
            Advance(execution);
            return true;
        }

        // Procura nos quadros que contêm a ação corrente, desta execução e das mães (filhas em paralelo), o ForEach mais
        // próximo, e acrescenta o alvo da repetição em curso dele.
        private static bool TryAddForEachTarget(Execution execution, List<AbilitySystemComponent> targets)
        {
            // Na execução da ação, o topo é a própria ação; nas mães, o topo é a ação que rodou as filhas em paralelo
            int depth = execution.Depth - 2;
            for (Execution current = execution; current != null; current = current.Mother)
            {
                for (int d = depth; d >= 0; d--)
                {
                    ref Entry entry = ref current.Stack[d];
                    if (entry.List == null || entry.Index >= entry.End) continue;
                    if (!(entry.List[entry.Index] is ForEachTargetAction)) continue;

                    // Alvo destruído no meio da repetição: a filha fica sem alvo, sem cair nos alvos do evento
                    AbilitySystemComponent target = ForEachTargetAction.CurrentTarget(entry.Frame, current.Targets[d]);
                    if (target != null) targets.Add(target);
                    return true;
                }
                if (current.Mother != null) depth = current.Mother.Depth - 1;
            }
            return false;
        }

        private void StartParallel(Execution mother, IReadOnlyList<GameplayAction> children)
        {
            if (children == null) return;
            int count = children.Count;
            for (int i = 0; i < count; i++)
            {
                if (children[i] != null) mother.Pending++;
            }
            for (int i = 0; i < count && mother.Running; i++)
            {
                if (children[i] == null) continue;
                Execution child = Start(children, i, i + 1, mother.Context, mother.Owner, mother.Link, mother, mother.Cursor);
                Advance(child);
            }
        }

        private void Finish(Execution execution)
        {
            Execution mother = execution.Mother;
            double cursor = execution.Cursor;
            Release(execution);
            if (mother == null || !mother.Running) return;

            if (cursor > mother.Cursor) mother.Cursor = cursor;
            mother.Pending--;
            if (mother.Pending == 0 && !mother.Advancing) Advance(mother);
        }

        // Cópia para uma execução agendada: tudo menos os alvos da ação corrente, o cancelamento e o LastRemovedStacks
        private static void CopyContext(ActionContext source, ActionContext copy)
        {
            copy.Owner = source.Owner;
            copy.Instigator = source.Instigator;
            copy.EventTargets.AddRange(source.EventTargets);
            if (source.HasPoint) copy.SetPoint(source.Point);
            copy.Ability = source.Ability;
            copy.Spec = source.Spec;
            copy.Level = source.Level;
            copy.EffectInstance = source.EffectInstance;
            copy.EffectDefinition = source.EffectDefinition;
            copy.EffectEvent = source.EffectEvent;
            if (source.HasEffect) copy.SetEffect(source.Effect);
            copy.EventInstigator = source.EventInstigator;
            copy.Change = source.Change;
            copy.DefaultTarget = source.DefaultTarget;
            copy.LastCreatedThinker = source.LastCreatedThinker;
            // Cópia rasa, como no CreateDerived; o dicionário da fonte vem do pool com o contexto dela
            Dictionary<string, object> data = source.CustomData;
            if (data.Count == 0) return;
            copy.SetCustomDataComparer(data.Comparer);
            foreach (KeyValuePair<string, object> pair in data) copy.CustomData[pair.Key] = pair.Value;
        }

        private void CancelExecution(Execution execution)
        {
            execution.Running = false;
            execution.Context.IsCancelled = true;
            for (int i = _executions.Count - 1; i >= 0; i--)
            {
                if (i >= _executions.Count) continue;
                Execution child = _executions[i];
                if (child.Running && child.Mother == execution) CancelExecution(child);
            }
            for (int d = execution.Depth - 1; d >= 0; d--)
            {
                ref Entry entry = ref execution.Stack[d];
                if (entry.List == null || entry.Index >= entry.End) continue;
                GameplayAction action = entry.List[entry.Index];
                if (action == null) continue;
                execution.Context.SetTargets(execution.Targets[d]);
                action.InvokeCancel(execution.Context, ref entry.Frame);
            }
            Release(execution);
        }

        /// <summary>Zera <paramref name="context"/> e o devolve ao pool.</summary>
        internal void ReturnContext(ActionContext context)
        {
            context.Reset();
            _contextPool.Push(context);
        }

        private void Release(Execution execution)
        {
            int index = _executions.IndexOf(execution);
            if (index >= 0) _executions.RemoveAt(index);
            Array.Clear(execution.Stack, 0, execution.Depth);
            for (int i = 0; i < execution.Depth; i++) execution.Targets[i].Clear();
            execution.Depth = 0;
            execution.Running = false;
            execution.Advancing = false;
            execution.Version++;
            // O contexto pode sobreviver à execução (o da ativação, ou o de uma mãe em paralelo): solta o buffer dela
            if (execution.Context != null) execution.Context.SetTargets(null);
            if (execution.OwnsContext && execution.Context != null) ReturnContext(execution.Context);
            execution.Context = null;
            execution.Owner = null;
            execution.Link = ExecutionLink.Holds;
            execution.Mother = null;
            execution.Inherited.Clear();
            execution.HasInherited = false;
            _executionPool.Push(execution);
        }
    }
}
