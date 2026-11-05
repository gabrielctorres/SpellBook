using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Listas de tags compactas e contador no grupo Tags da habilidade (IH4; CA5, CA8; DS do Inspector da habilidade, seção 6).</summary>
    public class AbilityCompactTagListsTests
    {
        private static readonly string[] TagFields =
        {
            "BlockedTags", "RequiredTags", "ActivationOwnedTags", "CancelAbilitiesWithTag", "BlockAbilitiesWithTag", "CancelledByTags"
        };

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
        public void EmptyList_OneRow_NameTooltipAndAddButton()
        {
            VisualElement root = CreateEditor(_gas.CreateAbility()).CreateInspectorGUI();

            List<CompactTagList> lists = root.Query<CompactTagList>().ToList();
            Assert.That(lists.Select(l => l.Field.name.Substring("PropertyField:".Length)), Is.EqualTo(TagFields));
            CompactTagList blocked = lists[0];
            Assert.That(blocked.IsCompact, Is.True);
            Assert.That(blocked.Field.style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)),
                "vazia, a lista inteira dá lugar a uma linha");
            VisualElement row = blocked.Q(className: CompactTagList.EmptyRowClass);
            Label name = row.Q<Label>();
            Assert.That(name.text, Is.EqualTo("Blocked Tags"));
            Assert.That(name.tooltip, Is.EqualTo("Não ativa se o dono tiver alguma destas tags."));
            Assert.That(row.Q<Button>().text, Is.EqualTo(CompactTagList.AddText));
        }

        [Test]
        public void ListWithItems_ShowsFullField()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.RequiredTags = new List<GameplayTag> { _gas.CreateTag("State.Ready") };

            VisualElement root = CreateEditor(ability).CreateInspectorGUI();

            CompactTagList required = List(root, "RequiredTags");
            Assert.That(required.IsCompact, Is.False);
            Assert.That(required.Field.style.display, Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.Flex)));
            Assert.That(required.Q(className: CompactTagList.EmptyRowClass).style.display,
                Is.EqualTo(new StyleEnum<DisplayStyle>(DisplayStyle.None)));
        }

        [Test]
        public void AddButton_AddsItem_ShowsFullField()
        {
            GameplayAbility ability = _gas.CreateAbility();
            VisualElement root = CreateEditor(ability).CreateInspectorGUI();
            CompactTagList blocked = List(root, "BlockedTags");

            blocked.AddItem();

            Assert.That(ability.BlockedTags, Has.Count.EqualTo(1));
            Assert.That(blocked.IsCompact, Is.False);
            Assert.That(root.Q<Label>(CompactTagList.CountName).text, Is.EqualTo("1 de 6 com itens"));
        }

        [Test]
        public void TagsGroup_CountsListsWithItems()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.BlockedTags = new List<GameplayTag> { _gas.CreateTag("State.Stunned") };
            ability.CancelledByTags = new List<GameplayTag> { _gas.CreateTag("State.Silenced") };
            var editor = (GameplayAbilityEditor)CreateEditor(ability);
            VisualElement root = editor.CreateInspectorGUI();

            Label count = root.Q<Label>(CompactTagList.CountName);
            Assert.That(count.text, Is.EqualTo("2 de 6 com itens"));
            VisualElement group = count.parent.parent;
            Assert.That(group.Q<Label>(className: InspectorTabsView.GroupTitleClass).text, Is.EqualTo("Tags"));
            Assert.That(group.Query<CompactTagList>().ToList(), Has.Count.EqualTo(6), "o contador fica no título do grupo das listas");

            ability.CancelledByTags.Clear();
            editor.serializedObject.Update();
            editor.RefreshTagLists();

            Assert.That(count.text, Is.EqualTo("1 de 6 com itens"));
            Assert.That(List(root, "CancelledByTags").IsCompact, Is.True);
        }

        [Test]
        public void MultiSelection_ItemsInAnyAsset_ShowsFullField()
        {
            GameplayAbility first = _gas.CreateAbility();
            GameplayAbility second = _gas.CreateAbility();
            second.BlockedTags = new List<GameplayTag> { _gas.CreateTag("State.Stunned") };
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            _created.Add(editor);
            _created.Add(first);
            _created.Add(second);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(List(root, "BlockedTags").IsCompact, Is.False, "a lista não esconde os itens de um dos assets");
            Assert.That(List(root, "RequiredTags").IsCompact, Is.True);
        }

        private UnityEditor.Editor CreateEditor(GameplayAbility ability)
        {
            var editor = UnityEditor.Editor.CreateEditor(ability);
            _created.Add(editor);
            _created.Add(ability);
            return editor;
        }

        private static CompactTagList List(VisualElement root, string field)
        {
            return root.Query<CompactTagList>().ToList().Single(l => l.Field.name == "PropertyField:" + field);
        }
    }
}
