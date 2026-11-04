using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI.Tests
{
    /// <summary>
    /// Gatilho do tooltip (Spec UI-3, Design 3): ponteiro com atraso, foco e código abrem e fecham (CA6), item desativado
    /// ou destruído fecha sem exceção (CA7), o item manda no dono e no assunto, alvo vazio é o próprio objeto e âncora sem
    /// binder não faz nada. Os eventos chegam por <see cref="ExecuteEvents"/>.
    /// </summary>
    public class TooltipTriggerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private UIUpdateDriver _driver;
        private ManualTimeSource _time;
        private TooltipBinder _binder;
        private TooltipBinderAnchor _anchor;
        private RecordingView _view;
        private TestSource _source;
        private Owner _resolverOwner;

        [SetUp]
        public void SetUp()
        {
            _driver = CreateObject("tela").AddComponent<UIUpdateDriver>();
            _time = Create<ManualTimeSource>();
            _driver.SetTimeSource(_time);
            _binder = CreateBinder(out _view);
            _anchor = Create<TooltipBinderAnchor>();
            _anchor.Provide(_binder);
            _source = new TestSource();
            _resolverOwner = CreateObject("dono do resolvedor").AddComponent<Owner>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void PointerEnter_AfterDelay_Opens_Exit_Closes()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.Pointer, 0.3f);

            Focus(trigger, true);
            Wait(1f);
            bool openBySelect = _binder.IsOpen;
            Focus(trigger, false);
            Pointer(trigger, true);
            Wait(0.2f);
            bool openAt02 = _binder.IsOpen;
            Wait(0.2f);
            bool openAt04 = _binder.IsOpen;
            Pointer(trigger, false);

            Assert.That(openBySelect, Is.False, "no modo Pointer, o foco não deveria abrir");
            Assert.That(openAt02, Is.False, "0,2 s do driver não chega ao atraso de 0,3 s");
            Assert.That(openAt04, Is.True, "passado o atraso, o ponteiro deveria abrir");
            Assert.That(_binder.IsOpen, Is.False, "sair com o ponteiro deveria fechar");
            Assert.That(_view.Last.IsEmpty, Is.True);
        }

        [Test]
        public void Select_Opens_Deselect_Closes()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.Focus, 0f);

            Pointer(trigger, true);
            bool openByPointer = _binder.IsOpen;
            Pointer(trigger, false);
            Focus(trigger, true);
            bool openBySelect = _binder.IsOpen;
            Focus(trigger, false);

            Assert.That(openByPointer, Is.False, "no modo Focus, o ponteiro não deveria abrir");
            Assert.That(openBySelect, Is.True, "selecionar deveria abrir");
            Assert.That(_binder.IsOpen, Is.False, "perder o foco deveria fechar");
        }

        [Test]
        public void Code_ShowHide()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.Code, 0f);

            Pointer(trigger, true);
            Focus(trigger, true);
            bool openByEvents = _binder.IsOpen;
            trigger.Show();
            bool openByShow = _binder.IsOpen;
            trigger.Hide();

            Assert.That(openByEvents, Is.False, "no modo Code, ponteiro e foco não deveriam abrir");
            Assert.That(openByShow, Is.True, "Show() deveria abrir");
            Assert.That(_binder.IsOpen, Is.False, "Hide() deveria fechar");
        }

        [Test]
        public void ItemDisabled_Closes_NoException()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.PointerAndFocus, 0f);
            trigger.Show();
            bool openBefore = _binder.IsOpen;

            Assert.DoesNotThrow(() => trigger.gameObject.SetActive(false));
            Assert.That(openBefore, Is.True);
            Assert.That(_binder.IsOpen, Is.False, "desativar o item deveria fechar o tooltip");
            Assert.That(_source.LastBinding.IsAttached, Is.False);
        }

        [Test]
        public void ItemDestroyed_Closes_NoException()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.PointerAndFocus, 0f);
            trigger.Show();
            bool openBefore = _binder.IsOpen;

            Assert.DoesNotThrow(() => Object.DestroyImmediate(trigger.gameObject));
            Assert.That(openBefore, Is.True);
            Assert.That(_binder.IsOpen, Is.False, "destruir o item deveria fechar o tooltip");
            Assert.That(_source.LastBinding.IsAttached, Is.False);
        }

        [Test]
        public void DisabledTrigger_Show_DoesNotOpen()
        {
            GameObject neverActive = CreateObject("nunca ativo", typeof(RectTransform));
            neverActive.SetActive(false);
            TooltipTrigger inactive = Configure(neverActive.AddComponent<TooltipTrigger>(), TooltipOpenMode.Code, 0f);
            TooltipTrigger hidden = CreateTrigger(TooltipOpenMode.Code, 0f);
            hidden.gameObject.SetActive(false);
            TooltipTrigger disabled = CreateTrigger(TooltipOpenMode.Code, 0f);
            disabled.enabled = false;

            inactive.Show();
            hidden.Show();
            disabled.Show();

            Assert.That(_binder.IsOpen, Is.False, "um gatilho desativado não deveria abrir o tooltip");
            Assert.That(_source.CreatedBindings, Is.Zero, "um gatilho desativado não deveria criar ligação");
        }

        [Test]
        public void Show_OnOtherBinder_ClosesPrevious()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.Code, 0f);
            TooltipBinder other = CreateBinder(out _);
            var otherAnchor = Create<TooltipBinderAnchor>();
            otherAnchor.Provide(other);
            trigger.Show();
            bool openOnFirst = _binder.IsOpen;

            trigger.SetBinder(otherAnchor);
            trigger.Show();
            bool firstOpenAfterSwitch = _binder.IsOpen;
            bool openOnOther = other.IsOpen;
            trigger.Hide();

            Assert.That(openOnFirst, Is.True);
            Assert.That(openOnOther, Is.True, "Show() deveria abrir no binder novo");
            Assert.That(firstOpenAfterSwitch, Is.False, "abrir em outro binder deveria fechar o anterior");
            Assert.That(other.IsOpen, Is.False, "Hide() deveria fechar o binder novo");
        }

        [Test]
        public void ItemContext_OwnerAndSubjectWin()
        {
            GameObject item = CreateObject("item", typeof(RectTransform));
            UIItemContext context = item.AddComponent<UIItemContext>();
            Owner itemOwner = CreateObject("dono do item").AddComponent<Owner>();
            Subject subject = Create<Subject>();
            SetItemOwner(context, itemOwner);
            context.SetSubject(subject);
            TooltipTrigger trigger = Configure(item.AddComponent<TooltipTrigger>(), TooltipOpenMode.Code, 0f);

            trigger.Show();

            Assert.That(_source.LastBinding.AttachedOwner, Is.SameAs(itemOwner), "com UIItemContext, o dono vem do item");
            Assert.That(_source.LastBinding.Subject, Is.SameAs(subject), "com UIItemContext, o assunto vem do item");
        }

        [Test]
        public void NoItemContext_UsesResolver()
        {
            TooltipTrigger trigger = CreateTrigger(TooltipOpenMode.Code, 0f);

            trigger.Show();

            Assert.That(_source.LastBinding.AttachedOwner, Is.SameAs(_resolverOwner), "sem UIItemContext, o dono vem do resolvedor");
            Assert.That(_source.LastBinding.Subject, Is.Null);
        }

        [Test]
        public void Target_EmptyUsesOwnRect_ElseConfigured()
        {
            TooltipTrigger own = CreateTrigger(TooltipOpenMode.Code, 0f);
            TooltipTrigger configured = CreateTrigger(TooltipOpenMode.Code, 0f);
            var target = (RectTransform)CreateObject("alvo", typeof(RectTransform)).transform;
            configured.SetTarget(target);

            own.Show();
            RectTransform ownTarget = _view.LastTarget;
            configured.Show();

            Assert.That(ownTarget, Is.SameAs(own.transform), "alvo vazio deveria ser o próprio RectTransform");
            Assert.That(_view.LastTarget, Is.SameAs(target));
        }

        [Test]
        public void NoBinderInAnchor_DoesNothing()
        {
            TooltipTrigger noAnchor = CreateTrigger(TooltipOpenMode.PointerAndFocus, 0f);
            noAnchor.SetBinder(null);
            TooltipTrigger emptyAnchor = CreateTrigger(TooltipOpenMode.PointerAndFocus, 0f);
            emptyAnchor.SetBinder(Create<TooltipBinderAnchor>());
            TooltipTrigger destroyedBinder = CreateTrigger(TooltipOpenMode.PointerAndFocus, 0f);
            var deadAnchor = Create<TooltipBinderAnchor>();
            TooltipBinder dead = CreateObject("binder destruído").AddComponent<TooltipBinder>();
            deadAnchor.Provide(dead);
            Object.DestroyImmediate(dead.gameObject);
            destroyedBinder.SetBinder(deadAnchor);

            noAnchor.name = "sem âncora";
            emptyAnchor.name = "âncora vazia";
            destroyedBinder.name = "binder destruído";
            var thrown = new List<string>();

            foreach (TooltipTrigger trigger in new[] { noAnchor, emptyAnchor, destroyedBinder })
            {
                try
                {
                    trigger.Show();
                    Pointer(trigger, true);
                    Focus(trigger, true);
                    trigger.Hide();
                    trigger.gameObject.SetActive(false);
                }
                catch (Exception exception)
                {
                    thrown.Add($"{trigger.name}: {exception.GetType().Name}");
                }
            }

            Assert.That(thrown, Is.Empty, "sem binder, o gatilho não deveria lançar");
            Assert.That(_source.CreatedBindings, Is.Zero, "sem binder, nenhuma ligação deveria ser criada");
            Assert.That(_binder.IsOpen, Is.False);
        }

        private void Wait(float seconds)
        {
            _time.Advance(seconds);
            _driver.Tick();
        }

        private static void Pointer(TooltipTrigger trigger, bool enter)
        {
            var data = new PointerEventData(null);
            if (enter)
            {
                ExecuteEvents.Execute(trigger.gameObject, data, ExecuteEvents.pointerEnterHandler);
            }
            else
            {
                ExecuteEvents.Execute(trigger.gameObject, data, ExecuteEvents.pointerExitHandler);
            }
        }

        private static void Focus(TooltipTrigger trigger, bool select)
        {
            var data = new BaseEventData(null);
            if (select)
            {
                ExecuteEvents.Execute(trigger.gameObject, data, ExecuteEvents.selectHandler);
            }
            else
            {
                ExecuteEvents.Execute(trigger.gameObject, data, ExecuteEvents.deselectHandler);
            }
        }

        // Só o binder de lista escreve o dono do item (internal); o teste faz o papel dele
        private static void SetItemOwner(UIItemContext context, Object owner)
        {
            MethodInfo setOwner = typeof(UIItemContext).GetMethod("SetOwner", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setOwner, Is.Not.Null, "UIItemContext.SetOwner não encontrado");
            setOwner.Invoke(context, new object[] { owner });
        }

        private TooltipBinder CreateBinder(out RecordingView view)
        {
            GameObject gameObject = CreateObject("tooltip " + _created.Count);
            gameObject.SetActive(false);
            gameObject.transform.SetParent(_driver.transform);
            TooltipBinder tooltip = gameObject.AddComponent<TooltipBinder>();
            view = gameObject.AddComponent<RecordingView>();
            tooltip.SetView(view);
            gameObject.SetActive(true);
            return tooltip;
        }

        private TooltipTrigger CreateTrigger(TooltipOpenMode mode, float delay)
        {
            GameObject gameObject = CreateObject("gatilho " + _created.Count, typeof(RectTransform));
            return Configure(gameObject.AddComponent<TooltipTrigger>(), mode, delay);
        }

        private TooltipTrigger Configure(TooltipTrigger trigger, TooltipOpenMode mode, float delay)
        {
            trigger.SetSource(_source);
            trigger.SetOwner(ActorResolver.ForExplicit(_resolverOwner));
            trigger.SetMode(mode);
            trigger.SetDelay(delay);
            trigger.SetBinder(_anchor);
            return trigger;
        }

        private GameObject CreateObject(string name, params Type[] components)
        {
            var gameObject = new GameObject(name, components);
            _created.Add(gameObject);
            return gameObject;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        public sealed class Owner : MonoBehaviour
        {
        }

        public sealed class Subject : ScriptableObject
        {
        }

        [Serializable]
        public sealed class TestSource : UISource<TooltipModel>
        {
            public int CreatedBindings;
            public TestBinding LastBinding;

            public override Type OwnerType => typeof(Owner);

            public override UIBinding<TooltipModel> CreateBinding()
            {
                CreatedBindings++;
                LastBinding = new TestBinding();
                return LastBinding;
            }

            public override TooltipModel CreatePreview(in UIPreviewContext context) => default;
        }

        public sealed class TestBinding : UIBinding<TooltipModel>
        {
            public Object AttachedOwner;
            public Object Subject;

            protected override void OnAttach(Object owner)
            {
                AttachedOwner = owner;
                Subject = Context.Subject;
                Publish(new TooltipModel(owner.name, null, null));
            }

            protected override void OnDetach()
            {
            }
        }

        public sealed class RecordingView : MonoBehaviour, IView<TooltipModel>, ITooltipPlacementTarget
        {
            public TooltipModel Last;
            public RectTransform LastTarget;

            public void Render(in TooltipModel model) => Last = model;

            public void Place(RectTransform target) => LastTarget = target;
        }
    }
}
