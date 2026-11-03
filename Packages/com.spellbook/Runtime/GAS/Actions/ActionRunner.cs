using System;
using System.Collections.Generic;
using SpellBook.GAS.Core;

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
    }

    /// <summary>
    /// Executor das ações de um ASC (ADR 0034): cada execução tem uma pilha de quadros e um cursor lógico de tempo.
    /// Execuções e contextos vêm de pools; em regime, rodar uma lista não aloca.
    /// </summary>
    internal sealed class ActionRunner
    {
        internal enum EntryState : byte { Ready, Waiting, Children, Parallel }

        internal struct Entry
        {
            public IReadOnlyList<GameplayAction> List;
            public int Index;
            public int End;
            public ActionFrame Frame;
            public EntryState State;
            public double Deadline;
        }

        internal sealed class Execution
        {
            public Entry[] Stack = new Entry[4];
            public int Depth;
            public double Cursor;
            public object Owner;
            public ActionContext Context;
            public bool OwnsContext;
            public Execution Mother;
            public int Pending;
            public int Version;
            public bool Running;
            public bool Advancing;
        }

        private readonly AbilitySystemComponent _asc;
        private readonly List<Execution> _executions = new List<Execution>();
        private readonly Stack<Execution> _executionPool = new Stack<Execution>();
        private readonly Stack<ActionContext> _contextPool = new Stack<ActionContext>();

        public ActionRunner(AbilitySystemComponent asc) => _asc = asc;

        public ActionContext RentContext()
        {
            ActionContext context = _contextPool.Count > 0 ? _contextPool.Pop() : new ActionContext();
            context.Owner = _asc;
            context.Instigator = _asc;
            return context;
        }

        public ActionExecutionHandle Run(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner)
        {
            return Run(actions, context, owner, true);
        }

        /// <summary>
        /// Como <see cref="Run(IReadOnlyList{GameplayAction}, ActionContext, object)"/>. Com <paramref name="ownsContext"/>
        /// falso, o contexto não volta ao pool no fim: quem chamou o devolve por <see cref="ReturnContext"/>.
        /// </summary>
        internal ActionExecutionHandle Run(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner, bool ownsContext)
        {
            if (context == null) context = RentContext();
            else if (context.Owner == null) context.Owner = _asc;

            Execution execution = Start(actions, 0, actions != null ? actions.Count : 0, context, owner, null, _asc.TickedTime);
            execution.OwnsContext = ownsContext;
            var handle = new ActionExecutionHandle(execution);
            Advance(execution);
            return handle;
        }

        public bool HasRunning(object owner)
        {
            for (int i = 0; i < _executions.Count; i++)
            {
                if (_executions[i].Running && Equals(_executions[i].Owner, owner)) return true;
            }
            return false;
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

        private Execution Start(IReadOnlyList<GameplayAction> list, int index, int end, ActionContext context,
            object owner, Execution mother, double cursor)
        {
            Execution execution = _executionPool.Count > 0 ? _executionPool.Pop() : new Execution();
            execution.Running = true;
            execution.Owner = owner;
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
            if (execution.Depth == execution.Stack.Length) Array.Resize(ref execution.Stack, execution.Depth * 2);
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
                    execution.Depth--;
                    continue;
                }

                GameplayAction action = top.List[top.Index];
                if (action == null)
                {
                    top.Index++;
                    continue;
                }

                ActionStatus status = action.InvokeExecute(execution.Context, ref top.Frame);
                // A própria ação pode ter cancelado esta execução
                if (execution.Version != version || !execution.Running) return;

                switch (status.Kind)
                {
                    case ActionStatus.StatusKind.Done:
                        top.Index++;
                        top.Frame = default;
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
                Execution child = Start(children, i, i + 1, mother.Context, mother.Owner, mother, mother.Cursor);
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
                if (action != null) action.InvokeCancel(execution.Context, ref entry.Frame);
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
            execution.Depth = 0;
            execution.Running = false;
            execution.Advancing = false;
            execution.Version++;
            if (execution.OwnsContext && execution.Context != null) ReturnContext(execution.Context);
            execution.Context = null;
            execution.Owner = null;
            execution.Mother = null;
            _executionPool.Push(execution);
        }
    }
}
