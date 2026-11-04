using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI
{
    /// <summary>
    /// Base não genérica dos binders (ADR 0017 e 0039, itens 1, 5 e 7). No Play, acha o dono pelo
    /// <see cref="ActorResolver"/> ao habilitar, liga a fonte a ele e religa quando a âncora muda; dono ausente mostra o
    /// estado vazio, sem exceção. A ligação recebe o tema no contexto e é registrada no <see cref="UIUpdateDriver"/>, que a
    /// chama só enquanto ela pede tick. Fora do Play, desenha a prévia da fonte e não liga nada. Não tem <c>Update</c>.
    /// </summary>
    [ExecuteAlways]
    public abstract class UIBinderBase : MonoBehaviour, IValidatable
    {
        [SerializeField]
        [Tooltip("Onde achar o dono que a fonte lê.")]
        private ActorResolver owner = ActorResolver.ForSelfOrParent();

        [SerializeField]
        [Tooltip("Cores por papel e provedor dos textos entregues à fonte. Vazio: padrões do pacote.")]
        private UITheme theme;

        [SerializeField]
        [Tooltip("Driver que chama o tick da ligação. Vazio: o UIUpdateDriver do pai, lido ao habilitar.")]
        private UIUpdateDriver driver;

        private Action<RuntimeAnchor> _onAnchorChanged;
        private UIUpdateDriver _activeDriver;
        private UIBinding _registered;
        private BinderTicker _ticker;
        private RuntimeAnchor _subscribedAnchor;
        private bool _bound;
        private bool _warnedMissingOwner;

        /// <summary>Como o binder acha o dono.</summary>
        public ActorResolver Owner => owner;

        /// <summary>O tema entregue à ligação no contexto; pode ser nulo.</summary>
        public UITheme Theme => theme;

        /// <summary>O driver do campo; vazio, o binder usa o do pai.</summary>
        public UIUpdateDriver Driver => driver;

        /// <summary>Troca o tema e religa, para a ligação recebê-lo no contexto.</summary>
        public void SetTheme(UITheme newTheme)
        {
            theme = newTheme;
            Rebind();
        }

        /// <summary>Troca o resolvedor do dono (nulo vale <see cref="ActorResolver.ForSelfOrParent"/>) e religa.</summary>
        public void SetOwner(ActorResolver resolver)
        {
            owner = resolver ?? ActorResolver.ForSelfOrParent();
            Rebind();
        }

        /// <summary>
        /// No Play, desliga e liga de novo na mesma ligação, procurando o dono outra vez. Fora do Play, redesenha a
        /// prévia. Sem efeito com o binder desabilitado.
        /// </summary>
        public void Rebind()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                DrawPreview();
                return;
            }

            Unbind();
            Bind();
        }

        /// <summary>
        /// Acrescenta os problemas do resolvedor, conferido contra o tipo do dono pedido pela fonte. Quem sobrescreve chama
        /// a base.
        /// </summary>
        public virtual void Validate(ValidationResult result)
        {
            owner.Validate(result, this, SourceOwnerType, nameof(owner));
        }

        /// <summary>No Play, liga ao dono; fora dele, desenha a prévia. Quem sobrescreve chama a base.</summary>
        protected virtual void OnEnable()
        {
            if (Application.isPlaying)
            {
                _activeDriver = driver != null ? driver : GetComponentInParent<UIUpdateDriver>();
                Bind();
            }
            else
            {
                DrawPreview();
            }
        }

        /// <summary>Desliga e solta a âncora. Quem sobrescreve chama a base.</summary>
        protected virtual void OnDisable()
        {
            if (_bound)
            {
                Unbind();
            }
        }

        /// <summary>
        /// Pede (<c>true</c>) ou solta (<c>false</c>) o tick do próprio binder ao driver ativo, para animações do binder
        /// como o rastro da barra. Devolve <c>false</c> sem driver ativo ou fora de ligação: o binder não anima.
        /// </summary>
        protected bool RequestBinderTick(bool on)
        {
            if (!_bound || _activeDriver == null)
            {
                return false;
            }

            if (on && !(_ticker ??= new BinderTicker(this)).IsAttached)
            {
                _ticker.Attach(this, default);
                _activeDriver.Register(_ticker);
            }

            _ticker?.Request(on);
            return true;
        }

        /// <summary>Chamado pelo driver enquanto o binder pede tick, com o tempo da fonte de tempo.</summary>
        protected virtual void OnBinderTick(double time)
        {
        }

        // Tipo do dono pedido pela fonte; nulo sem fonte
        private protected abstract Type SourceOwnerType { get; }

        // A ligação da fonte atual, criada uma vez por fonte
        private protected abstract UIBinding AcquireBinding();

        private protected abstract void DetachBinding();

        private protected abstract void ShowEmpty();

        private protected abstract void DrawPreview();

        private void Bind()
        {
            _bound = true;
            if (owner.Mode == ActorResolveMode.FromAnchor && owner.Anchor != null)
            {
                _onAnchorChanged ??= OnAnchorChanged;
                _subscribedAnchor = owner.Anchor;
                _subscribedAnchor.Changed += _onAnchorChanged;
            }

            AttachToOwner();
        }

        private void Unbind()
        {
            _bound = false;
            if (_subscribedAnchor != null)
            {
                _subscribedAnchor.Changed -= _onAnchorChanged;
                _subscribedAnchor = null;
            }

            LeaveDriver();
            DetachBinding();
            if (_ticker != null)
            {
                _ticker.Detach();
                if (_activeDriver != null)
                {
                    _activeDriver.Unregister(_ticker);
                }
            }
        }

        private void OnAnchorChanged(RuntimeAnchor anchor)
        {
            DetachBinding();
            AttachToOwner();
        }

        private void AttachToOwner()
        {
            // Sem fonte, o estado vazio vem sem aviso: a regra é do validador
            Type ownerType = SourceOwnerType;
            Object found = null;
            if (ownerType != null && !owner.TryResolve(this, ownerType, out found))
            {
                WarnMissingOwner(ownerType);
            }

            UIBinding binding = found != null ? AcquireBinding() : null;
            if (binding == null)
            {
                ShowEmpty();
                return;
            }

            binding.Attach(found, new UIBindContext(theme != null ? theme.Text : null, null, theme));
            if (_activeDriver != null)
            {
                _activeDriver.Register(binding);
                _registered = binding;
            }
        }

        private void LeaveDriver()
        {
            if (_registered != null && _activeDriver != null)
            {
                _activeDriver.Unregister(_registered);
            }

            _registered = null;
        }

        private void WarnMissingOwner(Type ownerType)
        {
            // Âncora sem valor é o ator que ainda não nasceu: o binder espera o Changed sem avisar
            bool waitingForAnchor = owner.Mode == ActorResolveMode.FromAnchor && owner.Anchor != null
                && owner.Anchor.UntypedValue == null;
            if (waitingForAnchor || _warnedMissingOwner || !Debug.isDebugBuild)
            {
                return;
            }

            _warnedMissingOwner = true;
            Debug.LogWarning($"[UI] '{name}': dono do tipo {ownerType.Name} não encontrado (modo {owner.Mode}); a view mostra o estado vazio.", this);
        }

        // Ligação do próprio binder no driver: repassa o tick ao OnBinderTick
        private sealed class BinderTicker : UIBinding
        {
            private readonly UIBinderBase _binder;

            public BinderTicker(UIBinderBase binder)
            {
                _binder = binder;
            }

            public void Request(bool on) => RequestTick(on);

            public override void Tick(double time) => _binder.OnBinderTick(time);

            protected override void OnAttach(Object owner)
            {
            }

            protected override void OnDetach()
            {
            }
        }
    }

    /// <summary>
    /// Binder de um modelo (ADR 0039, itens 2 e 3): liga a <see cref="UISource{TModel}"/> ao dono e entrega cada modelo
    /// à <see cref="IView{TModel}"/>. A ligação é criada uma vez e reaproveitada a cada troca de dono. O estado vazio é
    /// <c>default(TModel)</c>.
    /// </summary>
    /// <typeparam name="TModel">Modelo desenhado.</typeparam>
    public abstract class UIBinder<TModel> : UIBinderBase, IUIModelReceiver<TModel> where TModel : struct
    {
        [SerializeReference, TypePicker]
        [Tooltip("O que o binder lê do dono.")]
        private UISource<TModel> source;

        [SerializeField]
        [Tooltip("Componente que implementa IView do modelo e o desenha.")]
        private MonoBehaviour view;

        private UIBinding<TModel> _binding;
        private UISource<TModel> _bindingSource;
        private IView<TModel> _view;
        private MonoBehaviour _viewComponent;

        /// <summary>A fonte do modelo.</summary>
        public UISource<TModel> Source => source;

        /// <summary>A view que recebe o modelo, ou <c>null</c> se o componente não implementa <see cref="IView{TModel}"/>.</summary>
        public IView<TModel> View
        {
            get
            {
                if (!ReferenceEquals(_viewComponent, view))
                {
                    _viewComponent = view;
                    _view = view as IView<TModel>;
                }

                return _view;
            }
        }

        /// <summary>Troca a fonte e religa (em edição, redesenha a prévia). A ligação da fonte antiga é descartada.</summary>
        public void SetSource(UISource<TModel> newSource)
        {
            if (ReferenceEquals(source, newSource))
            {
                return;
            }

            source = newSource;
            Rebind();
        }

        /// <summary>Troca a view e religa, para a nova receber o estado atual (em edição, redesenha a prévia).</summary>
        public void SetView(IView<TModel> newView)
        {
            view = newView as MonoBehaviour;
            _viewComponent = view;
            _view = newView;
            Rebind();
        }

        /// <summary>
        /// Diz se o campo da view é obrigatório. Binder que desenha por outro caminho, como os números flutuantes, devolve
        /// <c>false</c>.
        /// </summary>
        protected virtual bool RequiresView => true;

        /// <summary>
        /// Acrescenta os problemas do resolvedor, da fonte (no caminho <c>source</c>) e da view: sem fonte, sem view ou view
        /// que não desenha <typeparamref name="TModel"/>.
        /// </summary>
        public override void Validate(ValidationResult result)
        {
            if (source == null)
            {
                result.AddError("Sem fonte. Escolha o que o binder lê do dono.", nameof(source));
            }
            else
            {
                source.Validate(result, nameof(source));
            }

            base.Validate(result);
            if (view == null)
            {
                if (RequiresView)
                {
                    result.AddError("Sem view. Ligue o componente que desenha o modelo.", nameof(view));
                }
            }
            else if (!(view is IView<TModel>))
            {
                result.AddError($"A view {view.GetType().Name} não implementa IView<{typeof(TModel).Name}>. " +
                    "Ligue um componente que desenhe esse modelo.", nameof(view));
            }
        }

        /// <summary>Recebe cada modelo publicado; por padrão, entrega à <see cref="View"/>.</summary>
        protected virtual void OnModel(in TModel model) => View?.Render(model);

        void IUIModelReceiver<TModel>.Receive(in TModel model) => OnModel(model);

        private protected override Type SourceOwnerType => source?.OwnerType;

        private protected override UIBinding AcquireBinding()
        {
            if (_binding == null || !ReferenceEquals(_bindingSource, source))
            {
                _binding?.Detach();
                _bindingSource = source;
                _binding = source.CreateBinding();
                _binding?.SetReceiver(this);
            }

            return _binding;
        }

        private protected override void DetachBinding() => _binding?.Detach();

        private protected override void ShowEmpty() => OnModel(default);

        private protected override void DrawPreview()
        {
            TModel preview = source != null ? source.CreatePreview(new UIPreviewContext(1)) : default;
            OnModel(preview);
        }
    }
}
