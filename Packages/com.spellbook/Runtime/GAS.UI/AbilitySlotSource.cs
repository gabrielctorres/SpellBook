using System;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte do slot de uma habilidade (Spec do B-016, Design 3; R4 do PRD): acha a entrada do <see cref="Loadout"/> pela
    /// tag <see cref="Slot"/> e lê do ASC dono o estado, a recarga, as cargas, o motivo da última falha e o rótulo da
    /// tecla. Atualiza por evento e pede tick só enquanto a recarga, a próxima carga ou o motivo visível correm. O
    /// "pronto" vem da mesma consulta da ativação, até o custo (Q3). Não ativa nada (R11).
    /// </summary>
    [Serializable, TypeCategory("Habilidades", Summary = "Slot de uma habilidade do loadout, pela tag: estado, recarga, cargas, motivo de falha e rótulo da tecla.",
        Line = "Slot {slot.definition} · {loadout}")]
    public sealed class AbilitySlotSource : UISource<SlotModel>
    {
        [Tooltip("Loadout com a entrada do slot e o rótulo das teclas.")]
        [SerializeField] private AbilityLoadout loadout;

        [Tooltip("Tag do slot procurada no loadout, como Slot.Q.")]
        [SerializeField] private GameplayTag slot;

        [Tooltip("Segundos em que o motivo da última falha aparece no slot. 0: o contador sobe e o motivo não aparece.")]
        [SerializeField, Min(0f)] private float failureSeconds = 1f;

        [NonSerialized] private TextBuffer _preview;

        /// <summary>O loadout lido.</summary>
        public AbilityLoadout Loadout { get => loadout; set => loadout = value; }

        /// <summary>A tag do slot procurada no loadout.</summary>
        public GameplayTag Slot { get => slot; set => slot = value; }

        /// <summary>Segundos em que o motivo da última falha aparece; negativo vale 0.</summary>
        public float FailureSeconds { get => failureSeconds; set => failureSeconds = Mathf.Max(0f, value); }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<SlotModel> CreateBinding() => new Binding(this);

        /// <summary>A habilidade da entrada, metade da recarga, duas cargas e o rótulo do provedor ou "Q" (R12).</summary>
        public override SlotModel CreatePreview(in UIPreviewContext context)
        {
            GameplayAbility ability = FindAbility();
            TextBuffer label = _preview ??= new TextBuffer(8);
            label.Clear();
            KeyLabelProvider labels = loadout != null ? loadout.KeyLabels : null;
            if (labels == null || !labels.TryWriteLabel(slot.GetDefinition(), context.Text, label)) label.Append("Q");

            float duration = ability != null ? ability.GetCooldownDuration(null) : 0f;
            return new SlotModel(SlotState.OnCooldown, ability != null ? ability.Icon : null, ability)
                .WithKeyLabel(label.SpanFrom(0))
                .WithCooldown(0.5f, duration * 0.5f)
                .WithCharges(2, 3, 0.5f);
        }

        private GameplayAbility FindAbility() =>
            loadout != null && loadout.TryFindEntry(slot, out AbilityLoadout.Entry entry) ? entry.Ability : null;

        // Um SlotReader por ligação: duas barras com a mesma fonte não dividem estado (N3)
        private sealed class Binding : UIBinding<SlotModel>
        {
            private readonly AbilitySlotSource _source;
            private readonly SlotReader _reader;

            public Binding(AbilitySlotSource source)
            {
                _source = source;
                _reader = new SlotReader(OnReaderChanged);
            }

            public override void Tick(double time) => _reader.Tick();

            // Dono que não é ASC: estado vazio, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                if (owner is not AbilitySystemComponent asc)
                {
                    Publish(default);
                    return;
                }

                AbilityLoadout loadout = _source.loadout;
                _reader.Attach(asc, _source.FindAbility(), _source.slot.GetDefinition(),
                    loadout != null ? loadout.KeyLabels : null, Context.Text, _source.failureSeconds);
            }

            protected override void OnDetach() => _reader.Detach();

            private void OnReaderChanged()
            {
                Publish(_reader.Model);
                RequestTick(_reader.WantsTick);
            }
        }
    }
}
