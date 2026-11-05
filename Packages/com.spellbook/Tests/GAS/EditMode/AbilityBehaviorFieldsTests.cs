using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Campos da habilidade por comportamento (IH2; CA3, CA8; DS do Inspector da habilidade, seção 3).</summary>
    public class AbilityBehaviorFieldsTests
    {
        private const string FieldPrefix = "PropertyField:";
        private static readonly string[] BehaviorFields = { "channelDuration", "channelTickInterval", "reactivatePassive", "autocast" };

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

        [TestCase(AbilityBehavior.Instant, new[] { "autocast" })]
        [TestCase(AbilityBehavior.Passive, new[] { "reactivatePassive" })]
        [TestCase(AbilityBehavior.Toggle, new string[0])]
        [TestCase(AbilityBehavior.Channel, new[] { "channelDuration", "channelTickInterval", "autocast" })]
        public void BehaviorFields_ShownOnlyForBehavior(AbilityBehavior behavior, string[] shown)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Behavior = behavior;

            VisualElement root = CreateEditor(ability).CreateInspectorGUI();

            Assert.That(Visible(root), Is.EqualTo(shown));
        }

        [Test]
        public void BehaviorChange_UpdatesFields_KeepsStoredValues()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ReactivatePassive = true;
            ability.Autocast.Allowed = true;
            var editor = (GameplayAbilityEditor)CreateEditor(ability);
            VisualElement root = editor.CreateInspectorGUI();

            SetBehavior(editor, AbilityBehavior.Channel);
            editor.serializedObject.FindProperty("channelTickInterval").floatValue = 0.5f;
            editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            editor.RefreshBehaviorFields();
            Assert.That(Visible(root), Is.EqualTo(new[] { "channelDuration", "channelTickInterval", "autocast" }));

            SetBehavior(editor, AbilityBehavior.Passive);
            editor.RefreshBehaviorFields();
            Assert.That(Visible(root), Is.EqualTo(new[] { "reactivatePassive" }));

            SetBehavior(editor, AbilityBehavior.Instant);
            editor.RefreshBehaviorFields();
            Assert.That(Visible(root), Is.EqualTo(new[] { "autocast" }));
            Assert.That(ability.ReactivatePassive, Is.True, "esconder não apaga o valor");
            Assert.That(ability.Autocast.Allowed, Is.True);
            Assert.That(ability.ChannelTickInterval, Is.EqualTo(0.5f));
        }

        [Test]
        public void MultiSelection_MixedBehaviors_ShowsFieldsAnyAssetUses()
        {
            GameplayAbility passive = _gas.CreateAbility();
            passive.Behavior = AbilityBehavior.Passive;
            GameplayAbility channel = _gas.CreateAbility();
            channel.Behavior = AbilityBehavior.Channel;
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { passive, channel });
            _created.Add(editor);
            _created.Add(passive);
            _created.Add(channel);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(Visible(root), Is.EqualTo(BehaviorFields));
        }

        private UnityEditor.Editor CreateEditor(GameplayAbility ability)
        {
            var editor = UnityEditor.Editor.CreateEditor(ability);
            _created.Add(editor);
            _created.Add(ability);
            return editor;
        }

        private static void SetBehavior(UnityEditor.Editor editor, AbilityBehavior behavior)
        {
            editor.serializedObject.Update();
            editor.serializedObject.FindProperty("behavior").enumValueIndex = (int)behavior;
            editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Campos dependentes do comportamento que aparecem, na ordem do grupo; o escondido continua no grupo.</summary>
        private static string[] Visible(VisualElement root)
        {
            return BehaviorFields
                .Select(name => root.Q<PropertyField>(FieldPrefix + name))
                .Where(field =>
                {
                    Assert.That(field, Is.Not.Null, "o campo continua no Inspector, só escondido");
                    return field.style.display != DisplayStyle.None;
                })
                .Select(field => field.name.Substring(FieldPrefix.Length))
                .ToArray();
        }
    }
}
