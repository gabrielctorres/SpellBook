using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using RecordingText = SpellBook.UI.UGUI.Tests.WidgetViewsTests.RecordingText;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// View do ícone de efeito (Spec do B-018, BF4; R9 do PRD), num Canvas do teste: moldura pelo papel, radial, segundos,
    /// pilhas, sinal de fim pelo <see cref="Animator"/>, infinito sem relógio, ícone padrão (CA17) e assunto no item.
    /// </summary>
    public class EffectIconViewTests
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
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void IconView_FrameByRole_RadialSecondsStacks_EndingSoon_Infinite()
        {
            Rig rig = CreateRig();
            ScriptableObject burn = CreateSubject();
            int secondsSets = rig.Seconds.StringSets;
            int stacksSets = rig.Stacks.StringSets;

            rig.View.Render(Model(TextRole.Beneficial, 3, true, 0.4f, Write("4 s"), false, false, burn));
            Assert.That(rig.Frame.color, Is.EqualTo(Color.green), "a moldura tem a cor do papel benéfico no tema");
            Assert.That(rig.Fill.fillAmount, Is.EqualTo(0.4f));
            Assert.That(rig.Seconds.text, Is.EqualTo("4 s"));
            Assert.That(rig.Stacks.text, Is.EqualTo("3"));
            Assert.That(rig.Seconds.StringSets, Is.EqualTo(secondsSets), "os segundos foram entregues como string");
            Assert.That(rig.Stacks.StringSets, Is.EqualTo(stacksSets), "as pilhas foram entregues como string");
            Assert.That(rig.Animator.GetBool("EndingSoon"), Is.False, "longe do fim, não pisca");

            rig.View.Render(Model(TextRole.Harmful, 1, false, 0.1f, Write("1 s"), false, true, burn));
            Assert.That(rig.Frame.color, Is.EqualTo(Color.red), "a moldura tem a cor do papel nocivo no tema");
            Assert.That(rig.Fill.fillAmount, Is.EqualTo(0.1f));
            Assert.That(rig.Seconds.text, Is.EqualTo("1 s"));
            Assert.That(rig.Stacks.text, Is.Empty, "sem ShowStacks, as pilhas somem");
            Assert.That(rig.Animator.GetBool("EndingSoon"), Is.True, "perto do fim, pisca");

            rig.View.Render(Model(TextRole.Neutral, 2, true, 0.5f, Write("9 s"), true, true, burn));
            Assert.That(rig.Frame.color, Is.EqualTo(Color.gray), "a moldura tem a cor do papel neutro no tema");
            Assert.That(rig.Fill.fillAmount, Is.Zero, "o infinito não tem radial");
            Assert.That(rig.Seconds.text, Is.Empty, "o infinito não tem segundos");
            Assert.That(rig.Stacks.text, Is.EqualTo("2"), "o infinito mostra as pilhas");
            Assert.That(rig.Animator.GetBool("EndingSoon"), Is.False, "o infinito nunca pisca");
        }

        [Test]
        public void NullIcon_UsesDefaultIcon()
        {
            Rig rig = CreateRig();
            Sprite own = CreateSprite();
            Sprite fallback = CreateSprite();
            SetFields(rig.View, ("defaultIcon", fallback));

            rig.View.Render(Model(TextRole.Neutral, 1, false, 0f, default, true, false, CreateSubject(), own));
            Sprite shownWithIcon = rig.Icon.sprite;
            rig.View.Render(Model(TextRole.Neutral, 1, false, 0f, default, true, false, CreateSubject()));
            Sprite shownWithoutIcon = rig.Icon.sprite;
            bool enabledWithDefault = rig.Icon.enabled;
            SetFields(rig.View, ("defaultIcon", null));
            rig.View.Render(Model(TextRole.Neutral, 1, false, 0f, default, true, false, CreateSubject()));

            Assert.That(shownWithIcon, Is.SameAs(own), "o ícone do efeito vem primeiro");
            Assert.That(shownWithoutIcon, Is.SameAs(fallback), "sem ícone, o padrão da view (CA17)");
            Assert.That(enabledWithDefault, Is.True);
            Assert.That(rig.Icon.enabled, Is.False, "sem ícone e sem padrão, a imagem desliga");
        }

        [Test]
        public void WritesSubjectToItemContext()
        {
            Rig rig = CreateRig();
            UIItemContext context = rig.View.gameObject.AddComponent<UIItemContext>();
            SetFields(rig.View, ("itemContext", context));
            ScriptableObject burn = CreateSubject();
            ScriptableObject poison = CreateSubject();
            int changes = 0;
            context.Changed += () => changes++;

            rig.View.Render(Model(TextRole.Harmful, 1, false, 0.5f, Write("2 s"), false, false, burn));
            ScriptableObject written = context.Subject;
            rig.View.Render(Model(TextRole.Harmful, 2, true, 0.4f, Write("1 s"), false, true, burn));
            int changesAfterSameSubject = changes;
            rig.View.Render(Model(TextRole.Harmful, 1, false, 0.5f, Write("2 s"), false, false, poison));

            Assert.That(written, Is.SameAs(burn));
            Assert.That(changesAfterSameSubject, Is.EqualTo(1), "o mesmo assunto não avisa de novo");
            Assert.That(context.Subject, Is.SameAs(poison), "o item reaproveitado recebe o efeito novo");
        }

        private static EffectIconModel Model(TextRole role, int stacks, bool showStacks, float fraction, TextSpan text,
            bool infinite, bool endingSoon, ScriptableObject subject, Sprite icon = null) =>
            new EffectIconModel(icon, "Queimadura", role, stacks, showStacks, fraction, fraction * 10f, text, infinite,
                endingSoon, subject);

        private Rig CreateRig()
        {
            EffectIconView view = CreateUI<EffectIconView>("icone");
            var rig = new Rig
            {
                View = view,
                Frame = CreateChild<Image>(view.gameObject, "frame"),
                Icon = CreateChild<Image>(view.gameObject, "icon"),
                Fill = CreateChild<Image>(view.gameObject, "duration"),
                Seconds = CreateChild<RecordingText>(view.gameObject, "seconds"),
                Stacks = CreateChild<RecordingText>(view.gameObject, "stacks"),
                Animator = AddAnimator(view.gameObject),
            };
            SetFields(view, ("frame", rig.Frame), ("icon", rig.Icon), ("durationFill", rig.Fill), ("secondsText", rig.Seconds),
                ("stacksText", rig.Stacks), ("animator", rig.Animator),
                ("theme", CreateTheme((TextRole.Beneficial, Color.green), (TextRole.Harmful, Color.red), (TextRole.Neutral, Color.gray))));
            return rig;
        }

        // Animator iniciado em edição, com o bool e sem transição: o valor fica como a view deixou
        private Animator AddAnimator(GameObject target)
        {
            var controller = new AnimatorController();
            _created.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("EndingSoon", AnimatorControllerParameterType.Bool);
            Animator animator = target.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        private UITheme CreateTheme(params (TextRole Role, Color Color)[] colors)
        {
            var roles = ScriptableObject.CreateInstance<TextRoleTheme>();
            var theme = ScriptableObject.CreateInstance<UITheme>();
            _created.Add(roles);
            _created.Add(theme);
            var serializedRoles = new SerializedObject(roles);
            foreach ((TextRole role, Color color) in colors)
            {
                serializedRoles.FindProperty(role.ToString().ToLowerInvariant()).colorValue = color;
            }

            serializedRoles.ApplyModifiedPropertiesWithoutUndo();
            SetFields(theme, ("roles", roles));
            return theme;
        }

        private T CreateUI<T>(string name) where T : Component
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(_canvas.transform);
            return gameObject.AddComponent<T>();
        }

        private static T CreateChild<T>(GameObject parent, string name) where T : Component
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform);
            return gameObject.AddComponent<T>();
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            _created.Add(sprite);
            _created.Add(texture);
            return sprite;
        }

        private ScriptableObject CreateSubject()
        {
            var subject = ScriptableObject.CreateInstance<UITheme>();
            _created.Add(subject);
            return subject;
        }

        /// <summary>Escreve o texto depois de outro, para o trecho não começar no início do buffer.</summary>
        private static TextSpan Write(string text)
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

        private sealed class Rig
        {
            public EffectIconView View;
            public Image Frame;
            public Image Icon;
            public Image Fill;
            public RecordingText Seconds;
            public RecordingText Stacks;
            public Animator Animator;
        }
    }
}
