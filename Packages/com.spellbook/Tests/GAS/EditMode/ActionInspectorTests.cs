using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Inspector de ações: abas por evento (EV17b), lista e linha recolhida (EV17a); DS do Inspector de ações, 3, 4.2, 4.3 e 9.</summary>
    public class ActionInspectorTests
    {
        private GasFactory _gas;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                SessionState.EraseString(ActionEventsView.SessionKey(created));
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Tabs_OnePerActionEvent_CounterAndValidationDot()
        {
            GameplayAbility ability = _gas.CreateAbility(new DebugLogAction(), null);
            ability.OnEnd.Add(new DebugLogAction());

            VisualElement root = CreateInspector(ability);

            Tab[] tabs = root.Query<Tab>().ToList().ToArray();
            Assert.That(tabs.Select(t => t.label), Is.EqualTo(new[]
            {
                "Ao ativar · 2", "Ao terminar · 1", "Ao ser cancelada", "Ao ser concedida", "Ao ser retirada"
            }));
            Label dot = tabs[0].tabHeader.Q<Label>(className: ActionEventsView.DotClass);
            Assert.That(dot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(dot.text, Is.EqualTo("1"));
            Assert.That(dot.ClassListContains(ActionEventsView.DotErrorClass), Is.True);
            Assert.That(tabs[1].tabHeader.Q<Label>(className: ActionEventsView.DotClass).style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<PropertyField>("PropertyField:OnActivate"), Is.Null, "a lista não aparece também no Inspector padrão");
        }

        [Test]
        public void EffectTabs_ReactionsGrouped()
        {
            GameplayEffect effect = _gas.CreateDurationEffect(1f);

            VisualElement root = CreateInspector(effect);

            Tab[] tabs = root.Query<Tab>().ToList().ToArray();
            Assert.That(tabs.Select(t => t.label), Is.EqualTo(new[]
            {
                "Ao ser aplicado", "A cada tick", "Ao ser removido", "Ao receber dano", "Ao morrer", "Ao executar habilidade"
            }));
            Label[] groups = root.Query<Label>(className: ActionEventsView.GroupLabelClass).ToList().ToArray();
            Assert.That(groups.Length, Is.EqualTo(1));
            Assert.That(groups[0].text, Is.EqualTo("Reações"));
            Assert.That(tabs[3].tabHeader.Contains(groups[0]), Is.True, "o rótulo abre o grupo na primeira reação");
        }

        [Test]
        public void ChannelTab_OnlyWithChannel_HiddenWithWarning()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.OnChannelTick.Add(new DebugLogAction());
            VisualElement root = CreateInspector(ability);
            var view = root.Q<ActionEventsView>();

            Assert.That(root.Query<Tab>().ToList().Select(t => t.label), Has.None.StartsWith("A cada passo"));
            var warning = view.Q<HelpBox>(className: ActionEventsView.HiddenWarningClass);
            Assert.That(warning.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(warning.text, Is.EqualTo("1 ações em \"A cada passo\" não vão rodar com o comportamento atual."));

            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty("behavior").enumValueIndex = (int)AbilityBehavior.Channel;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            view.Refresh();

            Assert.That(root.Query<Tab>().ToList().Select(t => t.label), Has.Some.EqualTo("A cada passo · 1"));
            Assert.That(warning.style.display.value, Is.EqualTo(DisplayStyle.None));
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

        [Test]
        public void TabRemembered_PerAssetInSession()
        {
            GameplayAbility first = _gas.CreateAbility();
            first.OnCancel.Add(new DebugLogAction());
            GameplayAbility second = _gas.CreateAbility();
            _created.Add(first);
            _created.Add(second);

            Assert.That(CreateView(first).ActiveEvent, Is.EqualTo("OnCancel"), "asset novo abre na primeira aba com ação");
            CreateView(first).Select("OnRemoved");

            Assert.That(CreateView(first).ActiveEvent, Is.EqualTo("OnRemoved"));
            Assert.That(CreateView(second).ActiveEvent, Is.EqualTo("OnActivate"), "sem ação, abre na primeira");
        }

        [Test]
        public void MultiSelection_HidesTabs()
        {
            GameplayAbility first = _gas.CreateAbility(new DebugLogAction());
            GameplayAbility second = _gas.CreateAbility(new DebugLogAction());
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            _created.Add(editor);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(root.Query<Tab>().ToList(), Is.Empty);
            Assert.That(root.Query<Label>().ToList().Select(l => l.text), Has.Some.EqualTo(ActionEventsView.MultiSelectionText));
        }

        private VisualElement CreateInspector(Object target)
        {
            var editor = UnityEditor.Editor.CreateEditor(target);
            _created.Add(editor);
            _created.Add(target);
            return editor.CreateInspectorGUI();
        }

        private ActionEventsView CreateView(GameplayAbility ability)
        {
            return new ActionEventsView(new SerializedObject(ability), "Vazia.", field => field != nameof(GameplayAbility.OnChannelTick));
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
