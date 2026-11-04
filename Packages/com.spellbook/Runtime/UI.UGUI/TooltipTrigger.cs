using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace SpellBook.UI.UGUI
{
    /// <summary>Quais eventos abrem o tooltip do <see cref="TooltipTrigger"/>.</summary>
    public enum TooltipOpenMode
    {
        /// <summary>O ponteiro entrar abre e sair fecha.</summary>
        Pointer,

        /// <summary>O foco de navegação (teclado, controle) abre e perdê-lo fecha.</summary>
        Focus,

        /// <summary>Ponteiro e foco.</summary>
        PointerAndFocus,

        /// <summary>Só a chamada por código, por <see cref="TooltipTrigger.Show"/> e <see cref="TooltipTrigger.Hide"/>.</summary>
        Code,
    }

    /// <summary>
    /// Gatilho do tooltip num item da tela (Spec UI-3, Design 3): pede o tooltip ao <see cref="TooltipBinder"/> da âncora
    /// quando o ponteiro entra ou o item ganha o foco, pelos eventos da uGUI, e o fecha quando sai, perde o foco ou é
    /// desativado. <see cref="Show"/> e <see cref="Hide"/> fazem o mesmo por código, em qualquer modo. Com um
    /// <see cref="UIItemContext"/> no objeto, o dono e o assunto vêm dele; senão, o dono vem do resolvedor.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(DocUrls.UiTooltip + "#tooltiptrigger")]
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeReference, TypePicker]
        [Tooltip("O que o tooltip mostra.")]
        private UISource<TooltipModel> source;

        [SerializeField]
        [Tooltip("Onde achar o dono lido pela fonte. Com um UIItemContext no objeto, vale o dono do item.")]
        private ActorResolver owner = ActorResolver.ForSelfOrParent();

        [SerializeField]
        [Tooltip("Quais eventos abrem o tooltip. Show() e Hide() funcionam em qualquer modo.")]
        private TooltipOpenMode mode = TooltipOpenMode.PointerAndFocus;

        [SerializeField]
        [Tooltip("Segundos, pelo driver do binder, entre o pedido e a abertura.")]
        private float delay = 0.3f;

        [SerializeField]
        [Tooltip("Âncora em que o TooltipBinder da tela se publica.")]
        private TooltipBinderAnchor binder;

        [SerializeField]
        [Tooltip("Retângulo ao lado do qual o tooltip abre. Vazio: o deste objeto.")]
        private RectTransform target;

        private UIItemContext _item;
        private RectTransform _ownRect;
        private TooltipBinder _shownOn;

        /// <summary>A fonte do tooltip.</summary>
        public UISource<TooltipModel> Source => source;

        /// <summary>O resolvedor do dono, usado sem <see cref="UIItemContext"/> no objeto.</summary>
        public ActorResolver Owner => owner;

        /// <summary>Quais eventos abrem o tooltip.</summary>
        public TooltipOpenMode Mode => mode;

        /// <summary>O atraso da abertura, em segundos do driver.</summary>
        public float Delay => delay;

        /// <summary>A âncora do binder.</summary>
        public TooltipBinderAnchor Binder => binder;

        /// <summary>O alvo configurado; vazio, o tooltip usa o <see cref="RectTransform"/> deste objeto.</summary>
        public RectTransform Target => target;

        /// <summary>Troca a fonte; vale a partir do próximo <see cref="Show"/>.</summary>
        public void SetSource(UISource<TooltipModel> newSource)
        {
            source = newSource;
        }

        /// <summary>Troca o resolvedor do dono (nulo vale <see cref="ActorResolver.ForSelfOrParent"/>).</summary>
        public void SetOwner(ActorResolver resolver)
        {
            owner = resolver ?? ActorResolver.ForSelfOrParent();
        }

        /// <summary>Troca os eventos que abrem o tooltip.</summary>
        public void SetMode(TooltipOpenMode newMode)
        {
            mode = newMode;
        }

        /// <summary>Troca o atraso da abertura, em segundos do driver.</summary>
        public void SetDelay(float seconds)
        {
            delay = seconds;
        }

        /// <summary>Troca a âncora do binder; vale a partir do próximo <see cref="Show"/>.</summary>
        public void SetBinder(TooltipBinderAnchor anchor)
        {
            binder = anchor;
        }

        /// <summary>Troca o alvo (nulo vale o <see cref="RectTransform"/> deste objeto).</summary>
        public void SetTarget(RectTransform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Pede o tooltip ao binder da âncora, depois do atraso, e fecha o que este gatilho abriu em outro binder. Desativado,
        /// sem fonte, sem âncora ou sem binder publicado, não faz nada.
        /// </summary>
        public void Show()
        {
            TooltipBinder tooltip = binder != null ? binder.Value : null;
            if (!isActiveAndEnabled || source == null || tooltip == null)
            {
                return;
            }

            if (_shownOn != null && _shownOn != tooltip)
            {
                _shownOn.Hide(this);
            }

            Object ownerObject;
            Object subject = null;
            if (_item != null)
            {
                ownerObject = _item.Owner;
                subject = _item.Subject;
            }
            else
            {
                owner.TryResolve(this, source.OwnerType, out ownerObject);
            }

            _shownOn = tooltip;
            tooltip.Show(this, source, ownerObject, subject, target != null ? target : _ownRect, delay);
        }

        /// <summary>Fecha o tooltip deste gatilho, ou cancela o pedido esperando.</summary>
        public void Hide()
        {
            if (_shownOn != null)
            {
                _shownOn.Hide(this);
            }

            _shownOn = null;
        }

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
        {
            if (OpensByPointer)
            {
                Show();
            }
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
        {
            if (OpensByPointer)
            {
                Hide();
            }
        }

        void ISelectHandler.OnSelect(BaseEventData eventData)
        {
            if (OpensByFocus)
            {
                Show();
            }
        }

        void IDeselectHandler.OnDeselect(BaseEventData eventData)
        {
            if (OpensByFocus)
            {
                Hide();
            }
        }

        private bool OpensByPointer => mode == TooltipOpenMode.Pointer || mode == TooltipOpenMode.PointerAndFocus;

        private bool OpensByFocus => mode == TooltipOpenMode.Focus || mode == TooltipOpenMode.PointerAndFocus;

        private void Awake()
        {
            TryGetComponent(out _item);
            _ownRect = transform as RectTransform;
        }

        // Também roda antes de destruir o item
        private void OnDisable()
        {
            Hide();
        }
    }
}
