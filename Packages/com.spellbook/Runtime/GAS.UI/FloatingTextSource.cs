using System;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte dos números flutuantes do ASC dono (R14), para o <see cref="FloatingTextBinder"/>. Cada variação aplicada
    /// de um atributo com regra vira um número com sinal no formato do atributo ("-30", "+20"), no estilo da primeira
    /// regra que casa (<see cref="FloatingTextRules"/>). O efeito barrado por imunidade, a variação anulada pelos
    /// processadores (pedida, mas com final zero) e o bloqueio informado por <c>ReportBlocked</c> viram um número no
    /// estilo <see cref="FloatingTextRules.Blocked"/>. Lê o ASC pelos eventos dele, sem <c>Update</c> e sem relay. As
    /// regras são lidas a cada evento: mudar o asset muda o número seguinte.
    /// </summary>
    [Serializable, TypeCategory("Números flutuantes", Summary = "Números de dano, cura e bloqueio do dono, no estilo das regras de um asset.",
        Line = "Números · {Rules}")]
    public sealed class FloatingTextSource : UISource<FloatingTextRequest>
    {
        [Tooltip("Regras que escolhem o estilo de cada número. Vazio: nenhum número.")]
        [SerializeField] private FloatingTextRules rules;

        [Tooltip("Deslocamento do ponto de nascimento em relação ao dono, em unidades do mundo.")]
        [SerializeField] private Vector3 offset;

        /// <summary>Regras que escolhem o estilo; nulo não gera número.</summary>
        public FloatingTextRules Rules { get => rules; set => rules = value; }

        /// <summary>Deslocamento do ponto de nascimento em relação ao dono.</summary>
        public Vector3 Offset { get => offset; set => offset = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<FloatingTextRequest> CreateBinding() => new Binding(this);

        /// <summary>Estado vazio: em edição, nenhum número nasce.</summary>
        public override FloatingTextRequest CreatePreview(in UIPreviewContext context) => default;

        // Assina os três eventos do ASC com delegates criados uma vez e escreve o texto num buffer próprio; ligar,
        // desligar e publicar não alocam
        private sealed class Binding : UIBinding<FloatingTextRequest>
        {
            private readonly FloatingTextSource _source;
            private readonly TextBuffer _buffer = new TextBuffer(32);
            private readonly AttributeChangeHandler _onChangeApplied;
            private readonly Action<GameplayEffect, AbilitySystemComponent, EffectApplicationResult> _onEffectBlocked;
            private readonly Action<GameplayTag, AbilitySystemComponent> _onBlocked;

            private AbilitySystemComponent _asc;

            public Binding(FloatingTextSource source)
            {
                _source = source;
                _onChangeApplied = OnChangeApplied;
                _onEffectBlocked = (_, _, reason) =>
                {
                    if (reason == EffectApplicationResult.BlockedByImmunity) PublishBlocked();
                };
                _onBlocked = (_, _) => PublishBlocked();
            }

            // Dono que não é ASC: nada a assinar, nenhum número
            protected override void OnAttach(Object owner)
            {
                if (owner is not AbilitySystemComponent asc) return;

                _asc = asc;
                _asc.OnAttributeChangeApplied += _onChangeApplied;
                _asc.OnEffectBlocked += _onEffectBlocked;
                _asc.OnBlocked += _onBlocked;
            }

            protected override void OnDetach()
            {
                if (_asc == null) return;

                _asc.OnAttributeChangeApplied -= _onChangeApplied;
                _asc.OnEffectBlocked -= _onEffectBlocked;
                _asc.OnBlocked -= _onBlocked;
                _asc = null;
            }

            // Variação aplicada: número com sinal. Variação pedida e anulada: "bloqueado", só se uma regra a mostraria
            private void OnChangeApplied(in AttributeChangeReport report)
            {
                FloatingTextRules rules = _source.rules;
                if (rules == null) return;

                if (report.Applied != 0f)
                {
                    AttributeRule rule = rules.Find(report.Attribute, report.Applied, report.Effect);
                    if (rule == null) return;

                    FloatingTextStyle style = rule.Style;
                    AttributeDisplay display = report.Attribute.Display;
                    _buffer.Clear();
                    if (style != null && !style.Format.IsEmpty) _buffer.Append(style.Format.Resolve(Context.Text));
                    else display.Format(_buffer, report.Applied, ModifierOperation.Add, Context.Culture);

                    Emit(style != null ? style.Role : display.GetDeltaRole(report.Applied), style);
                }
                else if (report.Requested != 0f && report.Final == 0f
                    && rules.Find(report.Attribute, report.Requested, report.Effect) != null)
                {
                    PublishBlocked();
                }
            }

            private void PublishBlocked()
            {
                FloatingTextStyle style = _source.rules != null ? _source.rules.Blocked : null;
                if (style == null) return;

                _buffer.Clear();
                _buffer.Append(style.Format.Resolve(Context.Text));
                Emit(style.Role, style);
            }

            private void Emit(TextRole role, FloatingTextStyle style)
            {
                Vector3 position = _asc.transform.position + _source.offset;
                Publish(new FloatingTextRequest(position, _buffer.SpanFrom(0), role, style));
            }
        }
    }
}
