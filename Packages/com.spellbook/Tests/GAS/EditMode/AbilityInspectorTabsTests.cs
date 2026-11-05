using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Habilidade de teste com um campo que o editor não conhece (CA1, campo de subclasse).</summary>
    internal sealed class InspectorTabsTestAbility : GameplayAbility
    {
        [SerializeField] private int extraField = 1;

        internal int ExtraField => extraField;
    }

    /// <summary>Quatro abas do Inspector da habilidade (IH1; CA1, CA2, CA8; DS do Inspector da habilidade, seções 2 e 3).</summary>
    public class AbilityInspectorTabsTests
    {
        private const string FieldPrefix = "PropertyField:";

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
                SessionState.EraseString(InspectorTabsView.SessionKey(created));
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Tabs_FourInOrder_BelowValidation()
        {
            VisualElement root = CreateInspector(_gas.CreateAbility());

            var tabs = root.Q<InspectorTabsView>();
            Assert.That(tabs, Is.Not.Null);
            Assert.That(TopTabs(tabs).Select(t => t.label),
                Is.EqualTo(new[] { "Configuração", "Ações", "Tags e gatilhos", "Níveis e prévia" }));
            var children = root.Children().ToList();
            Assert.That(children.IndexOf(tabs), Is.GreaterThan(children.IndexOf(root.Q(className: SpellBookEditor.ValidationClass))));
        }

        [Test]
        public void Configuration_FourGroupsInOrder_WithDsFields()
        {
            VisualElement root = CreateInspector(_gas.CreateAbility());
            VisualElement configuration = Tab(root, "Configuração");

            Assert.That(GroupTitles(configuration),
                Is.EqualTo(new[] { "Identidade", "Comportamento", "Custo e recarga", "Condições de ativação" }));
            Assert.That(FieldNames(configuration), Is.EqualTo(new[]
            {
                "Icon", "localizedName", "AbilityTag", "Description",
                "behavior", "commitMode", "channelDuration", "channelTickInterval", "reactivatePassive", "autocast",
                "CostEffect", "CooldownEffect", "CooldownReductionAttribute", "cooldownReductionPolicy", "charges",
                "castRange", "activationConditions"
            }));
        }

        [Test]
        public void Fields_EachInOneTab_ScriptAboveTabs()
        {
            VisualElement root = CreateInspector(_gas.CreateAbility());

            Assert.That(FieldNames(Tab(root, "Ações")), Is.Empty);
            Assert.That(Tab(root, "Ações").Q<ActionEventsView>(), Is.Not.Null);
            Assert.That(FieldNames(Tab(root, "Tags e gatilhos")), Is.EqualTo(new[]
            {
                "BlockedTags", "RequiredTags", "ActivationOwnedTags", "CancelAbilitiesWithTag", "BlockAbilitiesWithTag",
                "CancelledByTags", "triggers"
            }));
            Assert.That(GroupTitles(Tab(root, "Tags e gatilhos")), Is.EqualTo(new[] { "Tags", "Gatilhos" }));
            Assert.That(FieldNames(Tab(root, "Níveis e prévia")), Is.EqualTo(new[] { "maxLevel", "values" }));
            Assert.That(GroupTitles(Tab(root, "Níveis e prévia")), Is.EqualTo(new[] { "Níveis", "Prévia" }));
            Assert.That(root.Children().OfType<PropertyField>().Select(f => f.name), Is.EqualTo(new[] { FieldPrefix + "m_Script" }),
                "fora das abas fica só a linha do script");
            Assert.That(root.Q(className: InspectorTabsView.OthersGroupClass), Is.Null, "sem campo desconhecido, sem \"Outros\"");
        }

        [Test]
        public void RuleSummary_InActivationConditions_BelowRules()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule());
            VisualElement root = CreateInspector(ability);

            VisualElement group = root.Q(GameplayAbilityEditor.ActivationName);
            Assert.That(Tab(root, "Configuração").Contains(group), Is.True);
            Assert.That(GroupTitles(group.parent).Last(), Is.EqualTo("Condições de ativação"));
            var rows = group.Query<Label>(className: GameplayAbilityEditor.RuleRowClass).ToList();
            Assert.That(rows.Count, Is.EqualTo(1));
            List<VisualElement> order = group.Query<VisualElement>().ToList();
            Assert.That(order.IndexOf(rows[0]), Is.GreaterThan(order.IndexOf(group.Q(FieldPrefix + "activationConditions"))),
                "o resumo fica logo abaixo das regras");
        }

        [Test]
        public void PreviewElements_InLevelsTab()
        {
            VisualElement root = CreateInspector(_gas.CreateAbility());
            VisualElement levels = Tab(root, "Níveis e prévia");

            Assert.That(levels.Q(GameplayAbilityEditor.PreviewLevelName), Is.Not.Null);
            Assert.That(levels.Q(GameplayAbilityEditor.PreviewName), Is.Not.Null);
            Assert.That(levels.Q(GameplayAbilityEditor.ChargesPreviewName), Is.Not.Null);
            Assert.That(levels.Q(GameplayAbilityEditor.DescriptionSectionName), Is.Not.Null);
        }

        [Test]
        public void UnknownField_InOthersAtEndOfConfiguration()
        {
            var ability = ScriptableObject.CreateInstance<InspectorTabsTestAbility>();
            VisualElement root = CreateInspector(ability);
            VisualElement configuration = Tab(root, "Configuração");

            Assert.That(GroupTitles(configuration).Last(), Is.EqualTo("Outros"));
            Assert.That(FieldNames(configuration).Last(), Is.EqualTo("extraField"));
        }

        [Test]
        public void ActiveTab_RememberedPerAsset_NewAssetOpensConfiguration()
        {
            GameplayAbility first = _gas.CreateAbility();
            GameplayAbility second = _gas.CreateAbility();

            CreateInspector(first).Q<InspectorTabsView>().Select("Tags e gatilhos");

            Assert.That(CreateInspector(first).Q<InspectorTabsView>().ActiveTab, Is.EqualTo("Tags e gatilhos"));
            Assert.That(CreateInspector(second).Q<InspectorTabsView>().ActiveTab, Is.EqualTo("Configuração"));
        }

        [Test]
        public void MultiSelection_ShowsTabs_ActionsFollowDs()
        {
            GameplayAbility first = _gas.CreateAbility();
            GameplayAbility second = _gas.CreateAbility();
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            _created.Add(editor);
            _created.Add(first);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(TopTabs(root.Q<InspectorTabsView>()).Count, Is.EqualTo(4));
            Assert.That(Tab(root, "Configuração").Q<PropertyField>(FieldPrefix + "behavior"), Is.Not.Null);
            Assert.That(Tab(root, "Ações").Query<Label>().ToList().Select(l => l.text),
                Has.Some.EqualTo(ActionEventsView.MultiSelectionText));
        }

        [Test]
        public void RuntimeHeaders_Removed()
        {
            FieldInfo[] withHeader = typeof(GameplayAbility)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttribute<HeaderAttribute>() != null)
                .ToArray();

            Assert.That(withHeader.Select(f => f.Name), Is.Empty, "os títulos vêm dos grupos, sem [Header] duplicado");
        }

        private VisualElement CreateInspector(GameplayAbility ability)
        {
            var editor = UnityEditor.Editor.CreateEditor(ability);
            _created.Add(editor);
            _created.Add(ability);
            return editor.CreateInspectorGUI();
        }

        /// <summary>Só as abas do topo, sem as abas de eventos que ficam dentro de Ações.</summary>
        private static List<Tab> TopTabs(InspectorTabsView tabs)
        {
            return tabs.Query<Tab>().Where(t => t.GetFirstAncestorOfType<ActionEventsView>() == null).ToList();
        }

        private static VisualElement Tab(VisualElement root, string label)
        {
            return TopTabs(root.Q<InspectorTabsView>()).Single(t => t.label == label);
        }

        private static string[] GroupTitles(VisualElement parent)
        {
            return parent.Query<Label>(className: InspectorTabsView.GroupTitleClass).ToList().Select(l => l.text).ToArray();
        }

        /// <summary>Campos de primeiro nível da aba, na ordem, sem os de dentro das listas de ações.</summary>
        private static string[] FieldNames(VisualElement tab)
        {
            return tab.Query<PropertyField>().ToList()
                .Where(f => f.name.StartsWith(FieldPrefix) && !f.name.Contains('.'))
                .Where(f => f.GetFirstAncestorOfType<ActionEventsView>() == null)
                .Select(f => f.name.Substring(FieldPrefix.Length))
                .ToArray();
        }
    }
}
