using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>Indicador de "rodando" na linha de ação em Play (EV22; Spec, Design 10a; DS do Inspector de ações, 9).</summary>
    public class RunningIndicatorTests
    {
        private const string Main = nameof(GameplayAbility.OnActivate) + ".Array.data[0]";

        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void Row_RunningClass_WhenActionInRunningSet_ByReference()
        {
            var wait = new WaitAction();
            var serialized = new SerializedObject(_gas.CreateAbility(wait));
            ActionRowView row = BindRow(serialized, Main);

            row.SetRunning(new HashSet<GameplayAction> { new WaitAction() });
            bool litByOtherInstance = row.ClassListContains(ActionRowView.RunningClass);
            row.SetRunning(new HashSet<GameplayAction> { wait });
            bool litByOwnInstance = row.ClassListContains(ActionRowView.RunningClass);
            row.SetRunning(new HashSet<GameplayAction>());

            Assert.That(litByOtherInstance, Is.False, "outra instância do mesmo tipo acendeu a linha");
            Assert.That(litByOwnInstance, Is.True);
            Assert.That(row.ClassListContains(ActionRowView.RunningClass), Is.False, "a linha não apagou");
        }

        [Test]
        public void CollapsedFlowRow_LitWhenChildRuns()
        {
            var wait = new WaitAction();
            var repeat = new RepeatAction();
            repeat.Actions.Add(wait);
            var serialized = new SerializedObject(_gas.CreateAbility(repeat));
            serialized.FindProperty(Main).isExpanded = false;
            ActionRowView row = BindRow(serialized, Main);

            row.SetRunning(new HashSet<GameplayAction> { wait });
            bool collapsedLit = row.ClassListContains(ActionRowView.RunningClass);
            serialized.FindProperty(Main).isExpanded = true;
            row = BindRow(serialized, Main);
            row.SetRunning(new HashSet<GameplayAction> { wait });

            Assert.That(collapsedLit, Is.True, "a linha recolhida não acendeu com a filha rodando");
            Assert.That(row.ClassListContains(ActionRowView.RunningClass), Is.False, "expandida, a filha acende a própria linha");
        }

        [Test]
        public void Tracker_OutsidePlay_NoSchedule_SetEmpty()
        {
            AbilitySystemComponent asc = CreateRunningAsc(out _, out _);
            var root = new VisualElement();

            var tracker = new RunningActionsTracker(root, () => new[] { asc }, () => false);

            Assert.That(tracker.IsScheduled, Is.False);
            Assert.That(tracker.Running, Is.Empty);
        }

        [Test]
        public void Tracker_ExitPlay_ClearsAllRows()
        {
            AbilitySystemComponent asc = CreateRunningAsc(out RepeatAction repeat, out WaitAction wait);
            var serialized = new SerializedObject(_gas.CreateAbility(repeat));
            var root = new VisualElement();
            root.Add(BindRow(serialized, Main));
            root.Add(BindRow(serialized, Main + ".Actions.Array.data[0]"));
            bool playing = true;
            var tracker = new RunningActionsTracker(root, () => new[] { asc }, () => playing);

            tracker.Update();
            List<ActionRowView> rows = root.Query<ActionRowView>().ToList();
            bool scheduled = tracker.IsScheduled;
            bool allLit = rows.TrueForAll(r => r.ClassListContains(ActionRowView.RunningClass));
            playing = false;
            tracker.OnPlayModeChanged(PlayModeStateChange.ExitingPlayMode);

            Assert.That(scheduled, Is.True);
            Assert.That(allLit, Is.True, "a linha do Repeat e a do Wait deviam acender");
            Assert.That(tracker.IsScheduled, Is.False);
            Assert.That(tracker.Running, Is.Empty);
            Assert.That(rows.TrueForAll(r => !r.ClassListContains(ActionRowView.RunningClass)), Is.True, "uma linha ficou acesa");
        }

        private AbilitySystemComponent CreateRunningAsc(out RepeatAction repeat, out WaitAction wait)
        {
            wait = new WaitAction { Duration = ScalableFloat.Constant(1f) };
            repeat = new RepeatAction { Count = 1 };
            repeat.Actions.Add(wait);
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.RunActions(new GameplayAction[] { repeat }, null, null);
            return asc;
        }

        private static ActionRowView BindRow(SerializedObject serialized, string path)
        {
            var row = new ActionRowView();
            row.Bind(serialized.FindProperty(path), new List<ValidationMessage>());
            return row;
        }
    }
}
