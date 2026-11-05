using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Efeito de teste com um campo que o editor não conhece (CA6, campo de subclasse).</summary>
    internal sealed class InspectorTabsTestEffect : GameplayEffect
    {
        [SerializeField] private int extraField = 1;

        internal int ExtraField => extraField;
    }

    /// <summary>Quatro abas do Inspector do efeito (IH5; CA5, CA6, CA8; DS do Inspector da habilidade, seções 2, 4 e 6).</summary>
    public class EffectInspectorTabsTests
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
            VisualElement root = CreateInspector(CreateEffect());

            var tabs = root.Q<InspectorTabsView>();
            Assert.That(tabs, Is.Not.Null);
            Assert.That(TopTabs(tabs).Select(t => t.label), Is.EqualTo(new[] { "Configuração", "Modificadores", "Ações", "Tags" }));
            var children = root.Children().ToList();
            Assert.That(children.IndexOf(tabs), Is.GreaterThan(children.IndexOf(root.Q(className: SpellBookEditor.ValidationClass))));
        }

        [Test]
        public void Configuration_ThreeGroupsInOrder_WithDsFields()
        {
            VisualElement configuration = Tab(CreateInspector(CreateEffect()), "Configuração");

            Assert.That(GroupTitles(configuration), Is.EqualTo(new[] { "Identidade", "Duração", "Empilhamento" }));
            Assert.That(FieldNames(configuration), Is.EqualTo(new[]
            {
                "localizedName", "icon", "isBuff", "isDebuff", "isHidden", "isPurgable",
                "DurationPolicy", "duration", "defaultLevel", "Period",
                "CanStack", "MaxStacks", "RefreshDurationOnStack", "stacksMultiplyMagnitude"
            }));
        }

        [Test]
        public void Modifiers_PersistentAndTickGroups()
        {
            VisualElement modifiers = Tab(CreateInspector(CreateEffect()), "Modificadores");

            Assert.That(GroupTitles(modifiers), Is.EqualTo(new[] { "Persistentes", "Do tique" }));
            Assert.That(FieldNames(modifiers), Is.EqualTo(new[] { "Modifiers", "PeriodicModifiers" }));
        }

        [Test]
        public void Actions_EventTabs_ThenReactionAttributes()
        {
            VisualElement actions = Tab(CreateInspector(CreateEffect()), "Ações");

            var events = actions.Q<ActionEventsView>();
            Assert.That(events, Is.Not.Null);
            Assert.That(events.Query<Label>(className: ActionEventsView.GroupLabelClass).ToList().Select(l => l.text),
                Is.EqualTo(new[] { "Reações" }), "as reações seguem agrupadas na faixa de eventos");
            Assert.That(FieldNames(actions), Is.EqualTo(new[] { "damageAttributes", "deathAttribute" }));
            VisualElement group = actions.Q<PropertyField>(FieldPrefix + "damageAttributes").parent;
            Assert.That(actions.IndexOf(group), Is.GreaterThan(actions.IndexOf(events)), "os atributos ficam abaixo das abas de eventos");
            Assert.That(GroupTitles(actions), Is.EqualTo(new[] { "Reações" }));
        }

        [Test]
        public void Fields_EachInOneTab_ScriptAboveTabs()
        {
            VisualElement root = CreateInspector(CreateEffect());

            Assert.That(FieldNames(Tab(root, "Tags")),
                Is.EqualTo(new[] { "damageTags", "GrantedTags", "ApplicationRequirements", "ApplicationImmunityTags" }));
            Assert.That(GroupTitles(Tab(root, "Tags")), Is.EqualTo(new[] { "Tags" }));
            Assert.That(root.Children().OfType<PropertyField>().Select(f => f.name), Is.EqualTo(new[] { FieldPrefix + "m_Script" }),
                "fora das abas fica só a linha do script");
            Assert.That(root.Q(className: InspectorTabsView.OthersGroupClass), Is.Null, "sem campo desconhecido, sem \"Outros\"");
        }

        [Test]
        public void TagsGroup_FourCompactLists_CountsListsWithItems()
        {
            GameplayEffect effect = CreateEffect();
            effect.GrantedTags.Add(_gas.CreateTag("State.Burning"));
            var editor = UnityEditor.Editor.CreateEditor(effect);
            _created.Add(editor);
            _created.Add(effect);
            VisualElement root = editor.CreateInspectorGUI();
            VisualElement tags = Tab(root, "Tags");

            List<CompactTagList> lists = tags.Query<CompactTagList>().ToList();
            Assert.That(lists.Select(l => l.Field.name.Substring(FieldPrefix.Length)),
                Is.EqualTo(new[] { "damageTags", "GrantedTags", "ApplicationRequirements", "ApplicationImmunityTags" }));
            Assert.That(lists.Select(l => l.IsCompact), Is.EqualTo(new[] { true, false, true, true }));
            Label count = tags.Q<Label>(CompactTagList.CountName);
            Assert.That(count.text, Is.EqualTo("1 de 4 com itens"));

            effect.DamageTags.Add(_gas.CreateTag("Damage.Fire"));
            editor.serializedObject.Update();
            CompactTagList.RefreshAll(count.parent.parent);

            Assert.That(count.text, Is.EqualTo("2 de 4 com itens"));
            Assert.That(lists[0].IsCompact, Is.False, "o conjunto de tipos de dano conta pelas tags de dentro");
        }

        [Test]
        public void DamageTags_Empty_OneRowWithFieldName_AddButtonAddsTag()
        {
            GameplayEffect effect = CreateEffect();
            VisualElement root = CreateInspector(effect);
            CompactTagList damage = Tab(root, "Tags").Query<CompactTagList>().First();

            VisualElement row = damage.Q(className: CompactTagList.EmptyRowClass);
            Label name = row.Q<Label>();
            Assert.That(name.text, Is.EqualTo("Damage Tags"));
            Assert.That(name.tooltip, Does.StartWith("Tipos de dano do efeito."));
            Assert.That(damage.Field.style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)));

            damage.AddItem();

            Assert.That(effect.DamageTags.Count, Is.EqualTo(1));
            Assert.That(damage.IsCompact, Is.False);
            Assert.That(root.Q<Label>(CompactTagList.CountName).text, Is.EqualTo("1 de 4 com itens"));
        }

        [Test]
        public void UnknownField_InOthersAtEndOfConfiguration()
        {
            var effect = ScriptableObject.CreateInstance<InspectorTabsTestEffect>();
            VisualElement configuration = Tab(CreateInspector(effect), "Configuração");

            Assert.That(GroupTitles(configuration).Last(), Is.EqualTo("Outros"));
            Assert.That(FieldNames(configuration).Last(), Is.EqualTo("extraField"));
        }

        [Test]
        public void ActiveTab_RememberedPerAsset_NewAssetOpensConfiguration()
        {
            GameplayEffect first = CreateEffect();
            GameplayEffect second = CreateEffect();

            CreateInspector(first).Q<InspectorTabsView>().Select("Tags");

            Assert.That(CreateInspector(first).Q<InspectorTabsView>().ActiveTab, Is.EqualTo("Tags"));
            Assert.That(CreateInspector(second).Q<InspectorTabsView>().ActiveTab, Is.EqualTo("Configuração"));
        }

        [Test]
        public void MultiSelection_FieldsEditAll_ActionsFollowDs()
        {
            GameplayEffect first = CreateEffect();
            GameplayEffect second = CreateEffect();
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            _created.Add(editor);
            _created.Add(first);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(TopTabs(root.Q<InspectorTabsView>()).Count, Is.EqualTo(4));
            var buff = Tab(root, "Configuração").Q<PropertyField>(FieldPrefix + "isBuff");
            Assert.That(buff, Is.Not.Null);
            Assert.That(buff.bindingPath, Is.EqualTo("isBuff"));
            Assert.That(Tab(root, "Ações").Query<Label>().ToList().Select(l => l.text),
                Has.Some.EqualTo(ActionEventsView.MultiSelectionText));

            editor.serializedObject.FindProperty("isBuff").boolValue = true;
            editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(first.IsBuff && second.IsBuff, Is.True, "o metadado muda nos dois assets");
        }

        [Test]
        public void RuntimeHeaders_Removed()
        {
            FieldInfo[] withHeader = typeof(GameplayEffect)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.GetCustomAttribute<HeaderAttribute>() != null)
                .ToArray();

            Assert.That(withHeader.Select(f => f.Name), Is.Empty, "os títulos vêm dos grupos, sem [Header] duplicado");
        }

        private GameplayEffect CreateEffect()
        {
            return _gas.CreateEffect(EffectDurationPolicy.HasDuration, 1f);
        }

        private VisualElement CreateInspector(GameplayEffect effect)
        {
            var editor = UnityEditor.Editor.CreateEditor(effect);
            _created.Add(editor);
            _created.Add(effect);
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
