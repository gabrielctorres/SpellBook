using System.Collections.Generic;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Actions
{
    /// <summary>Referência a uma execução de ações. Continua válida depois que a execução termina, e então não roda.</summary>
    public readonly struct ActionExecutionHandle
    {
        private readonly ActionRunner.Execution _execution;

        internal ActionExecutionHandle(ActionRunner.Execution execution) => _execution = execution;

        /// <summary>Se a execução ainda está em curso.</summary>
        public bool IsRunning => _execution != null && _execution.Running;
    }

    /// <summary>
    /// Executor das ações de um ASC (ADR 0034): cada execução tem uma pilha de quadros e um cursor lógico de tempo.
    /// </summary>
    internal sealed class ActionRunner
    {
        internal enum EntryState : byte { Ready, Waiting }

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
            public bool Running;
            public bool Advancing;
        }

        private readonly AbilitySystemComponent _asc;
        private readonly List<Execution> _executions = new List<Execution>();

        public ActionRunner(AbilitySystemComponent asc) => _asc = asc;

        public ActionExecutionHandle Run(IReadOnlyList<GameplayAction> actions, ActionContext context, object owner)
        {
            if (context == null) context = new ActionContext { Owner = _asc, Instigator = _asc };
            else if (context.Owner == null) context.Owner = _asc;

            var execution = new Execution
            {
                Running = true,
                Owner = owner,
                Context = context,
                Cursor = _asc.TickedTime
            };
            Push(execution, actions, 0, actions != null ? actions.Count : 0);
            _executions.Add(execution);
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
                // Uma ação pode terminar outras execuções durante o avanço
                if (i >= _executions.Count) continue;
                Execution execution = _executions[i];
                if (execution.Running && !execution.Advancing) Advance(execution);
            }
        }

        private static void Push(Execution execution, IReadOnlyList<GameplayAction> list, int index, int end)
        {
            execution.Stack[execution.Depth++] = new Entry { List = list, Index = index, End = end };
        }

        private void Advance(Execution execution)
        {
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

                ActionStatus status = action.Execute(execution.Context, ref top.Frame);
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
                }
            }
            execution.Advancing = false;
            if (execution.Depth == 0) Finish(execution);
        }

        private void Finish(Execution execution)
        {
            int index = _executions.IndexOf(execution);
            if (index >= 0) _executions.RemoveAt(index);
            execution.Running = false;
            execution.Context = null;
            execution.Owner = null;
        }
    }
}
