using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Text;
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
    /// View e prefabs do slot e da barra (Spec do B-016, SL8; R6 a R11 do PRD), num Canvas do teste. O prefab do slot,
    /// com um binder de slot posto no teste, mostra a prévia em edição (CA12, parte automática).
    /// </summary>
    public class SlotViewTests
    {
        private const string Folder = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/";

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
        public void SlotView_RadialSecondsChargesLabel()
        {
            Rig rig = CreateRig();
            Sprite sprite = CreateSprite();
            int setsBefore = rig.WrittenStringSets;
            SlotModel model = new SlotModel(SlotState.OnCooldown, sprite, CreateSubject())
                .WithCooldown(0.4f, 3.2f)
                .WithCharges(2, 3, 0.5f)
                .WithKeyLabel(Write("Q"));

            rig.View.Render(model);

            Assert.That(rig.Icon.sprite, Is.SameAs(sprite));
            Assert.That(rig.Icon.enabled, Is.True, "ícone com sprite fica ligado");
            Assert.That(rig.CooldownFill.fillAmount, Is.EqualTo(0.4f));
            Assert.That(rig.Seconds.text, Is.EqualTo("4"), "segundos inteiros, arredondados para cima");
            Assert.That(rig.Charges.text, Is.EqualTo("2"));
            Assert.That(rig.ChargeFill.fillAmount, Is.EqualTo(0.5f));
            Assert.That(rig.Key.text, Is.EqualTo("Q"));
            Assert.That(rig.Failure.text, Is.Empty, "o motivo só aparece com FailureVisible");
            Assert.That(rig.WrittenStringSets, Is.EqualTo(setsBefore), "a view entregou string ao TextMeshPro em vez do trecho");

            int failureSets = rig.Failure.StringSets;
            rig.View.Render(model.WithFailure("gas.activation.cost", Write("Sem mana"), true, 1));
            Assert.That(rig.Failure.text, Is.EqualTo("Sem mana"));
            Assert.That(rig.Failure.StringSets, Is.EqualTo(failureSets), "o motivo foi entregue como string");

            rig.View.Render(new SlotModel(SlotState.Ready, null, model.Subject).WithKeyLabel(Write("W"))
                .WithFailure("gas.activation.cost", Write("Sem mana"), false, 1));

            Assert.That(rig.Icon.enabled, Is.False, "sem sprite, o ícone desliga");
            Assert.That(rig.CooldownFill.fillAmount, Is.Zero);
            Assert.That(rig.Seconds.text, Is.Empty, "sem recarga, sem segundos");
            Assert.That(rig.Charges.text, Is.Empty, "sem cargas, sem contador");
            Assert.That(rig.ChargeFill.fillAmount, Is.Zero, "sem cargas, sem radial da próxima");
            Assert.That(rig.Key.text, Is.EqualTo("W"));
            Assert.That(rig.Failure.text, Is.Empty, "o motivo some quando deixa de aparecer");
        }

        [Test]
        public void DimOnNoResourceNoChargesBlocked_HighlightOnActive()
        {
            Rig rig = CreateRig();
            Sprite sprite = CreateSprite();
            ScriptableObject subject = CreateSubject();

            foreach (SlotState state in new[] { SlotState.NoResource, SlotState.NoCharges, SlotState.Blocked })
            {
                rig.View.Render(new SlotModel(state, sprite, subject));
                Assert.That(rig.Icon.color, Is.EqualTo(rig.DimmedTint), $"{state} escurece o ícone");
                Assert.That(rig.Highlight.activeSelf, Is.False, $"{state} sem ativa não realça");
            }

            foreach (SlotState state in new[] { SlotState.Ready, SlotState.OnCooldown })
            {
                rig.View.Render(new SlotModel(state, sprite, subject));
                Assert.That(rig.Icon.color, Is.EqualTo(Color.white), $"{state} não escurece");
            }

            rig.View.Render(new SlotModel(SlotState.Ready, sprite, subject).WithActivity(true, false, true));
            Assert.That(rig.Highlight.activeSelf, Is.True, "ativa realça");

            rig.View.Render(new SlotModel(SlotState.NoResource, sprite, subject).WithActivity(true, true, false));
            Assert.That(rig.Highlight.activeSelf, Is.True, "o realce da ativa fica sobre a falha (R6)");
            Assert.That(rig.Icon.color, Is.EqualTo(rig.DimmedTint), "a ativa não apaga o escurecer da falha (R6)");

            rig.View.Render(new SlotModel(SlotState.Ready, sprite, subject));
            Assert.That(rig.Highlight.activeSelf, Is.False, "sem ativa, o realce desliga");
        }

        [Test]
        public void PulseAndShake_OncePerCounterChange()
        {
            Rig rig = CreateRig();
            Animator animator = AddAnimator(rig);
            ScriptableObject fireball = CreateSubject();
            ScriptableObject heal = CreateSubject();
            SlotModel ready = new SlotModel(SlotState.Ready, null, fireball);

            rig.View.Render(ready.WithReadyPulse(3).WithFailure(null, default, false, 2));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "o primeiro desenho não anima (R8)");

            rig.View.Render(ready.WithReadyPulse(3).WithFailure(null, default, false, 2));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "contadores iguais não animam");

            rig.View.Render(ready.WithReadyPulse(4).WithFailure(null, default, false, 2));
            Assert.That(Fired(animator), Is.EqualTo((true, false)), "o pulso sobe: pulsa uma vez");

            rig.View.Render(ready.WithReadyPulse(4).WithFailure(null, default, false, 2));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "um pulso por mudança");

            rig.View.Render(ready.WithReadyPulse(4).WithFailure("gas.activation.cost", default, true, 3));
            Assert.That(Fired(animator), Is.EqualTo((false, true)), "a falha sobe: treme uma vez (R7)");

            rig.View.Render(new SlotModel(SlotState.Ready, null, heal).WithReadyPulse(9).WithFailure(null, default, false, 9));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "outro assunto (item do pool) só troca a base");

            rig.View.Render(ready.WithReadyPulse(5).WithFailure(null, default, false, 5));
            Fired(animator);
            rig.View.Render(default);
            rig.View.Render(ready.WithReadyPulse(10).WithFailure(null, default, false, 10));
            Assert.That(Fired(animator), Is.EqualTo((false, false)), "depois do vazio, o desenho é o primeiro de novo");
        }

        [Test]
        public void Empty_FrameOrHidden()
        {
            Rig rig = CreateRig();
            Sprite sprite = CreateSprite();
            SlotModel full = new SlotModel(SlotState.OnCooldown, sprite, CreateSubject())
                .WithCooldown(0.5f, 2f).WithCharges(1, 2, 0.5f).WithKeyLabel(Write("Q")).WithActivity(true, false, false);
            rig.View.Render(full);

            rig.View.Render(default);

            Assert.That(rig.Content.activeSelf, Is.True, "sem Hide When Empty, o vazio mostra a moldura");
            Assert.That(rig.Icon.enabled, Is.False, "o vazio desliga o ícone");
            Assert.That(rig.CooldownFill.fillAmount, Is.Zero);
            Assert.That(rig.ChargeFill.fillAmount, Is.Zero);
            Assert.That(rig.Seconds.text, Is.Empty);
            Assert.That(rig.Charges.text, Is.Empty);
            Assert.That(rig.Key.text, Is.Empty);
            Assert.That(rig.Highlight.activeSelf, Is.False, "o vazio não realça");

            SetField(rig.View, "hideWhenEmpty", true);
            rig.View.Render(default);
            Assert.That(rig.Content.activeSelf, Is.False, "com Hide When Empty, o vazio esconde o conteúdo");

            rig.View.Render(full);
            Assert.That(rig.Content.activeSelf, Is.True, "o conteúdo volta fora do vazio");
        }

        [Test]
        public void WritesSubjectToItemContext()
        {
            Rig rig = CreateRig();
            UIItemContext context = rig.View.gameObject.AddComponent<UIItemContext>();
            SetFields(rig.View, ("itemContext", context));
            ScriptableObject fireball = CreateSubject();
            int changes = 0;
            context.Changed += () => changes++;

            rig.View.Render(new SlotModel(SlotState.Ready, null, fireball));
            ScriptableObject written = context.Subject;
            rig.View.Render(new SlotModel(SlotState.OnCooldown, null, fireball));
            int changesAfterSameSubject = changes;
            rig.View.Render(default);

            Assert.That(written, Is.SameAs(fireball));
            Assert.That(changesAfterSameSubject, Is.EqualTo(1), "o mesmo assunto não avisa de novo");
            Assert.That(context.Subject, Is.Null, "o vazio limpa o assunto");
        }

        [Test]
        public void Overflow_ShowsPlusN()
        {
            var target = new GameObject("target");
            target.transform.SetParent(_canvas.transform);
            RecordingText text = CreateUI<RecordingText>("text");
            ListOverflowView view = CreateUI<ListOverflowView>("overflow");
            SetFields(view, ("target", target), ("text", text));
            int setsBefore = text.StringSets;

            view.Render(new ListOverflowModel(3));
            bool shownWithHidden = target.activeSelf;
            string written = text.text;
            int sets = text.StringSets;
            view.Render(new ListOverflowModel(0));

            Assert.That(shownWithHidden, Is.True);
            Assert.That(written, Is.EqualTo("+3"));
            Assert.That(sets, Is.EqualTo(setsBefore), "a view entregou string ao TextMeshPro em vez do trecho");
            Assert.That(target.activeSelf, Is.False, "sem ocultos, o \"+N\" some");
        }

        [Test]
        public void Prefabs_SlotAndBar_Wired_SlotPreviewInEditMode()
        {
            GameObject slot = LoadPrefab("AbilitySlot");
            GameObject bar = LoadPrefab("AbilityBar");

            SlotView slotView = slot.GetComponent<SlotView>();
            Assert.That(slotView, Is.Not.Null, "AbilitySlot sem SlotView na raiz");
            Assert.That(slot.GetComponent<UIBinderBase>(), Is.Null, "o item da barra não tem binder próprio");
            Assert.That(Field<UIItemContext>(slotView, "itemContext"), Is.SameAs(slot.GetComponent<UIItemContext>()),
                "a view escreve no UIItemContext do próprio item");
            foreach (string part in new[] { "content", "icon", "cooldownFill", "cooldownText", "chargesText", "chargeFill", "keyLabel", "failureText", "highlight" })
            {
                Assert.That(Field<Object>(slotView, part), Is.Not.Null, $"AbilitySlot sem {part}");
            }

            Assert.That(Field<Image>(slotView, "cooldownFill").type, Is.EqualTo(Image.Type.Filled));
            Assert.That(Field<Image>(slotView, "cooldownFill").fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(slot.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget), Is.Empty,
                "o slot é só exibição: nenhum gráfico recebe clique (R11)");
            Assert.That(slot.GetComponentsInChildren<TMP_Text>(true).Where(t => new SerializedObject(t).FindProperty("m_fontAsset").objectReferenceValue != null),
                Is.Empty, "os textos caem na fonte padrão do TMP_Settings (ADR 0039)");
            var animator = Field<Animator>(slotView, "animator");
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Assert.That(controller, Is.Not.Null, "AbilitySlot sem controller do pulso e do tremor");
            Assert.That(controller.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name),
                Is.EquivalentTo(new[] { "Pulse", "Shake" }));

            var binder = bar.GetComponent<AbilityBarBinder>();
            Assert.That(binder, Is.Not.Null, "AbilityBar sem AbilityBarBinder na raiz");
            Assert.That(bar.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null, "AbilityBar sem layout horizontal");
            Assert.That(binder.Source, Is.InstanceOf<AbilityLoadoutSource>());
            Assert.That(binder.ItemPrefab, Is.SameAs(slot), "a barra cria o AbilitySlot");
            Assert.That(binder.Owner.Mode, Is.EqualTo(ActorResolveMode.SelfOrParent));

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(slot, _canvas.transform);
            var slotBinder = instance.AddComponent<AbilitySlotBinder>();
            SlotView view = instance.GetComponent<SlotView>();
            slotBinder.SetView(view);
            slotBinder.SetSource(new AbilitySlotSource());

            Assert.That(Field<Image>(view, "cooldownFill").fillAmount, Is.EqualTo(0.5f), "a prévia mostra 50 % de recarga");
            Assert.That(Field<TMP_Text>(view, "chargesText").text, Is.EqualTo("2"), "a prévia mostra 2 cargas");
            Assert.That(Field<TMP_Text>(view, "keyLabel").text, Is.EqualTo("Q"), "a prévia mostra o rótulo Q");
        }

        private Rig CreateRig()
        {
            SlotView view = CreateUI<SlotView>("slot");
            var content = new GameObject("content", typeof(RectTransform));
            content.transform.SetParent(view.transform);
            var highlight = new GameObject("highlight", typeof(RectTransform));
            highlight.transform.SetParent(content.transform);
            var rig = new Rig
            {
                View = view,
                Content = content,
                Highlight = highlight,
                Icon = CreateChild<Image>(content, "icon"),
                CooldownFill = CreateChild<Image>(content, "cooldown"),
                ChargeFill = CreateChild<Image>(content, "chargeFill"),
                Seconds = CreateChild<RecordingText>(content, "seconds"),
                Charges = CreateChild<RecordingText>(content, "charges"),
                Key = CreateChild<RecordingText>(content, "key"),
                Failure = CreateChild<RecordingText>(content, "failure"),
                DimmedTint = new Color(0.2f, 0.3f, 0.4f, 1f),
            };
            SetFields(view, ("content", content), ("icon", rig.Icon), ("cooldownFill", rig.CooldownFill),
                ("cooldownText", rig.Seconds), ("chargesText", rig.Charges), ("chargeFill", rig.ChargeFill),
                ("keyLabel", rig.Key), ("failureText", rig.Failure), ("highlight", highlight));
            var serialized = new SerializedObject(view);
            serialized.FindProperty("dimmedTint").colorValue = rig.DimmedTint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return rig;
        }

        // Animator iniciado em edição, com os dois gatilhos e sem transição: o gatilho fica ligado até o teste ler
        private Animator AddAnimator(Rig rig)
        {
            var controller = new AnimatorController();
            _created.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("Pulse", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Shake", AnimatorControllerParameterType.Trigger);
            Animator animator = rig.View.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.Update(0f);
            SetFields(rig.View, ("animator", animator));
            return animator;
        }

        // Lê e desliga os gatilhos: cada leitura conta só os disparos depois da anterior
        private static (bool Pulse, bool Shake) Fired(Animator animator)
        {
            bool pulse = animator.GetBool("Pulse");
            bool shake = animator.GetBool("Shake");
            animator.ResetTrigger("Pulse");
            animator.ResetTrigger("Shake");
            return (pulse, shake);
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

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, $"o prefab {name} não existe em {Folder}");
            return prefab;
        }

        private static T Field<T>(Object owner, string field) where T : Object =>
            new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;

        private static void SetField(Object target, string field, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            public SlotView View;
            public GameObject Content;
            public GameObject Highlight;
            public Image Icon;
            public Image CooldownFill;
            public Image ChargeFill;
            public RecordingText Seconds;
            public RecordingText Charges;
            public RecordingText Key;
            public RecordingText Failure;
            public Color DimmedTint;

            // Só os textos com conteúdo: limpar um texto atribui string.Empty, que não aloca
            public int WrittenStringSets => Seconds.StringSets + Charges.StringSets + Key.StringSets;
        }
    }
}
