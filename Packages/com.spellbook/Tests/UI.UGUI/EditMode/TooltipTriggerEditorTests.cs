using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.GAS.UI;
using SpellBook.Testing;
using SpellBook.UI.Editor;
using SpellBook.UI.UGUI.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>Inspector do <see cref="TooltipTrigger"/> e do <see cref="TooltipBinder"/>: resumo, prévia por nível e avisos de cena (TT6).</summary>
    public class TooltipTriggerEditorTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<UnityEditor.Editor> _editors = new List<UnityEditor.Editor>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEditor.Editor editor in _editors.Where(e => e != null)) Object.DestroyImmediate(editor);
            _editors.Clear();
            foreach (Object item in _created.Where(o => o != null)) Object.DestroyImmediate(item);
            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void TriggerEditor_PickerResolverModeDelay_Summary()
        {
            TooltipTrigger trigger = Trigger(new LevelSource(), TooltipOpenMode.Pointer, 0.3f);

            VisualElement root = Inspect(trigger, out UnityEditor.Editor editor);

            Assert.That(editor, Is.InstanceOf<TooltipTriggerEditor>());
            List<string> fields = root.Query<PropertyField>().ToList().Select(f => f.bindingPath).ToList();
            Assert.That(fields, Is.SupersetOf(new[] { "source", "owner", "mode", "delay" }));
            Assert.That(Summary(root), Is.EqualTo($"Tooltip · Level Source · ponteiro, {Seconds(0.3f)} s"));

            SetMode(editor, TooltipOpenMode.Code, 1.5f);
            Assert.That(Summary(root), Is.EqualTo($"Tooltip · Level Source · código, {Seconds(1.5f)} s"), "mudar modo e atraso refaz o resumo");

            SetMode(editor, TooltipOpenMode.Focus, 0f);
            Assert.That(Summary(root), Is.EqualTo("Tooltip · Level Source · foco, 0 s"));

            trigger.SetSource(null);
            SetMode(editor, TooltipOpenMode.PointerAndFocus, 0.3f);
            Assert.That(Summary(root), Is.EqualTo($"Tooltip · (nenhum) · ponteiro e foco, {Seconds(0.3f)} s"));
        }

        [Test]
        public void SummaryLine_AbilityEffectAttributeSources_NamePrefix()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.name = "Fireball";
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.name = "Burn";
            _created.Add(effect);
            AttributeDefinition attribute = _gas.CreateAttribute("Strength");
            TooltipTrigger trigger = Trigger(new AbilityTooltipSource { Ability = ability }, TooltipOpenMode.Pointer, 0.3f);
            VisualElement root = Inspect(trigger, out UnityEditor.Editor editor);

            Assert.That(Summary(root), Is.EqualTo($"Tooltip · Habilidade Fireball · ponteiro, {Seconds(0.3f)} s"));

            trigger.SetSource(new EffectTooltipSource { Effect = effect });
            ((TooltipTriggerEditor)editor).Refresh();
            Assert.That(Summary(root), Does.StartWith("Tooltip · Efeito Burn · "));

            trigger.SetSource(new AttributeTooltipSource { Attribute = attribute });
            ((TooltipTriggerEditor)editor).Refresh();
            Assert.That(Summary(root), Does.StartWith("Tooltip · Atributo Strength · "));
        }

        [Test]
        public void Preview_LevelControl_ChangesLines()
        {
            TooltipTrigger trigger = Trigger(new LevelSource(), TooltipOpenMode.Pointer, 0.3f);
            VisualElement root = Inspect(trigger, out UnityEditor.Editor editor);
            var triggerEditor = (TooltipTriggerEditor)editor;

            Assert.That(root.Q<IntegerField>(TooltipTriggerEditor.PreviewLevelName), Is.Not.Null, "falta o controle de nível");
            Assert.That(PreviewTexts(root), Is.EqualTo(new[] { "Nível 1", "Sub", "Dano 10", "Pilhas: 2", "5 s", "Rodapé" }));

            triggerEditor.SetPreviewLevel(3);

            Assert.That(triggerEditor.PreviewLevel, Is.EqualTo(3));
            Assert.That(root.Q<IntegerField>(TooltipTriggerEditor.PreviewLevelName).value, Is.EqualTo(3));
            Assert.That(PreviewTexts(root), Is.EqualTo(new[] { "Nível 3", "Sub", "Dano 30", "Pilhas: 2", "5 s", "Rodapé" }));
            Label line = root.Q(TooltipTriggerEditor.PreviewName).Query<Label>(className: TooltipTriggerEditor.PreviewLineClass).First();
            Assert.That(line.text, Is.EqualTo("Dano 30"));
            Assert.That(line.style.color.value, Is.EqualTo(TextRoleTheme.Default.GetColor(TextRole.Harmful)), "a cor vem do papel");
            Assert.That(line.enableRichText, Is.False);

            triggerEditor.SetPreviewLevel(0);
            Assert.That(triggerEditor.PreviewLevel, Is.EqualTo(1), "o nível mínimo é 1");
            Assert.That(PreviewTexts(root)[0], Is.EqualTo("Nível 1"));

            trigger.SetSource(null);
            triggerEditor.Refresh();
            Assert.That(PreviewTexts(root), Is.EqualTo(new[] { TooltipTriggerEditor.NoPreviewText }));
        }

        [Test]
        public void PointerMode_NoEventSystemInScene_Warns()
        {
            Assume.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None), Is.Empty, "a cena de teste já tem um EventSystem");
            TooltipTrigger trigger = Trigger(new LevelSource(), TooltipOpenMode.Pointer, 0.3f);
            VisualElement root = Inspect(trigger, out UnityEditor.Editor editor);
            var triggerEditor = (TooltipTriggerEditor)editor;

            Assert.That(IsShown(root, TooltipTriggerEditor.SceneWarningName), Is.True, "modo ponteiro sem EventSystem avisa");
            Assert.That(root.Q<HelpBox>(TooltipTriggerEditor.SceneWarningName).text, Does.Contain("EventSystem"));

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            _created.Add(eventSystem);
            triggerEditor.Refresh();
            Assert.That(IsShown(root, TooltipTriggerEditor.SceneWarningName), Is.False, "com EventSystem, não avisa");

            Object.DestroyImmediate(eventSystem);
            SetMode(editor, TooltipOpenMode.Code, 0.3f);
            Assert.That(IsShown(root, TooltipTriggerEditor.SceneWarningName), Is.False, "o modo código não usa o EventSystem");

            SetMode(editor, TooltipOpenMode.PointerAndFocus, 0.3f);
            Assert.That(IsShown(root, TooltipTriggerEditor.SceneWarningName), Is.True, "ponteiro e foco também usa o ponteiro");
        }

        [Test]
        public void SecondBinderInScene_Warns()
        {
            Assume.That(Object.FindObjectsByType<TooltipBinder>(FindObjectsSortMode.None), Is.Empty, "a cena de teste já tem um TooltipBinder");
            TooltipBinder first = Binder("Tooltip A");
            VisualElement root = Inspect(first, out UnityEditor.Editor editor);

            Assert.That(editor, Is.InstanceOf<TooltipBinderEditor>());
            Assert.That(IsShown(root, TooltipBinderEditor.SceneWarningName), Is.False, "um binder só não avisa");

            TooltipBinder second = Binder("Tooltip B");
            ((TooltipBinderEditor)editor).Refresh();
            Assert.That(IsShown(root, TooltipBinderEditor.SceneWarningName), Is.True, "dois binders avisam");
            Assert.That(root.Q<HelpBox>(TooltipBinderEditor.SceneWarningName).text, Does.Contain("TooltipBinder"));

            second.gameObject.SetActive(false);
            ((TooltipBinderEditor)editor).Refresh();
            Assert.That(IsShown(root, TooltipBinderEditor.SceneWarningName), Is.False, "um binder inativo não conta");
        }

        private TooltipTrigger Trigger(UISource<TooltipModel> source, TooltipOpenMode mode, float delay)
        {
            var go = new GameObject("Item", typeof(RectTransform));
            _created.Add(go);
            var trigger = go.AddComponent<TooltipTrigger>();
            trigger.SetSource(source);
            trigger.SetMode(mode);
            trigger.SetDelay(delay);
            return trigger;
        }

        private TooltipBinder Binder(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go.AddComponent<TooltipBinder>();
        }

        private VisualElement Inspect(Object target, out UnityEditor.Editor editor)
        {
            editor = UnityEditor.Editor.CreateEditor(target);
            _editors.Add(editor);
            return editor.CreateInspectorGUI();
        }

        private static void SetMode(UnityEditor.Editor editor, TooltipOpenMode mode, float delay)
        {
            SerializedObject serialized = editor.serializedObject;
            serialized.Update();
            serialized.FindProperty("mode").enumValueIndex = (int)mode;
            serialized.FindProperty("delay").floatValue = delay;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ((TooltipTriggerEditor)editor).Refresh();
        }

        private static string Summary(VisualElement root)
        {
            Label summary = root.Q<Label>(TooltipTriggerEditor.SummaryName);
            Assert.That(summary, Is.Not.Null, "falta o resumo");
            return summary.text;
        }

        private static string Seconds(float value)
        {
            return ((double)value).ToString("0.##", CultureInfo.CurrentCulture);
        }

        private static string[] PreviewTexts(VisualElement root)
        {
            VisualElement preview = root.Q(TooltipTriggerEditor.PreviewName);
            Assert.That(preview, Is.Not.Null, "falta a prévia");
            return preview.Query<Label>().ToList()
                .Where(label => label.style.display != DisplayStyle.None)
                .Select(label => label.text)
                .ToArray();
        }

        private static bool IsShown(VisualElement root, string name)
        {
            VisualElement element = root.Q(name);
            Assert.That(element, Is.Not.Null, $"falta o elemento {name}");
            return element.style.display != DisplayStyle.None;
        }

        /// <summary>Fonte de teste cuja prévia muda com o nível: "Nível N" e uma linha "Dano 10×N" de papel prejudicial.</summary>
        [Serializable]
        public sealed class LevelSource : UISource<TooltipModel>
        {
            [NonSerialized] private TextBuffer _buffer;
            [NonSerialized] private TooltipLine[] _lines;

            public override Type OwnerType => typeof(GameObject);

            public override UIBinding<TooltipModel> CreateBinding() => throw new NotSupportedException("só a prévia");

            public override TooltipModel CreatePreview(in UIPreviewContext context)
            {
                _buffer ??= new TextBuffer();
                _lines ??= new TooltipLine[1];
                _buffer.Clear();
                TextSpan damage = _buffer.Append((context.Level * 10).ToString(CultureInfo.InvariantCulture));
                TextSpan remaining = _buffer.Append("5 s");
                _lines[0] = new TooltipLine("Dano", damage, TextRole.Harmful, TooltipLineKind.Description);
                return new TooltipModel($"Nível {context.Level}", null, _lines, "Sub", "Rodapé")
                    .WithStacks(2)
                    .WithRemaining(5f, remaining);
            }
        }
    }
}
