using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// Prefabs dos widgets (R11; ADR 0039, itens 3 e 8): cada um traz o binder e a view ligados, a fonte do GAS.UI já
    /// escolhida e o texto sem fonte própria; a barra mostra a prévia em edição (CA10, parte automática).
    /// </summary>
    public class WidgetPrefabsTests
    {
        private const string Folder = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/";

        private GameObject _canvas;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null)
            {
                Object.DestroyImmediate(_canvas);
            }
        }

        [Test]
        public void Prefabs_HaveBinderAndView_Wired()
        {
            GameObject bar = LoadPrefab("AttributeBar");
            GameObject text = LoadPrefab("AttributeText");
            GameObject indicator = LoadPrefab("TagIndicator");

            BarView barView = AssertWired<BarModel, BarBinder, BarView, AttributeBarSource>(bar);
            Image fill = Field<Image>(barView, "fill");
            Assert.That(fill, Is.Not.Null, "a barra sem preenchimento");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled), "o preenchimento não é do tipo Filled");
            Assert.That(fill.sprite, Is.Not.Null, "Filled sem sprite não recorta a imagem");
            Image trail = Field<Image>(barView, "trail");
            Assert.That(trail, Is.Not.Null.And.Not.SameAs(fill), "a barra sem rastro próprio");
            Assert.That(trail.type, Is.EqualTo(Image.Type.Filled), "o rastro não é do tipo Filled");
            Assert.That(trail.transform.GetSiblingIndex(), Is.LessThan(fill.transform.GetSiblingIndex()),
                "o rastro fica atrás do preenchimento");
            AssertNoFont(Field<TMP_Text>(barView, "label"), "AttributeBar");

            TextView textView = AssertWired<TextModel, TextBinder, TextView, AttributeTextSource>(text);
            AssertNoFont(Field<TMP_Text>(textView, "text"), "AttributeText");

            TagIndicatorView indicatorView = AssertWired<TagIndicatorModel, TagIndicatorBinder, TagIndicatorView, TagSource>(indicator);
            GameObject target = Field<GameObject>(indicatorView, "target");
            Assert.That(target, Is.Not.Null, "o indicador sem alvo");
            Assert.That(target.transform.IsChildOf(indicator.transform) && target != indicator, Is.True,
                "o alvo do indicador é um filho do prefab");
        }

        [Test]
        public void AttributeBarPrefab_InEditMode_ShowsPreviewHalfAnd50Of100()
        {
            _canvas = new GameObject("canvas", typeof(Canvas));
            GameObject prefab = LoadPrefab("AttributeBar");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _canvas.transform);

            BarView view = instance.GetComponent<BarView>();

            Assert.That(Field<Image>(view, "fill").fillAmount, Is.EqualTo(0.5f));
            Assert.That(Field<TMP_Text>(view, "label").text, Is.EqualTo("50 / 100"));
        }

        [Test]
        public void AbilitySlotPrefab_HasTooltipTrigger_WithAbilitySourceFromItem()
        {
            GameObject slot = LoadPrefab("AbilitySlot");

            var trigger = slot.GetComponent<TooltipTrigger>();

            Assert.That(trigger, Is.Not.Null, "AbilitySlot sem TooltipTrigger na raiz");
            Assert.That(slot.GetComponent<UIItemContext>(), Is.Not.Null,
                "o gatilho lê o dono e o assunto do UIItemContext do mesmo objeto");
            var source = trigger.Source as AbilityTooltipSource;
            Assert.That(source, Is.Not.Null, "AbilitySlot: a fonte do tooltip não é a de habilidade");
            Assert.That(source.Ability, Is.Null, "sem habilidade, a fonte mostra a do item (o assunto que o SlotView escreve)");
            Assert.That(trigger.Mode, Is.EqualTo(TooltipOpenMode.PointerAndFocus));
            Assert.That(trigger.Target, Is.Null, "o tooltip abre ao lado do próprio slot");
            Assert.That(trigger.Binder, Is.Null, "a âncora do binder é do projeto, como a do prefab Tooltip");
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { trigger });
            Assert.That(messages.Select(m => (m.Severity, m.PropertyPath)),
                Is.EqualTo(new[] { (ValidationSeverity.Error, "binder") }),
                "só falta a âncora: o ponteiro tem onde bater e a fonte usa o assunto do item");
        }

        [Test]
        public void EffectIconPrefab_HasTooltipWithEffectSourceFromItem()
        {
            GameObject icon = LoadPrefab("EffectIcon");

            var trigger = icon.GetComponent<TooltipTrigger>();
            var view = icon.GetComponent<EffectIconView>();

            Assert.That(trigger, Is.Not.Null, "EffectIcon sem TooltipTrigger na raiz");
            Assert.That(view, Is.Not.Null, "EffectIcon sem EffectIconView na raiz");
            Assert.That(icon.GetComponent<UIBinderBase>(), Is.Null, "o item da lista não tem binder próprio");
            Assert.That(Field<UIItemContext>(view, "itemContext"), Is.SameAs(icon.GetComponent<UIItemContext>()).And.Not.Null,
                "a view escreve no UIItemContext do próprio item, que o gatilho lê");
            var source = trigger.Source as EffectTooltipSource;
            Assert.That(source, Is.Not.Null, "EffectIcon: a fonte do tooltip não é a de efeito");
            Assert.That(source.Effect, Is.Null, "sem efeito, a fonte mostra o do item (o assunto que a view escreve)");
            Assert.That(trigger.Mode, Is.EqualTo(TooltipOpenMode.PointerAndFocus));
            Assert.That(trigger.Target, Is.Null, "o tooltip abre ao lado do próprio ícone");
            Assert.That(trigger.Binder, Is.Null, "a âncora do binder é do projeto, como a do prefab Tooltip");
            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { trigger });
            Assert.That(messages.Select(m => (m.Severity, m.PropertyPath)),
                Is.EqualTo(new[] { (ValidationSeverity.Error, "binder") }),
                "só falta a âncora: o ponteiro tem onde bater e a fonte usa o assunto do item");
            Assert.That(icon.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget).Select(g => g.name),
                Is.EqualTo(new[] { "Frame" }), "só a moldura recebe o ponteiro, para o tooltip");
            Assert.That(icon.GetComponentsInChildren<Selectable>(true), Is.Empty, "o ícone é só exibição");
            Assert.That(icon.GetComponentsInChildren<IPointerClickHandler>(true), Is.Empty, "nada trata o clique");

            foreach (string part in new[] { "frame", "icon", "durationFill", "secondsText", "stacksText", "defaultIcon" })
            {
                Assert.That(Field<Object>(view, part), Is.Not.Null, $"EffectIcon sem {part}");
            }

            Assert.That(Field<Image>(view, "frame").name, Is.EqualTo("Frame"), "a moldura que recebe o ponteiro é a do papel");
            Image fill = Field<Image>(view, "durationFill");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled), "a duração não é do tipo Filled");
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Radial360), "a duração não é radial");
            Assert.That(fill.sprite, Is.Not.Null, "Filled sem sprite não recorta a imagem");
            AssertNoFont(Field<TMP_Text>(view, "secondsText"), "EffectIcon");
            AssertNoFont(Field<TMP_Text>(view, "stacksText"), "EffectIcon");
            var animator = Field<Animator>(view, "animator");
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            Assert.That(animator, Is.SameAs(icon.GetComponent<Animator>()), "EffectIcon sem Animator ligado na raiz");
            Assert.That(controller, Is.Not.Null, "EffectIcon sem controller do piscar");
            Assert.That(controller.parameters.Select(p => (p.name, p.type)),
                Is.EqualTo(new[] { ("EndingSoon", AnimatorControllerParameterType.Bool) }));
            Assert.That(controller.animationClips.SelectMany(AnimationUtility.GetCurveBindings)
                    .Any(b => b.type == typeof(CanvasGroup) && b.propertyName == "m_Alpha"), Is.True,
                "o piscar anima o alfa do CanvasGroup");
            Assert.That(icon.GetComponent<CanvasGroup>(), Is.Not.Null, "EffectIcon sem o CanvasGroup que pisca");
        }

        [Test]
        public void EffectListPrefab_BinderLayoutOverflow_Wired()
        {
            GameObject list = LoadPrefab("EffectList");
            GameObject icon = LoadPrefab("EffectIcon");

            var binder = list.GetComponent<EffectListBinder>();

            Assert.That(binder, Is.Not.Null, "EffectList sem EffectListBinder na raiz");
            Assert.That(binder.Source, Is.InstanceOf<ActiveEffectsSource>(), "EffectList: fonte errada");
            Assert.That(binder.ItemPrefab, Is.SameAs(icon), "a lista cria o EffectIcon");
            Assert.That(binder.Owner.Mode, Is.EqualTo(ActorResolveMode.SelfOrParent));
            Assert.That(list.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null, "EffectList sem layout horizontal");
            var container = Field<Transform>(binder, "container");
            Assert.That(container, Is.Not.Null.And.Not.SameAs(list.transform), "os ícones ficam num filho próprio");
            Assert.That(container.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null, "o container sem layout horizontal");
            Assert.That(new SerializedObject(binder).FindProperty("maxVisible").intValue, Is.EqualTo(8));
            var overflow = Field<ListOverflowView>(binder, "overflowView");
            Assert.That(overflow, Is.Not.Null, "EffectList sem a view do \"+N\"");
            Assert.That(overflow.transform.IsChildOf(container), Is.False, "o \"+N\" fica fora do container, depois dos ícones");
            Assert.That(overflow.transform.GetSiblingIndex(), Is.GreaterThan(container.GetSiblingIndex()));
            GameObject badge = Field<GameObject>(overflow, "target");
            Assert.That(badge, Is.Not.Null.And.Not.SameAs(overflow.gameObject), "o \"+N\" sem alvo filho");
            Assert.That(badge.activeSelf, Is.False, "sem ocultos, o \"+N\" começa escondido");
            AssertNoFont(Field<TMP_Text>(overflow, "text"), "EffectList");
            Assert.That(list.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget), Is.Empty,
                "a lista não recebe o ponteiro; só os ícones");
        }

        private static TView AssertWired<TModel, TBinder, TView, TSource>(GameObject prefab)
            where TModel : struct
            where TBinder : UIBinder<TModel>
            where TView : MonoBehaviour, IView<TModel>
            where TSource : UISource<TModel>
        {
            var binder = prefab.GetComponent<TBinder>();
            var view = prefab.GetComponentInChildren<TView>(true);
            Assert.That(binder, Is.Not.Null, $"{prefab.name} sem {typeof(TBinder).Name} na raiz");
            Assert.That(view, Is.Not.Null, $"{prefab.name} sem {typeof(TView).Name}");
            Assert.That(binder.View, Is.SameAs(view), $"{prefab.name}: o binder não aponta para a view do prefab");
            Assert.That(binder.Source, Is.InstanceOf<TSource>(), $"{prefab.name}: fonte errada");
            Assert.That(binder.Owner.Mode, Is.EqualTo(ActorResolveMode.SelfOrParent), $"{prefab.name}: dono fora do padrão");
            return view;
        }

        private static void AssertNoFont(TMP_Text text, string prefab)
        {
            Assert.That(text, Is.Not.Null, $"{prefab} sem texto");
            Assert.That(new SerializedObject(text).FindProperty("m_fontAsset").objectReferenceValue, Is.Null,
                $"{prefab}: o texto tem fonte própria; deve cair na fonte padrão do TMP_Settings (ADR 0039)");
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, $"o prefab {name} não existe em {Folder}");
            return prefab;
        }

        private static T Field<T>(Object owner, string field) where T : Object
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            if (property == null)
            {
                throw new ArgumentException($"{owner.GetType().Name} não tem o campo {field}");
            }

            return property.objectReferenceValue as T;
        }
    }
}
