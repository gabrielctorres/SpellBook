using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
using SpellBook.GAS.Effects;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte do tooltip de um efeito (Spec UI-3, Design 4; C4 do PRD): título, ícone e as linhas de modificador do
    /// <see cref="EffectDescriber"/> com as pilhas somadas das instâncias no ASC dono, as pilhas e o tempo restante pelo
    /// <see cref="EffectAggregate"/>. Atualiza pelos eventos de efeito filtrados pelo efeito e pede tick só enquanto aberto e
    /// com duração finita. Sem instância ou sem ASC, mostra só o que o asset sabe. Não altera o ASC.
    /// </summary>
    [Serializable, TypeCategory("Tooltip", Summary = "Tooltip de um efeito: modificadores, pilhas e tempo restante no dono, ou só o asset sem instância ativa.",
        Line = "{effect}")]
    public sealed class EffectTooltipSource : UISource<TooltipModel>
    {
        // Tempo restante em segundos inteiros, arredondados para cima, como o texto da recarga no slot
        private static readonly AttributeDisplay Seconds = new AttributeDisplay { Kind = ValueKind.Seconds, Decimals = 0 };

        [Tooltip("Efeito mostrado. Vazio: o do item (UIItemContext.Subject).")]
        [SerializeField] private GameplayEffect effect;

        [NonSerialized] private TooltipLineWriter _preview;

        /// <summary>O efeito mostrado; nulo usa o assunto do item.</summary>
        public GameplayEffect Effect { get => effect; set => effect = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<TooltipModel> CreateBinding() => new Binding(this);

        /// <summary>O tooltip do efeito configurado no nível do contexto, sem instância; vazio sem efeito.</summary>
        public override TooltipModel CreatePreview(in UIPreviewContext context)
        {
            return Build(_preview ??= new TooltipLineWriter(), effect, default, context.Level, context.Text);
        }

        // Linhas no nível, instigador e habilidade da primeira instância (sem ela, no nível dado) e com as pilhas somadas
        private static TooltipModel Build(TooltipLineWriter writer, GameplayEffect shown, in EffectAggregate aggregate,
            int levelWithoutInstance, ITextProvider text)
        {
            if (shown == null) return default;

            ActiveEffectInstance first = aggregate.First;
            if (first != null)
            {
                EffectContext context = first.Context;
                writer.Begin(context.Ability, context.Level, first.Source != null ? first.Source : null, text, null);
            }
            else
            {
                writer.Begin(null, levelWithoutInstance, null, text, null);
            }

            writer.Context.Effect = shown;
            writer.Context.Stacks = Math.Max(1, aggregate.Stacks);
            EffectDescriber.Describe(shown, writer.Context);
            writer.Convert();

            var model = new TooltipModel(shown.DisplayName.Resolve(text), shown.Icon, writer.Lines);
            return aggregate.Stacks > 1 ? model.WithStacks(aggregate.Stacks) : model;
        }

        // Estado de uma ligação: o writer, as instâncias e o texto do tempo; os delegates nascem uma vez
        private sealed class Binding : UIBinding<TooltipModel>
        {
            private readonly EffectTooltipSource _source;
            private readonly TooltipLineWriter _writer = new TooltipLineWriter();
            private readonly List<ActiveEffectInstance> _instances = new List<ActiveEffectInstance>(2);
            private readonly TextBuffer _remaining = new TextBuffer(16);
            private readonly Action<ActiveEffectInstance> _onApplied;
            private readonly Action<ActiveEffectInstance, EffectRemovalReason> _onRemoved;
            private readonly Action<ActiveEffectInstance, int, int> _onStacksChanged;

            private AbilitySystemComponent _asc;
            private GameplayEffect _effect;
            private TooltipModel _model;
            private int _shownSeconds;

            public Binding(EffectTooltipSource source)
            {
                _source = source;
                _onApplied = instance => OnEffectChanged(instance);
                _onRemoved = (instance, _) => OnEffectChanged(instance);
                _onStacksChanged = (instance, _, _) => OnEffectChanged(instance);
            }

            // Dono que não é ASC (o binder sem dono liga a si mesmo): só o asset, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                _effect = _source.effect != null ? _source.effect : Context.Subject as GameplayEffect;
                if (owner is AbilitySystemComponent asc && _effect != null)
                {
                    _asc = asc;
                    _asc.OnEffectApplied += _onApplied;
                    _asc.OnEffectRemoved += _onRemoved;
                    _asc.OnEffectStacksChanged += _onStacksChanged;
                }

                Refresh();
            }

            // Solta pelo ReferenceEquals: um ASC destruído ainda guarda os delegates
            protected override void OnDetach()
            {
                if (!ReferenceEquals(_asc, null))
                {
                    _asc.OnEffectApplied -= _onApplied;
                    _asc.OnEffectRemoved -= _onRemoved;
                    _asc.OnEffectStacksChanged -= _onStacksChanged;
                }

                _instances.Clear();
                _asc = null;
                _effect = null;
                _model = default;
            }

            // Renovação e mudança de duração não têm evento: o tick relê as instâncias e publica quando o segundo muda
            public override void Tick(double time)
            {
                EffectAggregate aggregate = Aggregate();
                if (!aggregate.HasDuration)
                {
                    Refresh();
                    return;
                }

                if (SecondsOf(aggregate) != _shownSeconds) Publish(in aggregate);
            }

            private void OnEffectChanged(ActiveEffectInstance instance)
            {
                if (instance != null && instance.Effect == _effect) Refresh();
            }

            private void Refresh()
            {
                EffectAggregate aggregate = Aggregate();
                int level = _effect != null ? _effect.DefaultLevel : 1;
                _model = Build(_writer, _effect, in aggregate, level, Context.Text);
                Publish(in aggregate);
                RequestTick(!_model.IsEmpty && aggregate.HasDuration);
            }

            private EffectAggregate Aggregate() => EffectAggregate.Of(_asc != null ? _asc : null, _effect, _instances);

            private void Publish(in EffectAggregate aggregate)
            {
                if (_model.IsEmpty || !aggregate.HasDuration)
                {
                    _shownSeconds = -1;
                    Publish(_model);
                    return;
                }

                _shownSeconds = SecondsOf(aggregate);
                _remaining.Clear();
                Seconds.Format(_remaining, _shownSeconds, Context.Culture ?? Context.Text?.Culture);
                Publish(_model.WithRemaining(aggregate.Remaining, _remaining.SpanFrom(0)));
            }

            private static int SecondsOf(in EffectAggregate aggregate) => Mathf.CeilToInt(Mathf.Max(0f, aggregate.Remaining));
        }
    }
}
