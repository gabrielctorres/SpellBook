using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using RecordingText = SpellBook.UI.UGUI.Tests.WidgetViewsTests.RecordingText;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// View, prefab e validação da barra de cast (Spec do B-016, SL11; R20, R21 e CA26 do PRD), num Canvas do teste. O
    /// prefab mostra a prévia da fonte em edição (CA26, parte automática).
    /// </summary>
    public class CastBarViewTests
    {
        private const string PrefabPath = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/CastBar.prefab";

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
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Channeling_FillNameIconSeconds()
        {
            Rig rig = CreateRig();
            Sprite sprite = CreateSprite();
            int secondsSetsBefore = rig.Seconds.StringSets;

            rig.View.Render(Channeling(sprite, "Fireball", 0.6f, 1.2f));

            Assert.That(rig.Content.activeSelf, Is.True, "a canalização mostra a barra");
            Assert.That(rig.Fill.fillAmount, Is.EqualTo(0.6f));
            Assert.That(rig.Icon.sprite, Is.SameAs(sprite));
            Assert.That(rig.Icon.enabled, Is.True, "ícone com sprite fica ligado");
            Assert.That(rig.Name.text, Is.EqualTo("Fireball"));
            Assert.That(rig.Seconds.text, Is.EqualTo("1.2"), "uma casa, na cultura invariante sem tema");
            Assert.That(rig.Seconds.StringSets, Is.EqualTo(secondsSetsBefore), "a view entregou string ao TextMeshPro nos segundos");
            Assert.That(rig.Indeterminate.activeSelf, Is.False, "com duração, sem a marca indeterminada");
            Assert.That(rig.Completed.activeSelf || rig.Cancelled.activeSelf, Is.False, "canalizando, sem marca de resultado");

            int nameSets = rig.Name.StringSets;
            rig.View.Render(Channeling(sprite, "Fireball", 0.7f, 0.9f));
            Assert.That(rig.Name.StringSets, Is.EqualTo(nameSets), "o mesmo nome não é entregue de novo a cada tick");

            SetFields(rig.View, ("theme", CreateTheme("pt-BR")));
            rig.View.Render(Channeling(null, "Fireball", 0.6f, 1.2f));
            Assert.That(rig.Seconds.text, Is.EqualTo("1,2"), "a cultura vem do tema da view");
            Assert.That(rig.Icon.enabled, Is.False, "sem sprite, ícone desligado");
        }

        [Test]
        public void Indeterminate_MarkerWithoutSeconds()
        {
            Rig rig = CreateRig();
            rig.View.Render(Channeling(null, "Fireball", 0.6f, 1.2f));

            rig.View.Render(new CastBarModel(CastBarState.Channeling, null, "Beam", 0.5f, 3f, true, CastBarResult.None, 0));

            Assert.That(rig.Indeterminate.activeSelf, Is.True, "sem duração, a marca indeterminada aparece");
            Assert.That(rig.Fill.fillAmount, Is.Zero, "sem duração, sem preenchimento");
            Assert.That(rig.Seconds.text, Is.Empty, "sem duração, sem segundos");
            Assert.That(rig.Name.text, Is.EqualTo("Beam"));

            rig.View.Render(Channeling(null, "Fireball", 0.5f, 1f));
            Assert.That(rig.Indeterminate.activeSelf, Is.False, "com duração, a marca sai");
        }

        [Test]
        public void Idle_HiddenWithoutResult_MarkerByResult()
        {
            Rig rig = CreateRig();
            Sprite sprite = CreateSprite();

            rig.View.Render(default);
            Assert.That(rig.Content.activeSelf, Is.False, "sem canalização e sem resultado, a barra some");

            rig.View.Render(Channeling(sprite, "Fireball", 0.4f, 2f));
            rig.View.Render(Ended(sprite, "Fireball", CastBarResult.Completed, 1));
            Assert.That(rig.Content.activeSelf, Is.True, "concluída, a barra fica à vista com o resultado");
            Assert.That(rig.Completed.activeSelf, Is.True);
            Assert.That(rig.Cancelled.activeSelf, Is.False, "concluída e cancelada são distintas");
            Assert.That(rig.Fill.fillAmount, Is.EqualTo(1f), "concluída enche a barra");
            Assert.That(rig.Seconds.text, Is.Empty, "fora da canalização, sem segundos");
            Assert.That(rig.Name.text, Is.EqualTo("Fireball"), "o nome da que acabou fica");
            Assert.That(rig.Icon.sprite, Is.SameAs(sprite), "o ícone da que acabou fica");

            rig.View.Render(Channeling(sprite, "Heal", 0.3f, 2f, CastBarResult.Completed, 1));
            Assert.That(rig.Completed.activeSelf, Is.False, "uma nova canalização apaga a marca do resultado anterior");
            rig.View.Render(Ended(sprite, "Heal", CastBarResult.Cancelled, 2));
            Assert.That(rig.Cancelled.activeSelf, Is.True);
            Assert.That(rig.Completed.activeSelf, Is.False, "concluída e cancelada são distintas");
            Assert.That(rig.Fill.fillAmount, Is.EqualTo(0.3f), "cancelada deixa o preenchimento onde parou");
            Assert.That(rig.Indeterminate.activeSelf, Is.False);

            rig.View.Render(default);
            Assert.That(rig.Content.activeSelf, Is.False, "o vazio esconde de novo");
        }

        [Test]
        public void ResultTrigger_OncePerEnd_NotOnFirstOrAfterEmpty()
        {
            Rig rig = CreateRig();
            Animator animator = AddAnimator(rig);

            rig.View.Render(Ended(null, "Fireball", CastBarResult.Completed, 3));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "o primeiro desenho não anima");
            Assert.That(animator.GetBool("Channeling"), Is.False);

            rig.View.Render(Channeling(null, "Fireball", 0.5f, 1f, CastBarResult.Completed, 3));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "contador igual não anima");
            Assert.That(animator.GetBool("Channeling"), Is.True, "o bool acompanha a canalização");

            rig.View.Render(Ended(null, "Fireball", CastBarResult.Completed, 4));
            Assert.That(Fired(animator), Is.EqualTo((true, false)), "concluída dispara o gatilho dela uma vez");
            Assert.That(animator.GetBool("Channeling"), Is.False, "o bool desliga no fim");

            rig.View.Render(Ended(null, "Fireball", CastBarResult.Completed, 4));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "um gatilho por fim");

            rig.View.Render(Ended(null, "Fireball", CastBarResult.Cancelled, 5));
            Assert.That(Fired(animator), Is.EqualTo((false, true)), "cancelada dispara o outro gatilho");

            rig.View.Render(default);
            rig.View.Render(Ended(null, "Fireball", CastBarResult.Cancelled, 9));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "depois do vazio (troca de dono), o desenho é o primeiro de novo");
        }

        [Test]
        public void Prefab_Wired_PreviewInEditMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"o prefab {PrefabPath} não existe");

            CastBarView view = prefab.GetComponent<CastBarView>();
            Assert.That(view, Is.Not.Null, "CastBar sem CastBarView na raiz");
            foreach (string part in new[] { "content", "fill", "indeterminate", "icon", "nameText", "secondsText", "completed", "cancelled" })
            {
                Assert.That(Field<Object>(view, part), Is.Not.Null, $"CastBar sem {part}");
            }

            Assert.That(Field<Image>(view, "fill").type, Is.EqualTo(Image.Type.Filled));
            Assert.That(Field<Image>(view, "fill").fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(prefab.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget), Is.Empty,
                "a barra é só exibição: nenhum gráfico recebe clique");
            Assert.That(prefab.GetComponentsInChildren<TMP_Text>(true).Where(t => new SerializedObject(t).FindProperty("m_fontAsset").objectReferenceValue != null),
                Is.Empty, "os textos caem na fonte padrão do TMP_Settings (ADR 0039)");
            var animator = Field<Animator>(view, "animator");
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Assert.That(controller, Is.Not.Null, "CastBar sem controller do resultado");
            Assert.That(controller.parameters.Select(p => (p.name, p.type)), Is.EquivalentTo(new[]
            {
                ("Channeling", AnimatorControllerParameterType.Bool),
                ("Completed", AnimatorControllerParameterType.Trigger),
                ("Cancelled", AnimatorControllerParameterType.Trigger),
            }));

            var binder = prefab.GetComponent<CastBarBinder>();
            Assert.That(binder, Is.Not.Null, "CastBar sem CastBarBinder na raiz");
            Assert.That(binder.Source, Is.InstanceOf<ChannelSource>());
            Assert.That(binder.View, Is.SameAs(view));
            Assert.That(binder.Owner.Mode, Is.EqualTo(ActorResolveMode.SelfOrParent));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _canvas.transform);
            instance.GetComponent<CastBarBinder>().Rebind();
            CastBarView shown = instance.GetComponent<CastBarView>();

            Assert.That(Field<GameObject>(shown, "content").activeSelf, Is.True, "a prévia mostra a barra");
            Assert.That(Field<Image>(shown, "fill").fillAmount, Is.EqualTo(0.6f), "a prévia mostra 60 %");
            Assert.That(Field<TMP_Text>(shown, "nameText").text, Is.EqualTo("Fireball"), "a prévia mostra Fireball");
            Assert.That(Field<TMP_Text>(shown, "secondsText").text, Is.EqualTo("1.2"), "a prévia mostra 1,2 s");
        }

        [Test]
        public void CastBar_Validate_NoSourceViewPrefab()
        {
            CastBarBinder noSource = CreateUI<CastBarBinder>("noSource");
            noSource.SetView(CreateUI<CastBarView>("view"));
            CastBarBinder noView = CreateUI<CastBarBinder>("noView");
            noView.SetSource(new ChannelSource());
            CastBarBinder wrongView = CreateUI<CastBarBinder>("wrongView");
            wrongView.SetSource(new ChannelSource());
            SetFields(wrongView, ("view", CreateUI<SlotView>("slot")));

            Assert.That(Errors(noSource), Is.EqualTo(new[] { "source" }), "sem fonte");
            Assert.That(Errors(noView), Is.EqualTo(new[] { "view" }), "sem view");
            Assert.That(Errors(wrongView), Is.EqualTo(new[] { "view" }), "view que não desenha CastBarModel");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"o prefab {PrefabPath} não existe");
            Assert.That(Messages(prefab.GetComponent<CastBarBinder>()), Is.Empty, "o prefab do pacote não tem problema");
        }

        private static CastBarModel Channeling(Sprite icon, string name, float fraction, float remaining,
            CastBarResult last = CastBarResult.None, int count = 0) =>
            new CastBarModel(CastBarState.Channeling, icon, name, fraction, remaining, false, last, count);

        private static CastBarModel Ended(Sprite icon, string name, CastBarResult result, int count) =>
            new CastBarModel(CastBarState.Idle, icon, name, 0f, 0f, false, result, count);

        // O dono sem ASC na cena do teste dá um aviso legítimo; as regras da barra são erros
        private static string[] Errors(CastBarBinder binder) =>
            Messages(binder).Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.PropertyPath).ToArray();

        private static IReadOnlyList<ValidationMessage> Messages(CastBarBinder binder)
        {
            var result = new ValidationResult(binder);
            binder.Validate(result);
            return result.Messages;
        }

        private Rig CreateRig()
        {
            CastBarView view = CreateUI<CastBarView>("castBar");
            var content = new GameObject("content", typeof(RectTransform));
            content.transform.SetParent(view.transform);
            var rig = new Rig
            {
                View = view,
                Content = content,
                Fill = CreateChild<Image>(content, "fill"),
                Indeterminate = CreateChildObject(content, "indeterminate"),
                Icon = CreateChild<Image>(content, "icon"),
                Name = CreateChild<RecordingText>(content, "name"),
                Seconds = CreateChild<RecordingText>(content, "seconds"),
                Completed = CreateChildObject(content, "completed"),
                Cancelled = CreateChildObject(content, "cancelled"),
            };
            rig.Completed.SetActive(false);
            rig.Cancelled.SetActive(false);
            SetFields(view, ("content", content), ("fill", rig.Fill), ("indeterminate", rig.Indeterminate),
                ("icon", rig.Icon), ("nameText", rig.Name), ("secondsText", rig.Seconds), ("completed", rig.Completed),
                ("cancelled", rig.Cancelled));
            return rig;
        }

        // Animator iniciado em edição, com os três parâmetros e sem transição: o gatilho fica ligado até o teste ler
        private Animator AddAnimator(Rig rig)
        {
            var controller = new AnimatorController();
            _created.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("Channeling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Completed", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cancelled", AnimatorControllerParameterType.Trigger);
            Animator animator = rig.View.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetFields(rig.View, ("animator", animator));
            return animator;
        }

        // Lê e desliga os gatilhos: cada leitura conta só os disparos depois da anterior
        private static (bool Completed, bool Cancelled) Fired(Animator animator)
        {
            bool completed = animator.GetBool("Completed");
            bool cancelled = animator.GetBool("Cancelled");
            animator.ResetTrigger("Completed");
            animator.ResetTrigger("Cancelled");
            return (completed, cancelled);
        }

        private UITheme CreateTheme(string culture)
        {
            var provider = ScriptableObject.CreateInstance<TableTextProvider>();
            provider.SetCulture(CultureInfo.GetCultureInfo(culture));
            var theme = ScriptableObject.CreateInstance<UITheme>();
            _created.Add(provider);
            _created.Add(theme);
            SetFields(theme, ("text", provider));
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

        private static GameObject CreateChildObject(GameObject parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent.transform);
            return gameObject;
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
            _created.Add(sprite);
            _created.Add(texture);
            return sprite;
        }

        private static T Field<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

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
            public CastBarView View;
            public GameObject Content;
            public Image Fill;
            public GameObject Indeterminate;
            public Image Icon;
            public RecordingText Name;
            public RecordingText Seconds;
            public GameObject Completed;
            public GameObject Cancelled;
        }
    }
}
