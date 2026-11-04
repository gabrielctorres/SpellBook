using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder único do tooltip da tela (Spec UI-3, Design 2). Mostra um tooltip por vez: o pedido de outro requester fecha
    /// o atual, e o mesmo requester não reabre. Cada fonte ganha uma ligação, criada na primeira abertura e reaproveitada,
    /// e registrada no <see cref="UIUpdateDriver"/> enquanto aberta. Não tem <c>Update</c>.
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
        [Tooltip("Driver que chama o tick da ligação aberta. Vazio: o UIUpdateDriver do pai, lido ao habilitar.")]
        private UIUpdateDriver driver;

        private readonly Dictionary<UISourceBase, UIBinding<TooltipModel>> _bindings =
            new Dictionary<UISourceBase, UIBinding<TooltipModel>>();

        private UIUpdateDriver _activeDriver;

        // Requester do tooltip aberto e a ligação dele
        private object _requester;
        private UIBinding<TooltipModel> _open;

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
        /// Abre o tooltip de <paramref name="source"/> lido de <paramref name="owner"/>, com <paramref name="subject"/> no
        /// contexto da ligação. Requester, fonte ou dono nulos não fazem nada. O mesmo requester aberto não refaz nada;
        /// outro requester fecha o atual.
        /// </summary>
        public void Show(object requester, UISource<TooltipModel> source, Object owner, Object subject)
        {
            if (requester == null || source == null || owner == null || !isActiveAndEnabled)
            {
                return;
            }

            if (ReferenceEquals(requester, _requester))
            {
                return;
            }

            HideAll();
            UIBinding<TooltipModel> binding = AcquireBinding(source);
            if (binding == null)
            {
                return;
            }

            _requester = requester;
            _open = binding;
            binding.Attach(owner, new UIBindContext(theme != null ? theme.Text : null, null, theme, subject));
            if (_activeDriver != null)
            {
                _activeDriver.Register(binding);
            }
        }

        /// <summary>Fecha o tooltip, se for de <paramref name="requester"/>.</summary>
        public void Hide(object requester)
        {
            if (requester != null && ReferenceEquals(requester, _requester))
            {
                HideAll();
            }
        }

        /// <summary>Fecha o tooltip aberto, de qualquer requester.</summary>
        public void HideAll()
        {
            _requester = null;
            if (_open == null)
            {
                return;
            }

            UIBinding<TooltipModel> open = _open;
            _open = null;
            open.Detach();
            if (_activeDriver != null)
            {
                _activeDriver.Unregister(open);
            }

            View?.Render(default);
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
            _activeDriver = null;
        }

        private UIBinding<TooltipModel> AcquireBinding(UISource<TooltipModel> source)
        {
            if (!_bindings.TryGetValue(source, out UIBinding<TooltipModel> binding))
            {
                binding = source.CreateBinding();
                binding?.SetReceiver(this);
                _bindings.Add(source, binding);
            }

            return binding;
        }
    }
}
