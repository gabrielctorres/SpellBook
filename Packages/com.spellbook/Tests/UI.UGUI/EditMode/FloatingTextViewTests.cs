using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// View e prefab do número flutuante (R11, R13, R14; ADR 0039, itens 3, 4 e 8): o número nasce no ponto do mundo
    /// convertido para o container, com a cor do papel, sobe pelo estilo e some no fim.
    /// </summary>
    public class FloatingTextViewTests
    {
        private const string PrefabPath = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/FloatingText.prefab";
        private const float Tolerance = 1e-3f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            // Ordem inversa: a câmera sai antes da textura que ela usa
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Show_PlacesAtWorldPoint_ColorByRole()
        {
            Camera camera = CreateCamera(new Vector3(0f, 0f, -10f));
            Canvas canvas = CreateCanvas(RenderMode.ScreenSpaceCamera);
            canvas.worldCamera = camera;
            RectTransform container = CreateContainer(canvas);
            (FloatingTextView view, TextMeshProUGUI text) = CreateView(container, CreateTheme(TextRole.Harmful, Color.blue));
            var world = new Vector3(1f, 0.5f, 3f);

            view.Show(new FloatingTextRequest(world, WriteAfterOtherText("-30"), TextRole.Harmful, null));

            // O ponto fora do plano do canvas cai onde o raio da câmera o cruza, não na projeção ortogonal
            var ray = new Ray(camera.transform.position, world - camera.transform.position);
            var plane = new Plane(container.rotation * Vector3.back, container.position);
            Assert.That(plane.Raycast(ray, out float distance), Is.True, "o plano do container está atrás da câmera");
            Vector3 expected = container.InverseTransformPoint(ray.GetPoint(distance));
            Vector3 naive = container.InverseTransformPoint(world);
            Assert.That(Vector2.Distance(expected, naive), Is.GreaterThan(0.01f), "o teste não separa a projeção da câmera");
            AssertLocal(view, expected);
            Assert.That(text.text, Is.EqualTo("-30"));
            Assert.That(text.color, Is.EqualTo(Color.blue), "a cor é a do papel no tema, com alfa cheio");
        }

        [Test]
        public void Show_WorldSpaceCanvas_PlacesAtWorldPoint()
        {
            Canvas canvas = CreateCanvas(RenderMode.WorldSpace);
            canvas.transform.localScale = Vector3.one * 0.01f;
            canvas.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            RectTransform container = CreateContainer(canvas);
            container.localPosition = new Vector3(20f, -10f, 0f);
            (FloatingTextView view, _) = CreateView(container, null);
            var world = new Vector3(1f, 2f, 0.5f);

            view.Show(new FloatingTextRequest(world, WriteAfterOtherText("+20"), TextRole.Beneficial, null));

            AssertLocal(view, container.InverseTransformPoint(world));
        }

        [Test]
        public void Progress_RisesAndFades()
        {
            (FloatingTextView view, TextMeshProUGUI text) = CreateWorldSpaceView();
            FloatingTextStyle style = CreateStyle(new Vector2(0f, 40f), Vector2.zero);
            view.Show(new FloatingTextRequest(Vector3.zero, WriteAfterOtherText("-30"), TextRole.Harmful, style));
            Vector3 start = view.transform.localPosition;

            view.SetProgress(0f);
            Vector3 atStart = view.transform.localPosition;
            float alphaAtStart = text.color.a;
            view.SetProgress(0.5f);
            Vector3 atHalf = view.transform.localPosition;
            float alphaAtHalf = text.color.a;
            view.SetProgress(1f);

            Assert.That(Vector3.Distance(atStart, start), Is.LessThan(Tolerance));
            Assert.That(alphaAtStart, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Vector3.Distance(atHalf, start + new Vector3(0f, 20f, 0f)), Is.LessThan(Tolerance), "meio da subida");
            Assert.That(alphaAtHalf, Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(Vector3.Distance(view.transform.localPosition, start + new Vector3(0f, 40f, 0f)), Is.LessThan(Tolerance));
            Assert.That(text.color.a, Is.EqualTo(0f).Within(Tolerance), "o número some no fim");
        }

        [Test]
        public void Show_AfterFade_RestoresAlphaAndPosition()
        {
            (FloatingTextView view, TextMeshProUGUI text) = CreateWorldSpaceView();
            FloatingTextStyle style = CreateStyle(new Vector2(0f, 40f), Vector2.zero);
            view.Show(new FloatingTextRequest(Vector3.zero, WriteAfterOtherText("-30"), TextRole.Harmful, style));
            view.SetProgress(1f);
            var second = new Vector3(0.3f, 0.1f, 0f);

            view.Show(new FloatingTextRequest(second, WriteAfterOtherText("+5"), TextRole.Beneficial, style));

            AssertLocal(view, view.transform.parent.InverseTransformPoint(second));
            Assert.That(text.color.a, Is.EqualTo(1f).Within(Tolerance), "a instância reaproveitada do pool volta visível");
            Assert.That(text.text, Is.EqualTo("+5"));
        }

        [Test]
        public void Show_RandomOffset_WithinStyleBounds()
        {
            (FloatingTextView view, _) = CreateWorldSpaceView();
            var bounds = new Vector2(10f, 5f);
            FloatingTextStyle style = CreateStyle(Vector2.zero, bounds);
            Vector3 center = view.transform.parent.InverseTransformPoint(Vector3.zero);
            Random.State saved = Random.state;
            Random.InitState(1234);
            bool moved = false;

            try
            {
                for (int i = 0; i < 20; i++)
                {
                    view.Show(new FloatingTextRequest(Vector3.zero, WriteAfterOtherText("-1"), TextRole.Harmful, style));
                    Vector3 offset = view.transform.localPosition - center;
                    Assert.That(Mathf.Abs(offset.x), Is.LessThanOrEqualTo(bounds.x + Tolerance), "fora do limite em x");
                    Assert.That(Mathf.Abs(offset.y), Is.LessThanOrEqualTo(bounds.y + Tolerance), "fora do limite em y");
                    moved |= ((Vector2)offset).sqrMagnitude > Tolerance;
                }
            }
            finally
            {
                Random.state = saved;
            }

            Assert.That(moved, Is.True, "o deslocamento aleatório não foi aplicado");
        }

        [Test]
        public void FloatingTextPrefab_HasViewAndText_Wired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"o prefab não existe em {PrefabPath}");

            var view = prefab.GetComponent<IFloatingTextView>();
            var text = prefab.GetComponent<TMP_Text>();

            Assert.That(view, Is.InstanceOf<FloatingTextView>(), "o binder lê o IFloatingTextView da raiz do prefab");
            Assert.That(text, Is.Not.Null, "o prefab sem texto");
            Assert.That(new SerializedObject((FloatingTextView)view).FindProperty("text").objectReferenceValue, Is.SameAs(text),
                "a view não aponta para o texto do prefab");
            Assert.That(new SerializedObject(text).FindProperty("m_fontAsset").objectReferenceValue, Is.Null,
                "o texto tem fonte própria; deve cair na fonte padrão do TMP_Settings (ADR 0039)");
            Assert.That(text.raycastTarget, Is.False, "o número não deve bloquear cliques");
            var rect = (RectTransform)prefab.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }

        private (FloatingTextView View, TextMeshProUGUI Text) CreateWorldSpaceView()
        {
            Canvas canvas = CreateCanvas(RenderMode.WorldSpace);
            return CreateView(CreateContainer(canvas), null);
        }

        private Camera CreateCamera(Vector3 position)
        {
            var texture = new RenderTexture(200, 100, 0);
            _created.Add(texture);
            var gameObject = new GameObject("camera", typeof(Camera));
            _created.Add(gameObject);
            // Alvo de tamanho fixo: o pixelRect não depende da tela do batchmode
            gameObject.transform.position = position;
            var camera = gameObject.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.targetTexture = texture;
            return camera;
        }

        private Canvas CreateCanvas(RenderMode mode)
        {
            var gameObject = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            _created.Add(gameObject);
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = mode;
            return canvas;
        }

        private static RectTransform CreateContainer(Canvas canvas)
        {
            var gameObject = new GameObject("container", typeof(RectTransform));
            gameObject.transform.SetParent(canvas.transform, false);
            return (RectTransform)gameObject.transform;
        }

        private static (FloatingTextView View, TextMeshProUGUI Text) CreateView(RectTransform container, UITheme theme)
        {
            var gameObject = new GameObject("number", typeof(RectTransform));
            gameObject.transform.SetParent(container, false);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            var view = gameObject.AddComponent<FloatingTextView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("text").objectReferenceValue = text;
            serialized.FindProperty("theme").objectReferenceValue = theme;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return (view, text);
        }

        private FloatingTextStyle CreateStyle(Vector2 rise, Vector2 randomOffset)
        {
            var style = ScriptableObject.CreateInstance<FloatingTextStyle>();
            _created.Add(style);
            var serialized = new SerializedObject(style);
            serialized.FindProperty("rise").vector2Value = rise;
            serialized.FindProperty("randomOffset").vector2Value = randomOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return style;
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
            var serializedTheme = new SerializedObject(theme);
            serializedTheme.FindProperty("roles").objectReferenceValue = roles;
            serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            return theme;
        }

        private static void AssertLocal(Component view, Vector3 expected)
        {
            Vector3 actual = view.transform.localPosition;
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance), "posição local em x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance), "posição local em y");
        }

        /// <summary>Escreve o texto depois de outro, para o trecho não começar no início do buffer.</summary>
        private static TextSpan WriteAfterOtherText(string text)
        {
            var buffer = new TextBuffer();
            buffer.Append("outro texto");
            return buffer.Append(text);
        }
    }
}
