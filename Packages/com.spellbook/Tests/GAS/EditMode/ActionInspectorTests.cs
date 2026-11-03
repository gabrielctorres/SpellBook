using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>Inspector de ações: a linha recolhida da lista (EV17a; DS do Inspector de ações, 4.2 e 4.3).</summary>
    public class ActionInspectorTests
    {
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
        public void Row_SummaryFromLine_NoLineShowsTypeName_NullShowsChoose()
        {
            GameplayAbility ability = _gas.CreateAbility(new DebugLogAction { Message = "Oi" }, new WaitAction(), null);

            ActionRowView[] rows = BindRows(ability, 3, new List<ValidationMessage>());

            Assert.That(rows.Select(r => r.SummaryText), Is.EqualTo(new[] { "Log: Oi", "Wait Action", ActionRowView.ChooseText }));
            Assert.That(rows[0].ClassListContains("action-row--cat-debug"), Is.True);
            Assert.That(rows[1].ClassListContains("action-row--cat-flow"), Is.True);
            Assert.That(rows[2].ClassListContains(ActionRowView.EmptyClass), Is.True);
            Assert.That(rows[0].tooltip, Is.EqualTo("Log: Oi"), "o tooltip mostra o resumo inteiro");
        }

        [Test]
        public void Row_ValidationIcon_OnlyWithMessage()
        {
            GameplayAbility ability = _gas.CreateAbility(new WaitAction(), new WaitAction(), new WaitAction());
            var messages = new List<ValidationMessage>
            {
                new ValidationMessage(ValidationSeverity.Warning, "Aviso da segunda.", ability, "OnActivate.Array.data[1].duration"),
                new ValidationMessage(ValidationSeverity.Error, "Erro da décima.", ability, "OnActivate.Array.data[10]"),
            };

            ActionRowView[] rows = BindRows(ability, 3, messages);

            Assert.That(rows[0].ValidationIcon.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(rows[1].ValidationIcon.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(rows[1].ValidationIcon.tooltip, Is.EqualTo("Aviso da segunda."));
            Assert.That(rows[1].ValidationIcon.ClassListContains(ActionRowView.WarningClass), Is.True);
            Assert.That(rows[2].ValidationIcon.style.display.value, Is.EqualTo(DisplayStyle.None), "data[1] não casa com data[10]");
        }

        private static ActionRowView[] BindRows(GameplayAbility ability, int count, IReadOnlyList<ValidationMessage> messages)
        {
            var serialized = new SerializedObject(ability);
            SerializedProperty list = serialized.FindProperty(nameof(GameplayAbility.OnActivate));
            return Enumerable.Range(0, count).Select(i =>
            {
                var row = new ActionRowView();
                row.Bind(list.GetArrayElementAtIndex(i), messages);
                return row;
            }).ToArray();
        }
    }
}
