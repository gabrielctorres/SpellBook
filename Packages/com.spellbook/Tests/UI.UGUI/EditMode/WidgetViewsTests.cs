using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>Views uGUI da barra, do texto e do indicador de tag (R11; ADR 0039, itens 3, 4 e 8), num Canvas do teste.</summary>
    public class WidgetViewsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvas = new GameObject("canvas", typeof(Canvas));
            _created.Add(_canvas);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void BarView_FillTrailTextColorByRole()
        {
            Image fill = CreateUI<Image>("fill");
            Image trail = CreateUI<Image>("trail");
            RecordingText label = CreateUI<RecordingText>("label");
            label.color = Color.white;
            UITheme theme = CreateTheme(TextRole.Harmful, Color.blue);
            BarView view = CreateUI<BarView>("bar");
            SetFields(view, ("fill", fill), ("trail", trail), ("label", label), ("theme", theme));
            TextSpan text = WriteAfterOtherText("50 / 100");
            int setsBefore = label.StringSets;

            view.Render(new BarModel(50f, 100f, text, TextRole.Harmful).WithTrail(0.8f));

            Assert.That(fill.fillAmount, Is.EqualTo(0.5f));
            Assert.That(trail.fillAmount, Is.EqualTo(0.8f));
            Assert.That(label.text, Is.EqualTo("50 / 100"));
            Assert.That(label.StringSets, Is.EqualTo(setsBefore), "a barra entregou string ao TextMeshPro em vez do trecho");
            Assert.That(fill.color, Is.EqualTo(Color.blue), "o preenchimento tem a cor do papel no tema");
            Assert.That(label.color, Is.EqualTo(Color.white), "o texto da barra mantém a própria cor");
        }

        [Test]
        public void BarView_Empty_ZeroFillAndEmptyText()
        {
            Image fill = CreateUI<Image>("fill");
            TextMeshProUGUI label = CreateUI<TextMeshProUGUI>("label");
            BarView view = CreateUI<BarView>("bar");
            SetFields(view, ("fill", fill), ("label", label));
            view.Render(new BarModel(50f, 100f, WriteAfterOtherText("50 / 100"), TextRole.Harmful));

            view.Render(default);

            Assert.That(fill.fillAmount, Is.Zero);
            Assert.That(label.text, Is.Empty);
            Assert.That(fill.color, Is.EqualTo(TextRoleTheme.Default.GetColor(TextRole.Harmful)),
                "sem tema, a cor vem do tema padrão e o vazio não a muda");
        }

        [Test]
        public void TextView_WritesSpan_NoStringPerRender()
        {
            RecordingText text = CreateUI<RecordingText>("text");
            UITheme theme = CreateTheme(TextRole.Value, Color.cyan);
            TextView view = CreateUI<TextView>("view");
            SetFields(view, ("text", text), ("theme", theme));
            int setsBefore = text.StringSets;

            view.Render(new TextModel(WriteAfterOtherText("123"), TextRole.Value));
            view.Render(new TextModel(WriteAfterOtherText("456"), TextRole.Value));

            Assert.That(text.text, Is.EqualTo("456"));
            Assert.That(text.color, Is.EqualTo(Color.cyan));
            Assert.That(text.StringSets, Is.EqualTo(setsBefore), "a view entregou string ao TextMeshPro em vez do trecho");
        }

        [Test]
        public void TagIndicatorView_ShowsHides()
        {
            var target = new GameObject("icon");
            target.transform.SetParent(_canvas.transform);
            TagIndicatorView view = CreateUI<TagIndicatorView>("indicator");
            SetFields(view, ("target", target));

            view.Render(new TagIndicatorModel(true));
            bool shownWhenActive = target.activeSelf;
            view.Render(new TagIndicatorModel(false));
            bool shownWhenInactive = target.activeSelf;
            view.Render(new TagIndicatorModel(true));
            view.Render(default);

            Assert.That(shownWhenActive, Is.True);
            Assert.That(shownWhenInactive, Is.False);
            Assert.That(target.activeSelf, Is.False, "o estado vazio esconde o indicador");
        }

        private T CreateUI<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(_canvas.transform);
            return gameObject.AddComponent<T>();
        }

        private UITheme CreateTheme(TextRole role, Color color)
        {
            var roles = ScriptableObject.CreateInstance<TextRoleTheme>();
            var theme = ScriptableObject.CreateInstance<UITheme>();
            _created.Add(roles);
            _created.Add(theme);
            var serializedRoles = new SerializedObject(roles);
            serializedRoles.FindProperty(role.ToString().ToLowerInvariant()).colorValue = color;
            serializedRoles.ApplyModifiedPropertiesWithoutUndo();
            SetFields(theme, ("roles", roles));
            return theme;
        }

        /// <summary>Escreve o texto depois de outro, para o trecho não começar no início do buffer.</summary>
        private static TextSpan WriteAfterOtherText(string text)
        {
            var buffer = new TextBuffer();
            buffer.Append("outro texto");
            return buffer.Append(text);
        }

        private static void SetFields(Object target, params (string Field, Object Value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach ((string field, Object value) in fields)
            {
                serialized.FindProperty(field).objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Texto do TextMeshPro que conta as <see cref="string"/> recebidas pelo setter de <c>text</c>. O caminho por
        /// <c>SetCharArray</c> não passa por ele. No Editor o próprio TextMeshPro cria uma <c>string</c> por
        /// <c>SetCharArray</c>, então contar alocações não separa a view do TextMeshPro.
        /// </summary>
        public sealed class RecordingText : TextMeshProUGUI
        {
            public int StringSets;

            public override string text
            {
                get => base.text;
                set
                {
                    StringSets++;
                    base.text = value;
                }
            }
        }
    }
}
