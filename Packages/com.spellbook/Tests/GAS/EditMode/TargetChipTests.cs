using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Targeting;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>Chip de alvo e bloco "Alvo" da linha de ação (EV18; DS do Inspector de ações, 4.2 e 5).</summary>
    public class TargetChipTests
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
        public void Chip_TextPerSelector_AreaSummary()
        {
            var area = new AreaSelector { Radius = ScalableFloat.Constant(5f), MaxTargets = 2, Filter = new TagQueryCondition() };
            GameplayAbility ability = _gas.CreateAbility(
                new ApplyEffectAction(),
                new ApplyEffectAction { Target = new CasterSelector() },
                new ApplyEffectAction { Target = new EventTargetsSelector() },
                new ApplyEffectAction { Target = new PointSelector() },
                new ApplyEffectAction { Target = area },
                new WaitAction());

            ActionRowView[] rows = BindRows(ability, nameof(GameplayAbility.OnActivate), 6, new List<ValidationMessage>());

            Assert.That(rows.Take(5).Select(r => r.Chip.Text), Is.EqualTo(new[]
            {
                "→ Padrão do evento", "→ Quem lançou", "→ Alvos do evento", "→ Ponto",
                "→ Área 5 m · 2 alvos · Tags casam com a consulta",
            }));
            Assert.That(rows[0].Chip.ClassListContains(TargetChipView.DefaultClass), Is.True);
            Assert.That(rows[0].Chip.tooltip, Is.EqualTo("Neste evento, o padrão são os alvos do evento."));
            Assert.That(rows[1].Chip.ClassListContains(TargetChipView.DefaultClass), Is.False);
            Assert.That(rows[5].Chip.style.display.value, Is.EqualTo(DisplayStyle.None), "Esperar não usa alvo");
        }

        [Test]
        public void Chip_IncompatibleWithEvent_RedNotBlocked()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.OnGranted.Add(new ApplyEffectAction { Target = new EventTargetsSelector() });
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new UnityEngine.Object[] { ability });

            ActionRowView row = BindRows(ability, nameof(GameplayAbility.OnGranted), 1, messages)[0];

            Assert.That(row.Chip.Text, Is.EqualTo("✖ → Alvos do evento"));
            Assert.That(row.Chip.ClassListContains(TargetChipView.ErrorClass), Is.True);
            Assert.That(row.Chip.tooltip, Does.Contain("este evento não tem alvos"));
            Assert.That(row.Chip.enabledSelf, Is.True, "o seletor não é bloqueado");
        }

        [Test]
        public void AreaSelector_OpensTargetBlockBeforeActionFields()
        {
            GameplayAbility ability = _gas.CreateAbility(new ApplyEffectAction { Target = new AreaSelector() }, new ApplyEffectAction { Target = new CasterSelector() });
            var serialized = new SerializedObject(ability);
            SerializedProperty list = serialized.FindProperty(nameof(GameplayAbility.OnActivate));
            list.GetArrayElementAtIndex(0).isExpanded = true;
            list.GetArrayElementAtIndex(1).isExpanded = true;

            ActionRowView[] rows = BindRows(ability, nameof(GameplayAbility.OnActivate), 2, new List<ValidationMessage>(), serialized);

            VisualElement body = rows[0].Q(className: ActionRowView.ClassName + "__body");
            VisualElement block = body.Q(className: TargetChipView.BlockClass);
            Assert.That(block, Is.Not.Null);
            Assert.That(body.IndexOf(block), Is.EqualTo(0), "o bloco vem antes dos campos da ação");
            Assert.That(block.Q<Label>().text, Is.EqualTo("Alvo"));
            Assert.That(block.Query<PropertyField>().ToList().Select(f => f.bindingPath.Substring(f.bindingPath.LastIndexOf('.') + 1)),
                Is.EqualTo(new[] { "origin", "physics", "radius", "filter", "includeOwner", "maxTargets", "order", "layers" }));
            Assert.That(rows[1].Q(className: TargetChipView.BlockClass), Is.Null, "seletor sem campos não abre o bloco");
        }

        [Test]
        public void ForEachChild_ChipTooltipSaysCurrentTarget()
        {
            var forEach = new ForEachTargetAction();
            forEach.Actions.Add(new ApplyEffectAction());
            GameplayAbility ability = _gas.CreateAbility(forEach);
            var serialized = new SerializedObject(ability);
            SerializedProperty child = serialized.FindProperty(nameof(GameplayAbility.OnActivate)).GetArrayElementAtIndex(0)
                .FindPropertyRelative("actions").GetArrayElementAtIndex(0);

            var row = new ActionRowView();
            row.Bind(child, new List<ValidationMessage>());

            Assert.That(row.Chip.Text, Is.EqualTo("→ Padrão do evento"));
            Assert.That(row.Chip.tooltip, Is.EqualTo("Dentro do ForEach, o padrão é o alvo corrente."));
        }

        [Test]
        public void Picker_NoneLabel_ReadsEventDefault()
        {
            TypePickerTree tree = TypePickerTree.Build(TypePickerCatalog.GetCandidates(typeof(TargetSelector)), TargetChipView.DefaultLabel);

            TypePickerNode none = tree.Children(tree.Root)[0];

            Assert.That(none.Kind, Is.EqualTo(TypePickerNodeKind.None));
            Assert.That(none.DisplayName, Is.EqualTo("(Padrão do evento)"));
        }

        private static ActionRowView[] BindRows(GameplayAbility ability, string field, int count,
            IReadOnlyList<ValidationMessage> messages, SerializedObject serialized = null)
        {
            serialized = serialized ?? new SerializedObject(ability);
            SerializedProperty list = serialized.FindProperty(field);
            return Enumerable.Range(0, count).Select(i =>
            {
                var row = new ActionRowView();
                row.Bind(list.GetArrayElementAtIndex(i), messages);
                return row;
            }).ToArray();
        }
    }
}
