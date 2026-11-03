using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Menu, teclado, copiar e colar no Inspector de ações (EV20; DS do Inspector de ações, 4.1, 4.3 e 4.5).</summary>
    public class ActionClipboardTests
    {
        private GasFactory _gas;
        private Func<string, string, bool> _confirm;
        private readonly List<string> _asked = new List<string>();
        private bool _answer;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _confirm = ActionListView.Confirm;
            _asked.Clear();
            ActionListView.Confirm = (title, message) =>
            {
                _asked.Add(message);
                return _answer;
            };
        }

        [TearDown]
        public void TearDown()
        {
            ActionListView.Confirm = _confirm;
            _gas.Dispose();
        }

        [Test]
        public void Duplicate_CreatesNewInstanceBelow()
        {
            GameplayAbility ability = _gas.CreateAbility(Wait(2f), Wait(3f));
            SerializedProperty list = List(ability, nameof(GameplayAbility.OnActivate));

            Assert.That(ActionListView.Execute(list, 0, ActionCommand.Duplicate, out int selection), Is.True);

            Assert.That(ability.OnActivate.Select(Seconds), Is.EqualTo(new[] { 2f, 2f, 3f }));
            Assert.That(ability.OnActivate[1], Is.Not.SameAs(ability.OnActivate[0]), "instância nova, não a mesma referência");
            Assert.That(selection, Is.EqualTo(1));

            Undo.PerformUndo();

            Assert.That(ability.OnActivate.Select(Seconds), Is.EqualTo(new[] { 2f, 3f }));
        }

        [Test]
        public void CopyPaste_WithChildren_NewInstances_OtherOwnerValidatorFlags()
        {
            var child = new ApplyEffectAction { Target = new EventTargetsSelector() };
            var repeat = new RepeatAction { Count = 4 };
            repeat.Actions.Add(child);
            GameplayEffect effect = _gas.CreateDurationEffect(1f);
            effect.OnApplied.Add(repeat);
            GameplayAbility ability = _gas.CreateAbility();
            SerializedProperty granted = List(ability, nameof(GameplayAbility.OnGranted));

            string text = ActionClipboard.Copy(List(effect, nameof(GameplayEffect.OnApplied)).GetArrayElementAtIndex(0));

            Assert.That(ActionClipboard.CanPaste(text), Is.True);
            Assert.That(ActionClipboard.Paste(granted, 0, text), Is.True);
            var pasted = (RepeatAction)ability.OnGranted.Single();
            Assert.That(pasted, Is.Not.SameAs(repeat));
            Assert.That(pasted.Count, Is.EqualTo(4));
            var pastedChild = (ApplyEffectAction)pasted.Actions.Single();
            Assert.That(pastedChild, Is.Not.SameAs(child), "as filhas também são instâncias novas");
            Assert.That(pastedChild.Target, Is.InstanceOf<EventTargetsSelector>().And.Not.SameAs(child.Target));
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { ability });
            Assert.That(messages.Any(m => m.Severity == ValidationSeverity.Error
                && m.PropertyPath == "OnGranted.Array.data[0].Actions.Array.data[0].target"), Is.True,
                "entra e o validador acusa o seletor sem alvos do evento");

            Assert.That(ActionClipboard.CanPaste("texto qualquer"), Is.False);
            Assert.That(ActionClipboard.Paste(granted, 0, "texto qualquer"), Is.False);
            Assert.That(ability.OnGranted.Count, Is.EqualTo(1));
        }

        [Test]
        public void ChangeType_NonDefault_WarnsAndUndo()
        {
            var edited = new RepeatAction { Count = 5 };
            edited.Actions.Add(Wait(1f));
            GameplayAbility ability = _gas.CreateAbility(edited, new RepeatAction());
            SerializedProperty list = List(ability, nameof(GameplayAbility.OnActivate));

            Assert.That(ActionRowView.ChangeType(list.GetArrayElementAtIndex(1), typeof(WaitAction)), Is.True);
            Assert.That(ability.OnActivate[1], Is.InstanceOf<WaitAction>());
            Assert.That(_asked, Is.Empty, "ação no padrão troca sem aviso");

            _answer = false;
            Assert.That(ActionRowView.ChangeType(list.GetArrayElementAtIndex(0), typeof(WaitAction)), Is.False);
            Assert.That(_asked, Is.EqualTo(new[] { "Os dados desta ação serão descartados." }));
            Assert.That(ability.OnActivate[0], Is.SameAs(edited), "cancelar não muda nada");

            _answer = true;
            Assert.That(ActionRowView.ChangeType(list.GetArrayElementAtIndex(0), typeof(WaitAction)), Is.True);
            Assert.That(ability.OnActivate[0], Is.InstanceOf<WaitAction>());

            Undo.PerformUndo();

            var restored = (RepeatAction)ability.OnActivate[0];
            Assert.That(restored.Count, Is.EqualTo(5));
            Assert.That(restored.Actions.Select(Seconds), Is.EqualTo(new[] { 1f }));
        }

        [Test]
        public void Keyboard_DeleteCtrlDAltArrowsEnter()
        {
            Assert.That(ActionListView.CommandFor(KeyCode.DownArrow, EventModifiers.Alt), Is.EqualTo(ActionCommand.MoveDown));
            Assert.That(ActionListView.CommandFor(KeyCode.UpArrow, EventModifiers.Alt), Is.EqualTo(ActionCommand.MoveUp));
            Assert.That(ActionListView.CommandFor(KeyCode.Return, EventModifiers.None), Is.EqualTo(ActionCommand.Toggle));
            Assert.That(ActionListView.CommandFor(KeyCode.UpArrow, EventModifiers.None), Is.EqualTo(ActionCommand.None),
                "↑ sem Alt é a navegação do ListView");
            Assert.That(ActionListView.CommandFor("Duplicate"), Is.EqualTo(ActionCommand.Duplicate), "Ctrl+D");
            Assert.That(ActionListView.CommandFor("SoftDelete"), Is.EqualTo(ActionCommand.Remove), "Delete");
            Assert.That(ActionListView.CommandFor("Delete"), Is.EqualTo(ActionCommand.Remove));
            var repeat = new RepeatAction();
            repeat.Actions.Add(Wait(9f));
            GameplayAbility ability = _gas.CreateAbility(Wait(1f), repeat, Wait(3f));
            SerializedProperty list = List(ability, nameof(GameplayAbility.OnActivate));
            list.GetArrayElementAtIndex(0).isExpanded = false;

            ActionListView.Execute(list, 0, ActionCommand.MoveDown, out int down);
            Assert.That(ability.OnActivate[1], Is.InstanceOf<WaitAction>());
            Assert.That(down, Is.EqualTo(1), "a seleção segue a ação");
            ActionListView.Execute(list, 1, ActionCommand.MoveUp, out int up);
            Assert.That(ability.OnActivate.Select(a => a.GetType()), Is.EqualTo(new[] { typeof(WaitAction), typeof(RepeatAction), typeof(WaitAction) }));
            Assert.That(up, Is.EqualTo(0));

            ActionListView.Execute(list, 0, ActionCommand.Toggle, out _);
            Assert.That(list.GetArrayElementAtIndex(0).isExpanded, Is.True);
            ActionListView.Execute(list, 0, ActionCommand.Toggle, out _);
            Assert.That(list.GetArrayElementAtIndex(0).isExpanded, Is.False);

            ActionListView.Execute(list, 2, ActionCommand.Duplicate, out int duplicated);
            Assert.That(ability.OnActivate.Count, Is.EqualTo(4));
            Assert.That(duplicated, Is.EqualTo(3));

            Assert.That(ActionListView.Execute(list, 3, ActionCommand.Remove, out int removed), Is.True);
            Assert.That(_asked, Is.Empty, "folha sai sem perguntar");
            Assert.That(removed, Is.EqualTo(2));
            _answer = false;
            Assert.That(ActionListView.Execute(list, 1, ActionCommand.Remove, out _), Is.False);
            Assert.That(_asked.Count, Is.EqualTo(1), "ação com filhas pede confirmação");
            Assert.That(ability.OnActivate[1], Is.SameAs(repeat));
            _answer = true;
            Assert.That(ActionListView.Execute(list, 1, ActionCommand.Remove, out _), Is.True);
            Assert.That(ability.OnActivate.Select(Seconds), Is.EqualTo(new[] { 1f, 3f }));
        }

        [Test]
        public void RowMenu_ListsDsEntries()
        {
            GameplayAbility ability = _gas.CreateAbility(Wait(1f), Wait(2f), Wait(3f));
            SerializedProperty list = List(ability, nameof(GameplayAbility.OnActivate));
            var row = new ActionRowView();
            row.Bind(list.GetArrayElementAtIndex(0), new List<ValidationMessage>());

            List<ActionMenuEntry> entries = row.MenuEntries().ToList();

            Assert.That(entries.Select(e => e.Name), Is.EqualTo(new[]
            {
                "Duplicar", "Copiar", "Colar", "Mover para cima", "Mover para baixo", "Mover para o topo", "Mover para o fim", "Remover",
            }));
            Assert.That(entries.Where(e => !e.Enabled).Select(e => e.Name), Does.Contain("Mover para cima").And.Contain("Mover para o topo"));
            ActionCommand toEnd = entries.Single(e => e.Name == "Mover para o fim").Command;
            ActionListView.Execute(list, 0, toEnd, out int selection);
            Assert.That(ability.OnActivate.Select(Seconds), Is.EqualTo(new[] { 2f, 3f, 1f }));
            Assert.That(selection, Is.EqualTo(2));
        }

        private static WaitAction Wait(float seconds) => new WaitAction { Duration = ScalableFloat.Constant(seconds) };

        private static float Seconds(GameplayAction action) => ((WaitAction)action).Duration.Evaluate(1);

        private static SerializedProperty List(Object owner, string field) => new SerializedObject(owner).FindProperty(field);
    }
}
