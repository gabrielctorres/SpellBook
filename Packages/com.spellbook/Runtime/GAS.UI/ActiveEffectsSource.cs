using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
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
    /// <see cref="EffectAggregate"/> (pilhas somadas, infinito se alguma é). Efeito <see cref="GameplayEffect.IsHidden"/>
    /// nunca aparece. Atualiza pelos eventos de efeito. Guarda só o asset e o id de cada ícone; o resto vem do ASC. Não
    /// altera o ASC.
    /// </summary>
    [Serializable, TypeCategory("Efeitos", Summary = "Lista de buffs e debuffs do dono: um ícone por efeito, na ordem em que apareceu, com as pilhas somadas.")]
    public sealed class ActiveEffectsSource : UIListSource<EffectIconModel>
    {
        [Tooltip("Pilhas a partir das quais o número aparece no ícone.")]
        [SerializeField, Min(1)] private int minStacksToShow = 2;

        /// <summary>Pilhas a partir das quais o número aparece; menor que 1 vale 1.</summary>
        public int MinStacksToShow { get => minStacksToShow; set => minStacksToShow = Math.Max(1, value); }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIListBinding<EffectIconModel> CreateBinding() => new Binding(this);

        /// <summary>Sem ícones de exemplo por enquanto.</summary>
        public override void CreatePreview(in UIPreviewContext context, List<EffectIconModel> items)
        {
        }

        // Só IsBuff: benéfico; só IsDebuff: nocivo; nenhum ou os dois: neutro (C4)
        private static TextRole RoleOf(GameplayEffect effect)
        {
            if (effect.IsBuff == effect.IsDebuff) return TextRole.Neutral;
            return effect.IsBuff ? TextRole.Beneficial : TextRole.Harmful;
        }

        // Estado de uma ligação: os grupos na ordem em que o efeito apareceu e a lista de instâncias reaproveitada (P3)
        private sealed class Binding : UIListBinding<EffectIconModel>
        {
            private readonly ActiveEffectsSource _source;
            private readonly List<Group> _groups = new List<Group>(8);
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

                _groups.Clear();
                _asc = null;
            }

            private void OnEffectChanged(ActiveEffectInstance instance)
            {
                if (instance != null && instance.Effect != null) Refresh(instance.Effect);
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

                _groups.Add(new Group { Effect = effect, Id = _nextId++, Name = effect.DisplayName.Resolve(Context.Text) });
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
                GameplayEffect effect = group.Effect;
                var model = new EffectIconModel(effect.Icon, group.Name, RoleOf(effect), aggregate.Stacks,
                    aggregate.Stacks >= _source.minStacksToShow, 0f, 0f, default, aggregate.IsInfinite, false, effect);
                if (add) AddItem(group.Id, in model);
                else UpdateItem(group.Id, in model);
            }

            private void Remove(int index)
            {
                int id = _groups[index].Id;
                _groups.RemoveAt(index);
                RemoveItem(id);
            }

            private int IndexOf(GameplayEffect effect)
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (ReferenceEquals(_groups[i].Effect, effect)) return i;
                }

                return -1;
            }

            // Um ícone: o asset, o id estável e o nome resolvido ao nascer
            private struct Group
            {
                public GameplayEffect Effect;
                public int Id;
                public string Name;
            }
        }
    }
}
