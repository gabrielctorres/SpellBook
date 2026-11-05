using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte da lista de buffs e debuffs (Spec do B-018, Design 2; R3 e R7 do PRD): um ícone por <see cref="GameplayEffect"/>
    /// ativo no ASC dono, na ordem em que o efeito apareceu, com as instâncias vistas como uma pelo
    /// <see cref="EffectAggregate"/> (pilhas somadas, a maior duração restante, infinito se alguma é). Efeito
    /// <see cref="GameplayEffect.IsHidden"/> nunca aparece. Atualiza pelos eventos de efeito e pede tick só enquanto há
    /// efeito com duração. Guarda só o asset e o id de cada ícone; o resto vem do ASC. Não altera o ASC.
    /// </summary>
    [Serializable, TypeCategory("Efeitos", Summary = "Lista de buffs e debuffs do dono: um ícone por efeito, na ordem em que apareceu, com as pilhas somadas e a maior duração restante.")]
    public sealed class ActiveEffectsSource : UIListSource<EffectIconModel>
    {
        // Segundos inteiros, arredondados para cima, como o tempo do tooltip do efeito
        private static readonly AttributeDisplay Seconds = new AttributeDisplay { Kind = ValueKind.Seconds, Decimals = 0 };

        [Tooltip("Pilhas a partir das quais o número aparece no ícone.")]
        [SerializeField, Min(1)] private int minStacksToShow = 2;

        [NonSerialized] private TextBuffer _preview;

        /// <summary>Pilhas a partir das quais o número aparece; menor que 1 vale 1.</summary>
        public int MinStacksToShow { get => minStacksToShow; set => minStacksToShow = Math.Max(1, value); }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIListBinding<EffectIconModel> CreateBinding() => new Binding(this);

        /// <summary>Três ícones de exemplo (C13): um buff com 3 pilhas, um debuff em 60 % com 3 s e um permanente.</summary>
        public override void CreatePreview(in UIPreviewContext context, List<EffectIconModel> items)
        {
            TextBuffer text = _preview ??= new TextBuffer(16);
            text.Clear();
            TextSpan full = Format(text, 10f, context.Culture);
            TextSpan ending = Format(text, 3f, context.Culture);
            items.Add(new EffectIconModel(null, null, TextRole.Beneficial, 3, 3 >= minStacksToShow, 1f, 10f, full, false, false, null));
            items.Add(new EffectIconModel(null, null, TextRole.Harmful, 1, 1 >= minStacksToShow, 0.6f, 3f, ending, false, false, null));
            items.Add(new EffectIconModel(null, null, TextRole.Neutral, 1, 1 >= minStacksToShow, 0f, 0f, default, true, false, null));
        }

        // Só IsBuff: benéfico; só IsDebuff: nocivo; nenhum ou os dois: neutro (C4)
        private static TextRole RoleOf(GameplayEffect effect)
        {
            if (effect.IsBuff == effect.IsDebuff) return TextRole.Neutral;
            return effect.IsBuff ? TextRole.Beneficial : TextRole.Harmful;
        }

        private static int SecondsOf(float remaining) => Mathf.CeilToInt(Mathf.Max(0f, remaining));

        private static TextSpan Format(TextBuffer into, float seconds, IFormatProvider culture)
        {
            int start = into.Length;
            Seconds.Format(into, SecondsOf(seconds), culture);
            return into.SpanFrom(start);
        }

        // Estado de uma ligação: os grupos na ordem em que o efeito apareceu e os buffers de texto reaproveitados (P3)
        private sealed class Binding : UIListBinding<EffectIconModel>
        {
            private readonly ActiveEffectsSource _source;
            private readonly List<Group> _groups = new List<Group>(8);
            private readonly List<TextBuffer> _freeTexts = new List<TextBuffer>(8);
            private readonly List<ActiveEffectInstance> _instances = new List<ActiveEffectInstance>(2);
            private readonly Action<ActiveEffectInstance> _onApplied;
            private readonly Action<ActiveEffectInstance, EffectRemovalReason> _onRemoved;
            private readonly Action<ActiveEffectInstance, int, int> _onStacksChanged;
            private AbilitySystemComponent _asc;
            private int _nextId;

            public Binding(ActiveEffectsSource source)
            {
                _source = source;
                _onApplied = OnEffectChanged;
                _onRemoved = (instance, _) => OnEffectChanged(instance);
                _onStacksChanged = (instance, _, _) => OnEffectChanged(instance);
            }

            // Renovação e mudança de duração não têm evento: o tick relê os grupos com duração
            public override void Tick(double time)
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (!_groups[i].Timed) continue;

                    EffectAggregate aggregate = Aggregate(_groups[i].Effect);
                    if (aggregate.Count > 0) Publish(i, in aggregate, false);
                }
            }

            // Dono que não é ASC: lista vazia, sem assinar nada. Com ASC, lê o que já está ativo (C11)
            protected override void OnAttach(Object owner)
            {
                _asc = owner as AbilitySystemComponent;
                if (_asc == null) return;

                _asc.OnEffectApplied += _onApplied;
                _asc.OnEffectRemoved += _onRemoved;
                _asc.OnEffectStacksChanged += _onStacksChanged;
                IReadOnlyList<ActiveEffectInstance> active = _asc.ActiveEffects;
                for (int i = 0; i < active.Count; i++)
                {
                    Refresh(active[i].Effect);
                }

                RefreshTick();
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

                for (int i = 0; i < _groups.Count; i++)
                {
                    _freeTexts.Add(_groups[i].Text);
                }

                _groups.Clear();
                _asc = null;
            }

            private void OnEffectChanged(ActiveEffectInstance instance)
            {
                if (instance != null && instance.Effect != null)
                {
                    Refresh(instance.Effect);
                    RefreshTick();
                }
            }

            // Refaz só o grupo do efeito: sem instância sai; existente muda; novo e visível nasce no fim (C6)
            private void Refresh(GameplayEffect effect)
            {
                int index = IndexOf(effect);
                EffectAggregate aggregate = Aggregate(effect);
                if (aggregate.Count == 0)
                {
                    if (index >= 0) Remove(index);
                    return;
                }

                if (index >= 0)
                {
                    Publish(index, in aggregate, false);
                    return;
                }

                if (effect.IsHidden) return;

                _groups.Add(new Group
                {
                    Effect = effect,
                    Id = _nextId++,
                    Name = effect.DisplayName.Resolve(Context.Text),
                    Text = TakeText(),
                    ShownSeconds = -1,
                });
                Publish(_groups.Count - 1, in aggregate, true);
            }

            // A lista de instâncias é só rascunho do agregado: sai vazia, sem registro paralelo (N3)
            private EffectAggregate Aggregate(GameplayEffect effect)
            {
                EffectAggregate aggregate = EffectAggregate.Of(_asc != null ? _asc : null, effect, _instances);
                _instances.Clear();
                return aggregate;
            }

            private void Publish(int index, in EffectAggregate aggregate, bool add)
            {
                Group group = _groups[index];
                bool timed = aggregate.HasDuration;
                TextSpan text = default;
                float fraction = 0f;
                if (timed)
                {
                    int seconds = SecondsOf(aggregate.Remaining);
                    if (seconds != group.ShownSeconds)
                    {
                        group.Text.Clear();
                        group.ShownSeconds = seconds;
                        Format(group.Text, seconds, Context.Culture);
                    }

                    text = group.Text.SpanFrom(0);
                    fraction = aggregate.Duration > 0f ? aggregate.Remaining / aggregate.Duration : 0f;
                }

                group.Timed = timed;
                _groups[index] = group;
                GameplayEffect effect = group.Effect;
                var model = new EffectIconModel(effect.Icon, group.Name, RoleOf(effect), aggregate.Stacks,
                    aggregate.Stacks >= _source.minStacksToShow, fraction, timed ? aggregate.Remaining : 0f, text,
                    aggregate.IsInfinite, false, effect);
                if (add) AddItem(group.Id, in model);
                else UpdateItem(group.Id, in model);
            }

            private void Remove(int index)
            {
                Group group = _groups[index];
                _groups.RemoveAt(index);
                _freeTexts.Add(group.Text);
                RemoveItem(group.Id);
            }

            private int IndexOf(GameplayEffect effect)
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (ReferenceEquals(_groups[i].Effect, effect)) return i;
                }

                return -1;
            }

            private TextBuffer TakeText()
            {
                int last = _freeTexts.Count - 1;
                if (last < 0) return new TextBuffer(16);

                TextBuffer text = _freeTexts[last];
                _freeTexts.RemoveAt(last);
                return text;
            }

            // Pede tick só enquanto algum ícone tem duração finita (P6)
            private void RefreshTick()
            {
                bool wants = false;
                for (int i = 0; i < _groups.Count && !wants; i++)
                {
                    wants = _groups[i].Timed;
                }

                RequestTick(wants);
            }

            // Um ícone: o asset, o id estável, o nome resolvido ao nascer e o texto do tempo
            private struct Group
            {
                public GameplayEffect Effect;
                public int Id;
                public string Name;
                public TextBuffer Text;
                public int ShownSeconds;
                public bool Timed;
            }
        }
    }
}
