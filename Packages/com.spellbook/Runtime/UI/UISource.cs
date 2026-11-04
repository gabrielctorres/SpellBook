using System;
using System.Globalization;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;

namespace SpellBook.UI
{
    /// <summary>
    /// Desenha um modelo (ADR 0017 e 0039, item 3). Recebe só o modelo: não conhece a fonte, o dono nem o sistema lido.
    /// </summary>
    /// <typeparam name="TModel">Modelo desenhado, um <c>struct</c>.</typeparam>
    public interface IView<TModel> where TModel : struct
    {
        /// <summary>Desenha <paramref name="model"/>.</summary>
        void Render(in TModel model);
    }

    /// <summary>Recebe os modelos que uma ligação publica. O binder implementa e repassa à view (ADR 0039, item 1).</summary>
    /// <typeparam name="TModel">Modelo publicado.</typeparam>
    public interface IUIModelReceiver<TModel> where TModel : struct
    {
        /// <summary>Chamado a cada <c>Publish</c> da ligação ligada.</summary>
        void Receive(in TModel model);
    }

    /// <summary>O que a ligação recebe ao ligar: o provedor de texto e a cultura dos números (ADR 0039, item 4).</summary>
    public readonly struct UIBindContext
    {
        /// <summary>Cria o contexto. Sem <paramref name="culture"/>, vale a do provedor e, sem ele, a invariante.</summary>
        public UIBindContext(ITextProvider text, IFormatProvider culture = null)
        {
            Text = text;
            Culture = culture ?? text?.Culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>Provedor dos textos; pode ser nulo (textos padrão).</summary>
        public ITextProvider Text { get; }

        /// <summary>Cultura dos números; nunca nula num contexto criado pelo construtor.</summary>
        public IFormatProvider Culture { get; }
    }

    /// <summary>O que a fonte recebe para montar o modelo de prévia em edição (ADR 0039, item 7).</summary>
    public readonly struct UIPreviewContext
    {
        /// <summary>Cria o contexto. Sem <paramref name="culture"/>, vale a do provedor e, sem ele, a invariante.</summary>
        public UIPreviewContext(int level, ITextProvider text = null, IFormatProvider culture = null)
        {
            Level = level;
            Text = text;
            Culture = culture ?? text?.Culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>Nível mostrado na prévia.</summary>
        public int Level { get; }

        /// <summary>Provedor dos textos; pode ser nulo (textos padrão).</summary>
        public ITextProvider Text { get; }

        /// <summary>Cultura dos números; nunca nula num contexto criado pelo construtor.</summary>
        public IFormatProvider Culture { get; }
    }

    /// <summary>
    /// Base não genérica das fontes de UI (ADR 0013 e 0039, item 2). A fonte guarda só configuração; o estado fica na
    /// ligação que ela cria.
    /// </summary>
    [Serializable]
    public abstract class UISourceBase
    {
        /// <summary>Tipo do dono que a fonte lê, procurado pelo <see cref="ActorResolver"/>.</summary>
        public abstract Type OwnerType { get; }

        /// <summary>Acrescenta ao <paramref name="result"/> os problemas de configuração, no caminho <paramref name="propertyPath"/>.</summary>
        public virtual void Validate(ValidationResult result, string propertyPath)
        {
        }
    }

    /// <summary>Fonte que lê um sistema para um dono e produz <typeparamref name="TModel"/> (ADR 0039, itens 1 e 2).</summary>
    /// <typeparam name="TModel">Modelo produzido.</typeparam>
    [Serializable]
    public abstract class UISource<TModel> : UISourceBase where TModel : struct
    {
        /// <summary>Cria a ligação de um binder. O binder chama uma vez e reaproveita a ligação a cada troca de dono.</summary>
        public abstract UIBinding<TModel> CreateBinding();

        /// <summary>Modelo mostrado em edição, fora do Play.</summary>
        public abstract TModel CreatePreview(in UIPreviewContext context);
    }

    /// <summary>
    /// Estado da ligação entre uma fonte e um dono (ADR 0039, item 1). O ciclo é <see cref="Attach"/> e
    /// <see cref="Detach"/>, repetido a cada troca de dono na mesma instância.
    /// </summary>
    public abstract class UIBinding
    {
        /// <summary>Verdadeiro enquanto a ligação pede <see cref="Tick"/> ao driver.</summary>
        public bool WantsTick { get; private set; }

        /// <summary>Verdadeiro entre <see cref="Attach"/> e <see cref="Detach"/>.</summary>
        public bool IsAttached { get; private set; }

        /// <summary>Contexto recebido no último <see cref="Attach"/>.</summary>
        protected UIBindContext Context { get; private set; }

        /// <summary>
        /// Liga a <paramref name="owner"/>. Se já estiver ligada, desliga antes. A subclasse assina os eventos e publica o
        /// estado atual em <see cref="OnAttach"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> é nulo ou foi destruído.</exception>
        public void Attach(UnityEngine.Object owner, in UIBindContext context)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            Detach();
            Context = context;
            IsAttached = true;
            OnAttach(owner);
        }

        /// <summary>Desliga e solta o tick. Sem efeito se não estiver ligada.</summary>
        public void Detach()
        {
            if (!IsAttached)
            {
                return;
            }

            OnDetach();
            IsAttached = false;
            WantsTick = false;
        }

        /// <summary>Chamado pelo driver enquanto <see cref="WantsTick"/> é verdadeiro, com o tempo da fonte de tempo.</summary>
        public virtual void Tick(double time)
        {
        }

        /// <summary>Pede (<c>true</c>) ou solta (<c>false</c>) o tick. Fora de ligação, não pede.</summary>
        protected void RequestTick(bool on)
        {
            WantsTick = on && IsAttached;
        }

        /// <summary>Assina os eventos de <paramref name="owner"/> e publica o estado atual.</summary>
        protected abstract void OnAttach(UnityEngine.Object owner);

        /// <summary>Desassina o que <see cref="OnAttach"/> assinou.</summary>
        protected abstract void OnDetach();
    }

    /// <summary>Ligação que publica <typeparamref name="TModel"/> para um receptor.</summary>
    /// <typeparam name="TModel">Modelo publicado.</typeparam>
    public abstract class UIBinding<TModel> : UIBinding where TModel : struct
    {
        private IUIModelReceiver<TModel> _receiver;

        /// <summary>Define quem recebe os modelos; o binder chama uma vez, ao criar a ligação.</summary>
        public void SetReceiver(IUIModelReceiver<TModel> receiver)
        {
            _receiver = receiver;
        }

        /// <summary>Entrega <paramref name="model"/> ao receptor. Fora de ligação ou sem receptor, não faz nada.</summary>
        protected void Publish(in TModel model)
        {
            if (IsAttached)
            {
                _receiver?.Receive(model);
            }
        }
    }
}
