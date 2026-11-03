using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>Ações com filhas e arrastar entre listas no Inspector de ações (EV19; DS do Inspector de ações, 4.4).</summary>
    public class NestedActionListTests
    {
        private const string Main = nameof(GameplayAbility.OnActivate) + ".Array.data[0]";
        private const string Child = ".Actions.Array.data[0]";

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
        public void FlowChildren_IndentedUpTo3_Level4Indicator()
        {
            var level3 = new RepeatAction();
            level3.Actions.Add(new WaitAction());
            var level2 = new RepeatAction();
            level2.Actions.Add(level3);
            var level1 = new RepeatAction();
            level1.Actions.Add(level2);
            var serialized = new SerializedObject(_gas.CreateAbility(level1));
            string[] paths = { Main, Main + Child, Main + Child + Child, Main + Child + Child + Child };
            foreach (string path in paths.Take(3)) serialized.FindProperty(path).isExpanded = true;

            ActionRowView[] rows = paths.Select((path, i) => BindRow(serialized, path, i + 1)).ToArray();

            ActionListView[] nested = rows.Take(3).Select(r => r.Q<ActionListView>()).ToArray();
            Assert.That(nested.Select(l => l.Depth), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(nested.Select(l => l.ListPath), Is.EqualTo(paths.Take(3).Select(p => p + ".Actions")));
            Assert.That(nested.All(l => l.ClassListContains(ActionListView.NestedClass)), Is.True);
            Assert.That(nested.Select(l => l.ClassListContains(ActionListView.IndentedClass)), Is.EqualTo(new[] { true, true, false }),
                "o recuo para no nível 3");
            Assert.That(rows.Select(r => r.LevelLabel.style.display.value),
                Is.EqualTo(new[] { DisplayStyle.None, DisplayStyle.None, DisplayStyle.None, DisplayStyle.Flex }));
            Assert.That(rows[3].LevelLabel.text, Is.EqualTo("⋯ nível 4"));
            List<string> fields = rows[0].Query<PropertyField>().ToList().Select(f => f.bindingPath).ToList();
            Assert.That(fields, Does.Contain(Main + ".Count"), "os outros campos continuam na linha");
            Assert.That(fields, Does.Not.Contain(Main + ".Actions"), "a lista filha não aparece duas vezes");
        }

        [Test]
        public void CollapsedFlow_ShowsChildCount()
        {
            var repeat = new RepeatAction();
            repeat.Actions.Add(new WaitAction());
            repeat.Actions.Add(new WaitAction());
            var conditional = new ConditionalAction();
            conditional.Then.Add(new WaitAction());
            var expanded = new RepeatAction();
            expanded.Actions.Add(new WaitAction());
            var serialized = new SerializedObject(_gas.CreateAbility(repeat, conditional, new WaitAction(), expanded));
            SerializedProperty list = serialized.FindProperty(nameof(GameplayAbility.OnActivate));
            // isExpanded é estado do Editor guardado por caminho, e outros testes o deixam ligado: cada linha é arrumada aqui
            for (int i = 0; i < 4; i++) list.GetArrayElementAtIndex(i).isExpanded = i == 3;

            ActionRowView[] rows = Enumerable.Range(0, 4).Select(i => BindRow(serialized, list.GetArrayElementAtIndex(i).propertyPath, 1)).ToArray();

            string[] plain = Enumerable.Range(0, 4).Select(i => SummaryFormatter.Format(list.GetArrayElementAtIndex(i))).ToArray();
            Assert.That(rows.Select(r => r.SummaryText), Is.EqualTo(new[]
            {
                plain[0] + " · 2 ações", plain[1] + " · 1 ação", plain[2], plain[3],
            }));
        }

        [Test]
        public void DragBetweenListsSameTab_Moves_UndoRestores()
        {
            var repeat = new RepeatAction();
            GameplayAbility ability = _gas.CreateAbility(new WaitAction { Duration = ScalableFloat.Constant(2f) }, repeat);
            ability.OnEnd.Add(new WaitAction());
            var serialized = new SerializedObject(ability);
            SerializedProperty main = serialized.FindProperty(nameof(GameplayAbility.OnActivate));
            SerializedProperty inner = main.GetArrayElementAtIndex(1).FindPropertyRelative(nameof(RepeatAction.Actions));
            SerializedProperty otherTab = serialized.FindProperty(nameof(GameplayAbility.OnEnd));

            Assert.That(ActionListView.Move(main, 0, otherTab, 0), Is.False, "entre abas não");
            Assert.That(ActionListView.Move(main, 1, inner, 0), Is.False, "para dentro de si mesma não");
            Assert.That(ActionListView.Move(main, 0, inner, 0), Is.True);

            Assert.That(ability.OnActivate.Count, Is.EqualTo(1));
            List<GameplayAction> children = ((RepeatAction)ability.OnActivate[0]).Actions;
            Assert.That(children.Count, Is.EqualTo(1));
            Assert.That(((WaitAction)children[0]).Duration.Evaluate(1), Is.EqualTo(2f), "a ação vai com os dados");
            Assert.That(ability.OnEnd.Count, Is.EqualTo(1));

            Undo.PerformUndo();

            Assert.That(ability.OnActivate.Count, Is.EqualTo(2));
            Assert.That(((WaitAction)ability.OnActivate[0]).Duration.Evaluate(1), Is.EqualTo(2f));
            Assert.That(((RepeatAction)ability.OnActivate[1]).Actions, Is.Empty);
        }

        private static ActionRowView BindRow(SerializedObject serialized, string path, int depth)
        {
            var row = new ActionRowView(depth);
            row.Bind(serialized.FindProperty(path), new List<ValidationMessage>());
            return row;
        }
    }
}
