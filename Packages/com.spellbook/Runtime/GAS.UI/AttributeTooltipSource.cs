using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Text;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte do tooltip de um atributo do ASC dono (Spec UI-3, Design 4; R5 do PRD): nome, ícone e o valor atual no formato
    /// do atributo. Com <see cref="Breakdown"/>, também a base e uma linha por modificador, com o nome do efeito de origem
    /// ou o rótulo de outra fonte (P3 do PRD). Sem ASC, ou sem o atributo nele, mostra só o que o asset sabe. Atualiza por
    /// evento do atributo e, com a decomposição, dos efeitos do ASC; não pede tick e não altera o ASC.
    /// </summary>
    [Serializable, TypeCategory("Tooltip", Summary = "Tooltip de um atributo do dono: valor atual no formato do atributo e, opcional, a base e os modificadores.",
        Line = "Atributo {attribute}")]
    public sealed class AttributeTooltipSource : UISource<TooltipModel>
    {
        private static readonly LocalizedText BaseLabel = new LocalizedText(GasTextKeys.TooltipBase, "Base");
        private static readonly LocalizedText OtherSourceLabel = new LocalizedText(GasTextKeys.TooltipOtherSource, "Outra fonte");

        [Tooltip("Atributo mostrado. Vazio: o do item (UIItemContext.Subject).")]
        [SerializeField] private AttributeDefinition attribute;

        [Tooltip("Acrescenta a base e uma linha por modificador, com a fonte de cada um.")]
        [SerializeField] private bool breakdown;

        [NonSerialized] private TooltipLineWriter _preview;

        /// <summary>O atributo mostrado; nulo usa o assunto do item.</summary>
        public AttributeDefinition Attribute { get => attribute; set => attribute = value; }

        /// <summary>Se mostra a base e os modificadores; desligado por padrão.</summary>
        public bool Breakdown { get => breakdown; set => breakdown = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<TooltipModel> CreateBinding() => new Binding(this);

        /// <summary>O tooltip do atributo configurado sem dono: nome e ícone, sem valor; vazio sem atributo.</summary>
        public override TooltipModel CreatePreview(in UIPreviewContext context)
        {
            return Build(_preview ??= new TooltipLineWriter(), attribute, null, null, context.Text);
        }

        // Monta o modelo no writer; sem atributo (nulo ou destruído), o modelo vazio; sem estado, só o asset
        private TooltipModel Build(TooltipLineWriter writer, AttributeDefinition shown, AbilitySystemComponent asc,
            AttributeState state, ITextProvider text)
        {
            if (shown == null) return default;

            writer.Begin(null, 1, asc, text, null);
            if (state != null)
            {
                AttributeDisplay display = shown.Display;
                List<DescriptionLine> lines = writer.Context.Lines;
                lines.Add(new DescriptionLine(DescriptionLineKind.Attribute, default, state.CurrentValue, display.Role,
                    attribute: shown));
                writer.Convert();
                if (breakdown)
                {
                    lines.Add(new DescriptionLine(DescriptionLineKind.Attribute, BaseLabel, asc.GetAttributeBaseValue(shown),
                        display.Role, attribute: shown));
                    IReadOnlyList<AttributeModifier> modifiers = state.Modifiers;
                    for (int i = 0; i < modifiers.Count; i++)
                    {
                        AttributeModifier modifier = modifiers[i];
                        TextRole role = modifier.Operation == ModifierOperation.Override
                            ? TextRole.Value
                            : display.GetDeltaRole(modifier.Value);
                        lines.Add(DescriptionLine.ForModifier(DescriptionLineKind.Modifier, shown, LabelOf(modifier),
                            modifier.Value, modifier.Operation, role));
                    }

                    writer.Convert(TooltipLineKind.Breakdown);
                }
            }

            return new TooltipModel(shown.DisplayName.Resolve(text), shown.Icon, writer.Lines);
        }

        // O nome do efeito de origem; sem efeito ou sem nome, o rótulo de outra fonte
        private static LocalizedText LabelOf(AttributeModifier modifier)
        {
            return modifier.Source is ActiveEffectInstance instance && instance.Effect != null
                && !instance.Effect.DisplayName.IsEmpty
                ? instance.Effect.DisplayName
                : OtherSourceLabel;
        }

        // Estado de uma ligação: o writer, o ASC e o atributo assinados; os delegates nascem uma vez
        private sealed class Binding : UIBinding<TooltipModel>
        {
            private readonly AttributeTooltipSource _source;
            private readonly TooltipLineWriter _writer = new TooltipLineWriter();
            private readonly Action<float, float> _onValueChanged;
            private readonly Action<ActiveEffectInstance> _onEffectApplied;
            private readonly Action<ActiveEffectInstance, EffectRemovalReason> _onEffectRemoved;
            private readonly Action<ActiveEffectInstance, int, int> _onStacksChanged;

            private AttributeDefinition _attribute;
            private AbilitySystemComponent _asc;
            private AttributeState _state;
            private bool _effects;

            public Binding(AttributeTooltipSource source)
            {
                _source = source;
                _onValueChanged = (_, _) => Refresh();
                _onEffectApplied = _ => Refresh();
                _onEffectRemoved = (_, _) => Refresh();
                _onStacksChanged = (_, _, _) => Refresh();
            }

            // Dono que não é ASC (o binder sem dono liga a si mesmo) ou ASC sem o atributo: só o asset, sem assinar nada.
            // Com a decomposição, os eventos de efeito cobrem o modificador que não muda o valor (atributo no limite)
            protected override void OnAttach(Object owner)
            {
                _attribute = _source.attribute != null ? _source.attribute : Context.Subject as AttributeDefinition;
                if (owner is AbilitySystemComponent asc && _attribute != null && asc.AttributeSet != null
                    && asc.AttributeSet.TryGetState(_attribute, out AttributeState state))
                {
                    _asc = asc;
                    _state = state;
                    _state.OnValueChanged += _onValueChanged;
                    if (_source.breakdown)
                    {
                        _effects = true;
                        _asc.OnEffectApplied += _onEffectApplied;
                        _asc.OnEffectRemoved += _onEffectRemoved;
                        _asc.OnEffectStacksChanged += _onStacksChanged;
                    }
                }

                Refresh();
            }

            // Solta pelo ReferenceEquals: um ASC destruído ainda guarda os delegates
            protected override void OnDetach()
            {
                if (_state != null) _state.OnValueChanged -= _onValueChanged;
                if (_effects && !ReferenceEquals(_asc, null))
                {
                    _asc.OnEffectApplied -= _onEffectApplied;
                    _asc.OnEffectRemoved -= _onEffectRemoved;
                    _asc.OnEffectStacksChanged -= _onStacksChanged;
                }

                _effects = false;
                _state = null;
                _asc = null;
                _attribute = null;
            }

            private void Refresh()
            {
                AbilitySystemComponent asc = _asc != null ? _asc : null;
                Publish(_source.Build(_writer, _attribute, asc, asc != null ? _state : null, Context.Text));
            }
        }
    }
}
