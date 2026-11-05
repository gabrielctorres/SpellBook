using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Campos do efeito por duração, período e pilha (IH6; CA7, CA8; DS do Inspector da habilidade, seções 4 e 7).</summary>
    public class EffectConfigFieldsTests
    {
        private const string FieldPrefix = "PropertyField:";
        private static readonly string[] StackFields = { "MaxStacks", "RefreshDurationOnStack", "stacksMultiplyMagnitude" };

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
                SessionState.EraseString(ActionEventsView.SessionKey(created));
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [TestCase(EffectDurationPolicy.Instant, false)]
        [TestCase(EffectDurationPolicy.Infinite, false)]
        [TestCase(EffectDurationPolicy.HasDuration, true)]
        public void Duration_ShownOnlyWithHasDuration(EffectDurationPolicy policy, bool shown)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, 1f);
            var attribute = typeof(GameplayEffect).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttribute<ShowIfAttribute>();

            using (var serialized = new SerializedObject(effect))
            {
                Assert.That(attribute, Is.Not.Null, "a duração segue com o [ShowIf] do runtime");
                Assert.That(ShowIfRule.IsVisible(serialized.FindProperty("duration"), attribute), Is.EqualTo(shown));
            }
        }

        [TestCase(0f, false)]
        [TestCase(0.5f, true)]
        public void TickGroup_ShownOnlyWithPeriod(float period, bool shown)
        {
            GameplayEffect effect = CreateEffect();
            effect.Period = period;

            VisualElement root = CreateEditor(effect).CreateInspectorGUI();

            Assert.That(IsShown(TickGroup(root)), Is.EqualTo(shown), "o grupo \"Do tique\" inteiro, com o título");
            Assert.That(IsShown(root.Q<PropertyField>(FieldPrefix + "Modifiers").parent), Is.True);
        }

        [TestCase(false, new string[0])]
        [TestCase(true, new[] { "MaxStacks", "RefreshDurationOnStack", "stacksMultiplyMagnitude" })]
        public void StackFields_ShownOnlyWithCanStack(bool canStack, string[] shown)
        {
            GameplayEffect effect = CreateEffect();
            effect.CanStack = canStack;

            VisualElement root = CreateEditor(effect).CreateInspectorGUI();

            Assert.That(VisibleStackFields(root), Is.EqualTo(shown));
            Assert.That(IsShown(root.Q<PropertyField>(FieldPrefix + "CanStack")), Is.True);
        }

        [Test]
        public void ConfigChange_UpdatesFields_KeepsStoredValues()
        {
            GameplayEffect effect = CreateEffect();
            effect.Period = 0.5f;
            effect.CanStack = true;
            effect.MaxStacks = 5;
            effect.RefreshDurationOnStack = false;
            effect.PeriodicModifiers.Add(new AttributeEffectModifier());
            var editor = (GameplayEffectEditor)CreateEditor(effect);
            VisualElement root = editor.CreateInspectorGUI();

            Set(editor, "Period", p => p.floatValue = 0f);
            Set(editor, "CanStack", p => p.boolValue = false);
            editor.RefreshConfigFields();
            Assert.That(IsShown(TickGroup(root)), Is.False);
            Assert.That(VisibleStackFields(root), Is.Empty);

            Set(editor, "Period", p => p.floatValue = 1f);
            Set(editor, "CanStack", p => p.boolValue = true);
            editor.RefreshConfigFields();
            Assert.That(IsShown(TickGroup(root)), Is.True);
            Assert.That(VisibleStackFields(root), Is.EqualTo(StackFields));
            Assert.That(effect.MaxStacks, Is.EqualTo(5), "esconder não apaga o valor");
            Assert.That(effect.RefreshDurationOnStack, Is.False);
            Assert.That(effect.PeriodicModifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void TickTab_HiddenWithActions_WarningInDurationGroup()
        {
            GameplayEffect effect = CreateEffect();
            effect.OnTick.Add(new DebugLogAction());
            var editor = CreateEditor(effect);
            VisualElement root = editor.CreateInspectorGUI();
            var events = root.Q<ActionEventsView>();

            Assert.That(EventTabs(events), Has.None.StartsWith("A cada tick"));
            Assert.That(events.Q<HelpBox>(className: ActionEventsView.HiddenWarningClass), Is.Null, "o aviso sai da faixa de eventos");
            var warning = root.Q<HelpBox>(className: ActionEventsView.HiddenWarningClass);
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.parent, Is.SameAs(root.Q<PropertyField>(FieldPrefix + "Period").parent), "no grupo Duração");
            Assert.That(IsShown(warning), Is.True);
            Assert.That(warning.text, Is.EqualTo("1 ações em \"A cada tick\" não vão rodar com o período atual."));

            Set(editor, "Period", p => p.floatValue = 0.5f);
            events.Refresh();

            Assert.That(EventTabs(events), Has.Some.StartsWith("A cada tick"));
            Assert.That(IsShown(warning), Is.False);
            Assert.That(effect.OnTick.Count, Is.EqualTo(1), "esconder a aba não apaga as ações");
        }

        [Test]
        public void MultiSelection_ShowsFieldsAnyAssetUses()
        {
            GameplayEffect periodic = CreateEffect();
            periodic.Period = 0.5f;
            GameplayEffect stacking = CreateEffect();
            stacking.CanStack = true;
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { periodic, stacking });
            _created.Add(editor);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(IsShown(TickGroup(root)), Is.True);
            Assert.That(VisibleStackFields(root), Is.EqualTo(StackFields));
        }

        private GameplayEffect CreateEffect()
        {
            return _gas.CreateEffect(EffectDurationPolicy.HasDuration, 1f);
        }

        private UnityEditor.Editor CreateEditor(GameplayEffect effect)
        {
            var editor = UnityEditor.Editor.CreateEditor(effect);
            _created.Add(editor);
            return editor;
        }

        private static void Set(UnityEditor.Editor editor, string property, System.Action<SerializedProperty> change)
        {
            editor.serializedObject.Update();
            change(editor.serializedObject.FindProperty(property));
            editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static VisualElement TickGroup(VisualElement root)
        {
            PropertyField field = root.Q<PropertyField>(FieldPrefix + "PeriodicModifiers");
            Assert.That(field, Is.Not.Null, "o campo continua no Inspector, só escondido");
            return field.parent;
        }

        private static bool IsShown(VisualElement element)
        {
            return element.style.display != DisplayStyle.None;
        }

        /// <summary>Campos de pilha que aparecem, na ordem do grupo; o escondido continua no grupo.</summary>
        private static string[] VisibleStackFields(VisualElement root)
        {
            return StackFields
                .Select(name => root.Q<PropertyField>(FieldPrefix + name))
                .Where(field =>
                {
                    Assert.That(field, Is.Not.Null, "o campo continua no Inspector, só escondido");
                    return IsShown(field);
                })
                .Select(field => field.name.Substring(FieldPrefix.Length))
                .ToArray();
        }

        private static IEnumerable<string> EventTabs(ActionEventsView events)
        {
            return events.Query<Tab>().ToList().Select(t => t.label);
        }
    }
}
