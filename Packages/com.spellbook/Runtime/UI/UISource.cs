using System;
using System.Collections.Generic;
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

    /// <summary>O que a ligação recebe ao ligar: o provedor de texto, a cultura dos números e o tema (ADR 0039, item 4).</summary>
    public readonly struct UIBindContext
    {
        /// <summary>
        /// Cria o contexto. Sem <paramref name="culture"/>, vale a do provedor e, sem ele, a invariante.
        /// <paramref name="theme"/> pode ser nulo.
        /// </summary>
        public UIBindContext(ITextProvider text, IFormatProvider culture = null, UITheme theme = null)
        {
            Theme = theme;
            Text = text;
            Culture = culture ?? text?.Culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>Provedor dos textos; pode ser nulo (textos padrão).</summary>
        public ITextProvider Text { get; }

        /// <summary>Cultura dos números; nunca nula num contexto criado pelo construtor.</summary>
        public IFormatProvider Culture { get; }

        /// <summary>Tema do binder, ou nulo sem tema.</summary>
        public UITheme Theme { get; }
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

    /// <summary>Recebe os itens que uma ligação de lista publica. O binder de lista implementa (ADR 0039, item 6).</summary>
    /// <typeparam name="TItem">Modelo de cada item.</typeparam>
    public interface IUIListReceiver<TItem> where TItem : struct
    {
        /// <summary>Acrescenta o item <paramref name="id"/> no fim da lista.</summary>
        void AddItem(int id, in TItem item);

        /// <summary>Muda o conteúdo do item <paramref name="id"/>, sem mudar a posição.</summary>
        void UpdateItem(int id, in TItem item);

        /// <summary>Tira o item <paramref name="id"/> da lista.</summary>
        void RemoveItem(int id);

        /// <summary>Tira todos os itens.</summary>
        void ClearItems();
    }

    /// <summary>
    /// Fonte que lê um sistema para um dono e produz uma lista de <typeparamref name="TItem"/>, cada item com um
    /// identificador inteiro estável (ADR 0039, itens 2 e 6).
    /// </summary>
    /// <typeparam name="TItem">Modelo de cada item.</typeparam>
    [Serializable]
    public abstract class UIListSource<TItem> : UISourceBase where TItem : struct
    {
        /// <summary>Cria a ligação de um binder de lista. O binder chama uma vez e reaproveita a ligação a cada troca de dono.</summary>
        public abstract UIListBinding<TItem> CreateBinding();

        /// <summary>Acrescenta a <paramref name="items"/>, já vazia, os itens mostrados em edição, fora do Play.</summary>
        public abstract void CreatePreview(in UIPreviewContext context, List<TItem> items);
    }

    /// <summary>
    /// Estado da ligação entre uma fonte e um dono (ADR 0039, item 1). O ciclo é <see cref="Attach"/> e
    /// <see cref="Detach"/>, repetido a cada troca de dono na mesma instância.
    /// </summary>
    public abstract class UIBinding
    {
        /// <summary>Verdadeiro enquanto a ligação pede <see cref="Tick"/> ao driver; o driver registrado é avisado da troca.</summary>
        public bool WantsTick { get; private set; }

        /// <summary>Verdadeiro entre <see cref="Attach"/> e <see cref="Detach"/>.</summary>
        public bool IsAttached { get; private set; }

        /// <summary>Contexto recebido no último <see cref="Attach"/>.</summary>
        protected UIBindContext Context { get; private set; }

        // Driver em que a ligação está registrada; avisado quando WantsTick muda
        internal UIUpdateDriver Driver { get; set; }

        // Dono do Attach atual; o binder de lista o escreve no UIItemContext de cada item
        internal UnityEngine.Object Owner { get; private set; }

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
            Owner = owner;
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
            Owner = null;
            SetWantsTick(false);
        }

        /// <summary>Chamado pelo driver enquanto <see cref="WantsTick"/> é verdadeiro, com o tempo da fonte de tempo.</summary>
        public virtual void Tick(double time)
        {
        }

        /// <summary>Pede (<c>true</c>) ou solta (<c>false</c>) o tick. Fora de ligação, não pede.</summary>
        protected void RequestTick(bool on)
        {
            SetWantsTick(on && IsAttached);
        }

        /// <summary>Assina os eventos de <paramref name="owner"/> e publica o estado atual.</summary>
        protected abstract void OnAttach(UnityEngine.Object owner);

        /// <summary>Desassina o que <see cref="OnAttach"/> assinou.</summary>
        protected abstract void OnDetach();

        private void SetWantsTick(bool value)
        {
            if (WantsTick == value)
            {
                return;
            }

            WantsTick = value;
            if (Driver != null)
            {
                Driver.OnWantsTickChanged(this);
            }
        }
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

    /// <summary>
    /// Ligação de lista: acrescenta, muda e remove itens por um identificador inteiro estável; a ordem é a de chegada
    /// (ADR 0039, item 6). Fora de ligação ou sem receptor, os métodos não fazem nada.
    /// </summary>
    /// <typeparam name="TItem">Modelo de cada item.</typeparam>
    public abstract class UIListBinding<TItem> : UIBinding where TItem : struct
    {
        private IUIListReceiver<TItem> _receiver;

        /// <summary>Define quem recebe os itens; o binder chama uma vez, ao criar a ligação.</summary>
        public void SetReceiver(IUIListReceiver<TItem> receiver)
        {
            _receiver = receiver;
        }

        /// <summary>Acrescenta o item <paramref name="id"/> no fim; um id já presente só muda o conteúdo.</summary>
        protected void AddItem(int id, in TItem item)
        {
            if (IsAttached)
            {
                _receiver?.AddItem(id, item);
            }
        }

        /// <summary>Muda o conteúdo do item <paramref name="id"/>; um id ausente não faz nada.</summary>
        protected void UpdateItem(int id, in TItem item)
        {
            if (IsAttached)
            {
                _receiver?.UpdateItem(id, item);
            }
        }

        /// <summary>Tira o item <paramref name="id"/>; um id ausente não faz nada.</summary>
        protected void RemoveItem(int id)
        {
            if (IsAttached)
            {
                _receiver?.RemoveItem(id);
            }
        }

        /// <summary>Tira todos os itens.</summary>
        protected void ClearItems()
        {
            if (IsAttached)
            {
                _receiver?.ClearItems();
            }
        }
    }
}
