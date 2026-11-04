using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI
{
    /// <summary>View de tooltip que se posiciona perto do alvo.</summary>
    public interface ITooltipPlacementTarget
    {
        /// <summary>Posiciona o tooltip perto de <paramref name="target"/>.</summary>
        void Place(RectTransform target);
    }

    /// <summary>
    /// Binder único do tooltip da tela (Spec UI-3, Design 2). Mostra um tooltip por vez: o pedido de outro requester fecha
    /// o atual, e o mesmo requester não reabre. Cada fonte ganha uma ligação, criada na primeira abertura e reaproveitada.
    /// O atraso conta pelo tempo do <see cref="UIUpdateDriver"/>; sem driver, abre na hora. Aberto, fecha se o alvo é
    /// destruído e reposiciona a view só quando o alvo se move. Sem dono (nulo ou destruído), liga a fonte ao próprio
    /// binder: a fonte vê um dono fora do <see cref="UISourceBase.OwnerType"/> e mostra só o que o asset sabe (CA10).
    /// Não tem <c>Update</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(DocUrls.UiTooltip + "#tooltipbinder")]
    public sealed class TooltipBinder : MonoBehaviour, IValidatable, IUIModelReceiver<TooltipModel>
    {
        [SerializeField]
        [Tooltip("Componente que implementa IView<TooltipModel> e desenha o tooltip.")]
        private MonoBehaviour view;

        [SerializeField]
        [Tooltip("Cores por papel e provedor dos textos entregues à fonte. Vazio: padrões do pacote.")]
        private UITheme theme;

        [SerializeField]
        [Tooltip("Driver que conta o atraso e chama o tick. Vazio: o UIUpdateDriver do pai, lido ao habilitar.")]
        private UIUpdateDriver driver;

        private readonly Dictionary<UISourceBase, UIBinding<TooltipModel>> _bindings =
            new Dictionary<UISourceBase, UIBinding<TooltipModel>>();

        private UIUpdateDriver _activeDriver;
        private Ticker _ticker;

        // Pedido atual, esperando o atraso ou aberto
        private object _requester;
        private UISource<TooltipModel> _source;
        private Object _owner;
        private Object _subject;
        private RectTransform _target;
        private bool _hasTarget;
        private bool _pending;
        private double _openAt;
        private UIBinding<TooltipModel> _open;
        private Vector3 _lastTargetPosition;

        /// <summary>A view que recebe o modelo, ou <c>null</c> se o componente não implementa <see cref="IView{TModel}"/>.</summary>
        public IView<TooltipModel> View => view as IView<TooltipModel>;

        /// <summary>O tema entregue à ligação no contexto; pode ser nulo.</summary>
        public UITheme Theme => theme;

        /// <summary>O driver do campo; vazio, o binder usa o do pai.</summary>
        public UIUpdateDriver Driver => driver;

        /// <summary>Verdadeiro com um tooltip aberto.</summary>
        public bool IsOpen => _open != null;

        /// <summary>Troca a view. Um tooltip aberto continua aberto e a nova view recebe o próximo modelo.</summary>
        public void SetView(MonoBehaviour newView)
        {
            view = newView;
        }

        /// <summary>Troca o tema; vale a partir da próxima abertura.</summary>
        public void SetTheme(UITheme newTheme)
        {
            theme = newTheme;
        }

        /// <summary>Troca o driver (nulo vale o do pai). Fecha o tooltip atual se o binder está habilitado.</summary>
        public void SetDriver(UIUpdateDriver newDriver)
        {
            driver = newDriver;
            if (isActiveAndEnabled)
            {
                HideAll();
                LeaveDriver();
                _activeDriver = ResolveDriver();
            }
        }

        /// <summary>
        /// Pede o tooltip de <paramref name="source"/> lido de <paramref name="owner"/>, perto de <paramref name="target"/>,
        /// depois de <paramref name="delay"/> segundos do driver. Requester ou fonte nulos não fazem nada; dono nulo abre
        /// sem dono. O mesmo requester, aberto ou esperando, não refaz nada; outro requester fecha o atual.
        /// </summary>
        public void Show(object requester, UISource<TooltipModel> source, Object owner, Object subject, RectTransform target, float delay)
        {
            if (requester == null || source == null || !isActiveAndEnabled)
            {
                return;
            }

            if (ReferenceEquals(requester, _requester))
            {
                return;
            }

            HideAll();
            _requester = requester;
            _source = source;
            _owner = owner;
            _subject = subject;
            _target = target;
            _hasTarget = target != null;
            if (delay > 0f && _activeDriver != null)
            {
                _pending = true;
                _openAt = _activeDriver.TimeSource.ElapsedTime + delay;
                RequestTick(true);
                return;
            }

            Open();
        }

        /// <summary>Fecha o tooltip ou cancela o pedido esperando, se forem de <paramref name="requester"/>.</summary>
        public void Hide(object requester)
        {
            if (requester != null && ReferenceEquals(requester, _requester))
            {
                HideAll();
            }
        }

        /// <summary>Fecha o tooltip aberto e cancela o pedido esperando, de qualquer requester.</summary>
        public void HideAll()
        {
            bool wasOpen = _open != null;
            if (wasOpen)
            {
                UIBinding<TooltipModel> open = _open;
                _open = null;
                open.Detach();
                if (_activeDriver != null)
                {
                    _activeDriver.Unregister(open);
                }
            }

            _pending = false;
            _requester = null;
            _source = null;
            _owner = null;
            _subject = null;
            _target = null;
            _hasTarget = false;
            RequestTick(false);
            if (wasOpen)
            {
                View?.Render(default);
            }
        }

        /// <summary>Acrescenta os problemas da view: sem view ou view que não desenha <see cref="TooltipModel"/>.</summary>
        public void Validate(ValidationResult result)
        {
            if (view == null)
            {
                result.AddError("Sem view. Ligue o componente que desenha o tooltip.", nameof(view));
            }
            else if (!(view is IView<TooltipModel>))
            {
                result.AddError($"A view {view.GetType().Name} não implementa IView<{nameof(TooltipModel)}>. " +
                    "Ligue um componente que desenhe esse modelo.", nameof(view));
            }
        }

        void IUIModelReceiver<TooltipModel>.Receive(in TooltipModel model) => View?.Render(model);

        private void OnEnable()
        {
            _activeDriver = ResolveDriver();
        }

        private void OnDisable()
        {
            HideAll();
            LeaveDriver();
        }

        private UIUpdateDriver ResolveDriver() => driver != null ? driver : GetComponentInParent<UIUpdateDriver>();

        private void LeaveDriver()
        {
            if (_ticker != null && _activeDriver != null)
            {
                _activeDriver.Unregister(_ticker);
            }

            _activeDriver = null;
        }

        private void Open()
        {
            _pending = false;
            if (!_bindings.TryGetValue(_source, out UIBinding<TooltipModel> binding))
            {
                binding = _source.CreateBinding();
                binding?.SetReceiver(this);
                _bindings.Add(_source, binding);
            }

            // Alvo destruído durante o atraso fecha sem abrir
            if (binding == null || (_hasTarget && _target == null))
            {
                HideAll();
                return;
            }

            // Sem dono, a ligação liga ao binder e a fonte lê só o asset (o Attach recusa o nulo)
            _open = binding;
            binding.Attach(_owner != null ? _owner : this, new UIBindContext(theme != null ? theme.Text : null, null, theme, _subject));
            if (_activeDriver != null)
            {
                _activeDriver.Register(binding);
            }

            if (_hasTarget)
            {
                _lastTargetPosition = _target.position;
                PlaceView();
            }

            RequestTick(_hasTarget);
        }

        private void PlaceView()
        {
            if (view is ITooltipPlacementTarget placement)
            {
                placement.Place(_target);
            }
        }

        private void RequestTick(bool on)
        {
            if (_activeDriver == null)
            {
                return;
            }

            if (on && (_ticker ??= new Ticker(this)).Driver == null)
            {
                _ticker.Attach(this, default);
                _activeDriver.Register(_ticker);
            }

            _ticker?.Request(on);
        }

        private void OnTick(double time)
        {
            if (_pending)
            {
                if (time >= _openAt)
                {
                    Open();
                }

                return;
            }

            if (_open == null || !_hasTarget)
            {
                return;
            }

            // Alvo destruído: o operador == da Unity vê o objeto morto
            if (_target == null)
            {
                HideAll();
                return;
            }

            Vector3 position = _target.position;
            if (position != _lastTargetPosition)
            {
                _lastTargetPosition = position;
                PlaceView();
            }
        }

        // Ligação do próprio binder no driver: repassa o tick ao OnTick
        private sealed class Ticker : UIBinding
        {
            private readonly TooltipBinder _binder;

            public Ticker(TooltipBinder binder)
            {
                _binder = binder;
            }

            public void Request(bool on) => RequestTick(on);

            public override void Tick(double time) => _binder.OnTick(time);

            protected override void OnAttach(Object owner)
            {
            }

            protected override void OnDetach()
            {
            }
        }
    }
}
