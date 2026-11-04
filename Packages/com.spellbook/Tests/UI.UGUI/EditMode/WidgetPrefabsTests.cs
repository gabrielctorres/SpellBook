using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
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
