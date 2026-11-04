using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.Testing;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>View uGUI e prefab do tooltip (Spec UI-3, Design 5; CA8 e CA9, partes da view), num Canvas do teste.</summary>
    public class TooltipViewTests
    {
        private const string PrefabPath = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/Tooltip.prefab";

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
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void View_TitleIconLinesFooter_ColorByRole_NoMarkupFromModel()
        {
            Rig rig = CreateRig();
            UITheme theme = CreateTheme(TextRole.Harmful, Color.blue);
            SetFields(rig.View, ("theme", theme));
            Sprite sprite = CreateSprite();
            var buffer = new TextBuffer();
            buffer.Append("outro texto");
            var lines = new[]
            {
                new TooltipLine("Dano", buffer.Append("<i>30</i>"), TextRole.Harmful, TooltipLineKind.Modifier),
                new TooltipLine(null, buffer.Append("Causa dano"), TextRole.Value, TooltipLineKind.Description),
            };

            rig.View.Render(new TooltipModel("<b>Bola</b> de fogo", sprite, lines, footer: "Rodapé"));

            Assert.That(rig.Panel.gameObject.activeSelf, Is.True);
            Assert.That(rig.Title.text, Is.EqualTo("<b>Bola</b> de fogo"));
            Assert.That(rig.Icon.sprite, Is.SameAs(sprite));
            Assert.That(rig.Icon.enabled, Is.True);
            Assert.That(rig.Subtitle.gameObject.activeSelf, Is.False, "sem subtítulo, o texto some");
            Assert.That(rig.Footer.text, Is.EqualTo("Rodapé"));
            Assert.That(rig.Footer.gameObject.activeSelf, Is.True);
            List<TooltipLineView> rows = ActiveRows(rig);
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].Label.text, Is.EqualTo("Dano"));
            Assert.That(rows[0].Value.text, Is.EqualTo("<i>30</i>"));
            Assert.That(rows[0].Value.color, Is.EqualTo(Color.blue), "o valor tem a cor do papel no tema");
            Assert.That(rows[0].Label.color, Is.EqualTo(Color.white), "o rótulo mantém a própria cor");
            Assert.That(rows[1].Label.gameObject.activeSelf, Is.False, "linha sem rótulo esconde o rótulo");
            Assert.That(rows[1].Value.text, Is.EqualTo("Causa dano"));
            Assert.That(rows[1].Value.color, Is.EqualTo(theme.GetColor(TextRole.Value)));
            Assert.That(rows.Select(r => ((RecordingText)r.Value).StringSets), Is.All.Zero,
                "a view entregou string ao TextMeshPro em vez do trecho");
            Assert.That(new TMP_Text[] { rig.Title, rig.Footer, rows[0].Label, rows[0].Value, rows[1].Value }
                .Where(t => t.richText), Is.Empty, "o markup do modelo é interpretado como rich text");
            Assert.That(rig.Template.gameObject.activeSelf, Is.False, "a linha-modelo continua desligada");
        }

        [Test]
        public void View_StacksAndRemaining_OnlyWhenPresent()
        {
            Rig rig = CreateRig();
            var buffer = new TextBuffer();
            buffer.Append("outro texto");
            var plain = new TooltipModel("Veneno", null, null);

            rig.View.Render(plain);
            bool stacksShownWithout = rig.Stacks.gameObject.activeSelf;
            bool remainingShownWithout = rig.Remaining.gameObject.activeSelf;
            rig.View.Render(plain.WithStacks(3).WithRemaining(4.5f, buffer.Append("4,5 s")));
            bool stacksShown = rig.Stacks.gameObject.activeSelf;
            bool remainingShown = rig.Remaining.gameObject.activeSelf;
            string stacksText = rig.Stacks.text;
            string remainingText = rig.Remaining.text;
            rig.View.Render(plain);

            Assert.That(stacksShownWithout, Is.False);
            Assert.That(remainingShownWithout, Is.False);
            Assert.That(stacksShown, Is.True);
            Assert.That(remainingShown, Is.True);
            Assert.That(stacksText, Is.EqualTo("3"));
            Assert.That(remainingText, Is.EqualTo("4,5 s"));
            Assert.That(rig.Stacks.gameObject.activeSelf, Is.False, "o modelo seguinte sem pilhas esconde as pilhas");
            Assert.That(rig.Remaining.gameObject.activeSelf, Is.False, "o modelo seguinte sem tempo esconde o tempo");
            Assert.That(rig.Icon.enabled, Is.False, "sem ícone, a imagem fica desligada");
        }

        [Test]
        public void View_Lines_SpawnedBySpawner_ReuseRows_HideExtra_EmptyHidesPanel()
        {
            Rig rig = CreateRig();
            var spawner = ScriptableObject.CreateInstance<RecordingSpawnerAsset>();
            _created.Add(spawner);
            SetFields(rig.View, ("spawner", spawner));
            TooltipLine[] three = Lines("a", "b", "c");

            rig.View.Render(new TooltipModel("t", null, three));
            List<TooltipLineView> first = ActiveRows(rig);
            int spawnedFirst = spawner.SpawnCount;
            List<Transform> firstParents = first.Select(r => r.transform.parent).ToList();
            rig.View.Render(new TooltipModel("t", null, Lines("x")));
            List<TooltipLineView> afterOne = ActiveRows(rig);
            string textAfterOne = afterOne[0].Value.text;
            int childrenAfterOne = rig.Template.transform.parent.childCount;
            rig.View.Render(new TooltipModel("t", null, three));
            List<TooltipLineView> again = ActiveRows(rig);
            int spawnedAgain = spawner.SpawnCount;
            Object.DestroyImmediate(first[1].gameObject);
            rig.View.Render(new TooltipModel("t", null, three));
            List<TooltipLineView> rebuilt = ActiveRows(rig);
            rig.View.Render(default);

            Assert.That(first, Has.Count.EqualTo(3));
            Assert.That(spawnedFirst, Is.EqualTo(3), "cada linha nasce pelo spawner configurado (ADR 0015)");
            Assert.That(firstParents, Is.All.SameAs(rig.Template.transform.parent),
                "o clone fica junto da linha-modelo");
            Assert.That(afterOne, Is.EqualTo(new[] { first[0] }), "a primeira linha é reaproveitada e as outras somem");
            Assert.That(textAfterOne, Is.EqualTo("x"));
            Assert.That(childrenAfterOne, Is.EqualTo(4), "menos linhas não destroem nem criam clones");
            Assert.That(again, Is.EqualTo(first), "as mesmas três linhas voltam");
            Assert.That(spawnedAgain, Is.EqualTo(3), "linhas guardadas são reaproveitadas, sem novo spawn");
            Assert.That(rebuilt, Has.Count.EqualTo(3), "a linha destruída é recriada");
            Assert.That(spawner.SpawnCount, Is.EqualTo(4), "só a linha destruída, que conta como nula, nasce de novo");
            Assert.That(rebuilt[1].Value.text, Is.EqualTo("b"));
            Assert.That(rebuilt.Select(r => r.Value.text), Is.EqualTo(new[] { "a", "b", "c" }),
                "a linha recriada volta na mesma posição");
            Assert.That(rig.Panel.gameObject.activeSelf, Is.False, "o modelo vazio esconde o painel");
        }

        [Test]
        public void View_Place_PutsPanelByPlacement_DestroyedTargetDoesNothing()
        {
            Rig rig = CreateRig();
            var root = (RectTransform)rig.View.transform;
            root.sizeDelta = new Vector2(1000f, 600f);
            rig.Panel.pivot = new Vector2(0f, 1f);
            rig.Panel.sizeDelta = new Vector2(200f, 100f);
            RectTransform target = CreateChild<RectTransform>(rig.View.gameObject, "target");
            target.pivot = Vector2.zero;
            target.sizeDelta = new Vector2(50f, 50f);
            target.localPosition = new Vector3(350f, 0f, 0f);

            // À direita não cabe (408 + 200 > 500): inverte para a esquerda, a 8 do alvo, com o topo no topo do alvo
            rig.View.Place(target);
            Vector3 placed = rig.Panel.localPosition;
            Object.DestroyImmediate(target.gameObject);
            rig.View.Place(target);
            rig.View.Place(null);

            Assert.That(placed.x, Is.EqualTo(142f).Within(0.01f));
            Assert.That(placed.y, Is.EqualTo(50f).Within(0.01f));
            Assert.That(rig.Panel.localPosition, Is.EqualTo(placed), "alvo destruído ou nulo não move o painel");
        }

        [Test]
        public void TooltipPrefab_BinderViewRegistrar_Wired()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"o prefab não existe em {PrefabPath}");

            var binder = prefab.GetComponent<TooltipBinder>();
            var view = prefab.GetComponent<TooltipView>();
            Assert.That(binder, Is.Not.Null, "Tooltip sem TooltipBinder na raiz");
            Assert.That(view, Is.Not.Null, "Tooltip sem TooltipView na raiz");
            Assert.That(binder.View, Is.SameAs(view), "o binder não aponta para a view do prefab");
            Assert.That(prefab.GetComponent<AnchorRegistrar>(), Is.Not.Null, "Tooltip sem AnchorRegistrar");
            foreach (string part in new[] { "panel", "title", "subtitle", "icon", "lineTemplate", "stacks", "remaining", "footer" })
            {
                Assert.That(Field<Object>(view, part), Is.Not.Null, $"Tooltip sem {part}");
            }

            var panel = Field<RectTransform>(view, "panel");
            Assert.That(panel.GetComponent<VerticalLayoutGroup>(), Is.Not.Null, "o painel sem layout vertical");
            Assert.That(panel.GetComponent<ContentSizeFitter>(), Is.Not.Null, "o painel não se ajusta ao conteúdo");
            var template = Field<TooltipLineView>(view, "lineTemplate");
            Assert.That(template.gameObject.activeSelf, Is.False, "a linha-modelo fica desligada");
            Assert.That(template.Label, Is.Not.Null, "a linha-modelo sem rótulo");
            Assert.That(template.Value, Is.Not.Null, "a linha-modelo sem valor");
            Assert.That(prefab.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget), Is.Empty,
                "o tooltip é só exibição: nenhum gráfico recebe clique");
            Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => new SerializedObject(t).FindProperty("m_fontAsset").objectReferenceValue != null),
                Is.Empty, "os textos caem na fonte padrão do TMP_Settings (ADR 0039)");
        }

        private Rig CreateRig()
        {
            var viewObject = new GameObject("tooltip", typeof(RectTransform));
            viewObject.transform.SetParent(_canvas.transform, false);
            var rig = new Rig { View = viewObject.AddComponent<TooltipView>() };
            rig.Panel = CreateChild<RectTransform>(viewObject, "panel");
            GameObject panel = rig.Panel.gameObject;
            rig.Title = CreateChild<TextMeshProUGUI>(panel, "title");
            rig.Subtitle = CreateChild<TextMeshProUGUI>(panel, "subtitle");
            rig.Icon = CreateChild<Image>(panel, "icon");
            rig.Stacks = CreateChild<TextMeshProUGUI>(panel, "stacks");
            rig.Remaining = CreateChild<TextMeshProUGUI>(panel, "remaining");
            rig.Footer = CreateChild<TextMeshProUGUI>(panel, "footer");
            RectTransform lines = CreateChild<RectTransform>(panel, "lines");
            rig.Template = CreateChild<TooltipLineView>(lines.gameObject, "line");
            RecordingText label = CreateChild<RecordingText>(rig.Template.gameObject, "label");
            label.color = Color.white;
            RecordingText value = CreateChild<RecordingText>(rig.Template.gameObject, "value");
            SetFields(rig.Template, ("label", label), ("value", value));
            rig.Template.gameObject.SetActive(false);
            SetFields(rig.View, ("panel", rig.Panel), ("title", rig.Title), ("subtitle", rig.Subtitle), ("icon", rig.Icon),
                ("lineTemplate", rig.Template), ("stacks", rig.Stacks), ("remaining", rig.Remaining), ("footer", rig.Footer));
            return rig;
        }

        private static List<TooltipLineView> ActiveRows(Rig rig) =>
            rig.Template.transform.parent.GetComponentsInChildren<TooltipLineView>(false).ToList();

        private static TooltipLine[] Lines(params string[] values)
        {
            var buffer = new TextBuffer();
            return values.Select(v => new TooltipLine("r", buffer.Append(v), TextRole.Value, TooltipLineKind.Value)).ToArray();
        }

        private static T CreateChild<T>(GameObject parent, string name) where T : Component
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform, false);
            return gameObject.TryGetComponent(out T existing) ? existing : gameObject.AddComponent<T>();
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

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            _created.Add(texture);
            _created.Add(sprite);
            return sprite;
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

        private static T Field<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field)?.objectReferenceValue as T;

        private sealed class Rig
        {
            public TooltipView View;
            public RectTransform Panel;
            public TMP_Text Title;
            public TMP_Text Subtitle;
            public Image Icon;
            public TMP_Text Stacks;
            public TMP_Text Remaining;
            public TMP_Text Footer;
            public TooltipLineView Template;
        }

        /// <summary>Texto do TextMeshPro que conta as <see cref="string"/> recebidas pelo setter de <c>text</c>.</summary>
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
