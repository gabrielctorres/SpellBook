using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>O que o texto de uma fonte de atributo mostra.</summary>
    public enum AttributeTextMode
    {
        /// <summary>Só o valor atual ("50").</summary>
        Current,

        /// <summary>O atual, o separador e o máximo ("50 / 100").</summary>
        CurrentOfMax,
    }

    /// <summary>
    /// Base das fontes de atributo do ASC dono (R8): lê o atual e o máximo e escreve o texto no formato do atributo, com a
    /// cultura e o provedor do contexto, sem rich text. Atualiza pelo <c>OnValueChanged</c> do GAS, sem <c>Update</c>.
    /// Sem <see cref="Max"/>, o máximo é o efetivo do atributo (constante ou fonte de limite). <c>Hidden</c> não impede a
    /// leitura. A configuração vale a partir da próxima ligação.
    /// </summary>
    /// <typeparam name="TModel">Modelo produzido.</typeparam>
    [Serializable]
    public abstract class AttributeSource<TModel> : UISource<TModel> where TModel : struct
    {
        private const float PreviewCurrent = 50f;
        private const float PreviewMax = 100f;

        [Tooltip("Atributo lido do ASC dono.")]
        [SerializeField] private AttributeDefinition current;

        [Tooltip("Atributo do máximo. Vazio: o máximo efetivo do atributo lido (constante ou fonte de limite).")]
        [SerializeField] private AttributeDefinition max;

        [Tooltip("Só o valor atual, ou o atual, o separador e o máximo.")]
        [SerializeField] private AttributeTextMode textMode;

        [Tooltip("Texto entre o atual e o máximo.")]
        [SerializeField] private LocalizedText separator = new LocalizedText(null, " / ");

        [NonSerialized] private TextBuffer _preview;

        private protected AttributeSource(AttributeTextMode defaultMode)
        {
            textMode = defaultMode;
        }

        /// <summary>Atributo lido.</summary>
        public AttributeDefinition Current { get => current; set => current = value; }

        /// <summary>Atributo do máximo; nulo usa o máximo efetivo de <see cref="Current"/>.</summary>
        public AttributeDefinition Max { get => max; set => max = value; }

        /// <summary>O que o texto mostra.</summary>
        public AttributeTextMode TextMode { get => textMode; set => textMode = value; }

        /// <summary>Texto entre o atual e o máximo; o padrão é <c>" / "</c>.</summary>
        public LocalizedText Separator { get => separator; set => separator = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<TModel> CreateBinding() => new Binding(this);

        /// <summary>Sem atributo, erro; máximo igual ao atributo lido, aviso.</summary>
        public override void Validate(ValidationResult result, string propertyPath)
        {
            if (current == null)
            {
                result.AddError("Fonte sem atributo. Escolha o atributo lido do dono.", propertyPath + "." + nameof(current));
            }
            else if (max == current)
            {
                result.AddWarning("O máximo é o próprio atributo, e a barra fica sempre cheia. Escolha outro atributo ou " +
                    "deixe vazio para usar o máximo efetivo.", propertyPath + "." + nameof(max));
            }
        }

        /// <summary>50 de 100, no formato de <see cref="Current"/>.</summary>
        public override TModel CreatePreview(in UIPreviewContext context)
        {
            AttributeDisplay display = current != null ? current.Display : default;
            TextSpan text = Write(_preview ??= new TextBuffer(32), display, PreviewCurrent, PreviewMax, context.Text, context.Culture);
            return Build(PreviewCurrent, PreviewMax, text, display.Role);
        }

        /// <summary>Monta o modelo com os valores, o texto já escrito e o papel do atributo.</summary>
        private protected abstract TModel Build(float currentValue, float maxValue, TextSpan text, TextRole role);

        // Escreve do zero o atual e, em CurrentOfMax, o separador pelo provedor e o máximo, os dois no formato do atributo
        private TextSpan Write(
            TextBuffer buffer, AttributeDisplay display, float value, float maxValue, ITextProvider text, IFormatProvider culture)
        {
            buffer.Clear();
            display.Format(buffer, value, culture);
            if (textMode == AttributeTextMode.CurrentOfMax)
            {
                buffer.Append(separator.Resolve(text));
                display.Format(buffer, maxValue, culture);
            }

            return buffer.SpanFrom(0);
        }

        // Assina o OnValueChanged do atributo e do máximo (o explícito ou a fonte de limite do AR1). O delegate e o buffer
        // são criados uma vez; ligar, desligar e publicar não alocam
        private sealed class Binding : UIBinding<TModel>
        {
            private readonly AttributeSource<TModel> _source;
            private readonly TextBuffer _buffer = new TextBuffer(32);
            private readonly Action<float, float> _onValueChanged;

            private AttributeState _current;
            private AttributeState _explicitMax;
            private AttributeState _listenedMax;

            public Binding(AttributeSource<TModel> source)
            {
                _source = source;
                _onValueChanged = (_, _) => Refresh();
            }

            // Sem ASC, sem o atributo ou sem o máximo explícito no AttributeSet: estado vazio, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                AttributeSet set = owner is AbilitySystemComponent asc ? asc.AttributeSet : null;
                AttributeState max = null;
                if (set == null || !set.TryGetState(_source.current, out AttributeState state)
                    || (_source.max != null && !set.TryGetState(_source.max, out max)))
                {
                    Publish(default);
                    return;
                }

                _current = state;
                _explicitMax = max;
                if (max == null && state.Definition.MaxSource == LimitSource.Attribute)
                {
                    set.TryGetState(state.Definition.MaxAttribute, out max);
                }

                _current.OnValueChanged += _onValueChanged;
                if (max != null && max != state)
                {
                    _listenedMax = max;
                    _listenedMax.OnValueChanged += _onValueChanged;
                }

                Refresh();
            }

            protected override void OnDetach()
            {
                if (_current != null) _current.OnValueChanged -= _onValueChanged;
                if (_listenedMax != null) _listenedMax.OnValueChanged -= _onValueChanged;
                _current = null;
                _explicitMax = null;
                _listenedMax = null;
            }

            private void Refresh()
            {
                float value = _current.CurrentValue;
                float maxValue = _explicitMax != null ? _explicitMax.CurrentValue : _current.MaxValue;
                AttributeDisplay display = _current.Definition.Display;
                TextSpan text = _source.Write(_buffer, display, value, maxValue, Context.Text, Context.Culture);
                Publish(_source.Build(value, maxValue, text, display.Role));
            }
        }
    }

    /// <summary>Barra de um atributo (R8): atual, máximo, fração e texto, por padrão "atual / máximo".</summary>
    [Serializable, TypeCategory("Atributos", Summary = "Barra com o valor atual e o máximo de um atributo do dono, com o texto \"atual / máximo\".",
        Line = "Barra · {Current} / {Max}")]
    public sealed class AttributeBarSource : AttributeSource<BarModel>
    {
        /// <summary>Cria a fonte com o texto em <see cref="AttributeTextMode.CurrentOfMax"/>.</summary>
        public AttributeBarSource() : base(AttributeTextMode.CurrentOfMax)
        {
        }

        private protected override BarModel Build(float currentValue, float maxValue, TextSpan text, TextRole role) =>
            new BarModel(currentValue, maxValue, text, role);
    }

    /// <summary>Texto de um atributo (R8), por padrão só o valor atual.</summary>
    [Serializable, TypeCategory("Atributos", Summary = "Texto com o valor atual de um atributo do dono, no formato do atributo.",
        Line = "Texto · {Current}")]
    public sealed class AttributeTextSource : AttributeSource<TextModel>
    {
        /// <summary>Cria a fonte com o texto em <see cref="AttributeTextMode.Current"/>.</summary>
        public AttributeTextSource() : base(AttributeTextMode.Current)
        {
        }

        private protected override TextModel Build(float currentValue, float maxValue, TextSpan text, TextRole role) =>
            new TextModel(text, role);
    }
}
