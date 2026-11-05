#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.UI;
using SpellBook.Testing;
using SpellBook.Time;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// Tooltip do ícone de efeito (Spec do B-018, BF4; C10 do PRD): o prefab <c>EffectList</c> cria o <c>EffectIcon</c> do
    /// pacote, e o ponteiro no ícone abre o tooltip do efeito do item no ASC do item, com as pilhas e o tempo que o ícone
    /// mostra; sem binder de tooltip, o ícone funciona sem ele (CA10).
    /// </summary>
    public class EffectIconTooltipTests
    {
        private const string Folder = "Packages/com.spellbook/Runtime/UI.UGUI/Prefabs/";

        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private UIUpdateDriver _driver;
        private AbilitySystemComponent _asc;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _driver = Track(new GameObject("hud", typeof(Canvas))).AddComponent<UIUpdateDriver>();
            _driver.SetTimeSource(_clock);
            _asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(_asc, false);
            _asc.SetTimeSource(_clock);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void HoverIcon_OpensEffectTooltip_SameStacksAndRemaining_NoBinderStillWorks()
        {
            AbilitySystemComponent asc = _asc;
            GameplayEffect burn = _gas.CreateDurationEffect(10f);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            using (var serialized = new SerializedObject(burn))
            {
                serialized.FindProperty("localizedName.text").stringValue = "Queimadura";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            EffectListBinder list = CreateList(asc, spawner);
            for (int i = 0; i < 3; i++) asc.ApplyEffect(burn);
            Assert.That(spawner.Live.Count, Is.EqualTo(1), "três aplicações do mesmo efeito, um ícone");
            GameObject icon = spawner.Live[0];
            var trigger = icon.GetComponent<TooltipTrigger>();
            var view = icon.GetComponent<EffectIconView>();
            TMP_Text seconds = Text(view, "secondsText");
            TMP_Text stacks = Text(view, "stacksText");
            TooltipBinder tooltip = CreateTooltip(out RecordingTooltip shown, out TooltipBinderAnchor anchor);
            trigger.SetBinder(anchor);

            Pointer(icon, true);
            Wait(0.5f);
            TooltipModel opened = shown.Last;
            string tooltipSecondsAtOpen = Str(opened.RemainingText);
            string iconSecondsAtOpen = seconds.text;
            Wait(2.5f);
            string tooltipSecondsLater = Str(shown.Last.RemainingText);
            Pointer(icon, false);

            Assert.That(list.Count, Is.EqualTo(1));
            Assert.That(opened.Title, Is.EqualTo("Queimadura"), "o tooltip é do efeito do item (o assunto que a view escreveu)");
            Assert.That(opened.HasStacks, Is.True, "o tooltip leu as pilhas no ASC do item");
            Assert.That(opened.Stacks, Is.EqualTo(3));
            Assert.That(stacks.text, Is.EqualTo("3"), "o ícone mostra as mesmas pilhas");
            Assert.That(opened.HasRemaining, Is.True);
            Assert.That(tooltipSecondsAtOpen, Is.EqualTo(iconSecondsAtOpen).And.Not.Empty,
                "o tooltip e o ícone mostram o mesmo tempo");
            Assert.That(tooltipSecondsLater, Is.EqualTo(seconds.text).And.Not.EqualTo(iconSecondsAtOpen),
                "com o tempo andando, os dois seguem iguais");
            Assert.That(tooltip.IsOpen, Is.False, "o ponteiro saindo fecha");

            trigger.SetBinder(Track(ScriptableObject.CreateInstance<TooltipBinderAnchor>()));
            asc.ApplyEffect(burn);
            Assert.DoesNotThrow(() => Pointer(icon, true), "sem binder de tooltip, o ponteiro não lança");
            Wait(0.5f);
            Assert.That(tooltip.IsOpen, Is.False, "sem binder na âncora, nada abre");
            Assert.That(stacks.text, Is.EqualTo("4"), "o ícone segue desenhando sem tooltip");
        }

        // A lista do pacote sob o driver, configurada desligada, com o spawner de teste e o dono explícito
        private EffectListBinder CreateList(AbilitySystemComponent asc, RecordingSpawnerAsset spawner)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "EffectList.prefab");
            Assert.That(prefab, Is.Not.Null, $"o prefab EffectList não existe em {Folder}");
            GameObject holder = Track(new GameObject("lista"));
            holder.SetActive(false);
            holder.transform.SetParent(_driver.transform);
            var binder = Object.Instantiate(prefab, holder.transform).GetComponent<EffectListBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            using (var serialized = new SerializedObject(binder))
            {
                serialized.FindProperty("spawner").objectReferenceValue = spawner;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            holder.SetActive(true);
            return binder;
        }

        private TooltipBinder CreateTooltip(out RecordingTooltip view, out TooltipBinderAnchor anchor)
        {
            GameObject gameObject = Track(new GameObject("tooltip"));
            gameObject.SetActive(false);
            gameObject.transform.SetParent(_driver.transform);
            TooltipBinder tooltip = gameObject.AddComponent<TooltipBinder>();
            view = gameObject.AddComponent<RecordingTooltip>();
            tooltip.SetView(view);
            gameObject.SetActive(true);
            anchor = Track(ScriptableObject.CreateInstance<TooltipBinderAnchor>());
            anchor.Provide(tooltip);
            return tooltip;
        }

        // O ASC e o driver andam juntos no relógio manual
        private void Wait(float seconds)
        {
            _clock.Advance(seconds);
            _asc.Tick(seconds);
            _driver.Tick();
        }

        private static void Pointer(GameObject target, bool enter)
        {
            var data = new PointerEventData(null);
            if (enter) ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler);
            else ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
        }

        private static string Str(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        private static TMP_Text Text(Object view, string field) =>
            new SerializedObject(view).FindProperty(field).objectReferenceValue as TMP_Text;

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>View de tooltip de teste: guarda o último modelo.</summary>
        public sealed class RecordingTooltip : MonoBehaviour, IView<TooltipModel>, ITooltipPlacementTarget
        {
            public TooltipModel Last;

            public void Render(in TooltipModel model) => Last = model;

            public void Place(RectTransform target)
            {
            }
        }
    }
}
#endif
