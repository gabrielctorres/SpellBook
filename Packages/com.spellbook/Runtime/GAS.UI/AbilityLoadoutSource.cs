using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Text;
using SpellBook.Core.Validation;
using SpellBook.GAS.Core;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte da barra de habilidades (Spec do B-016, Design 3; R2 do PRD): um slot por entrada do <see cref="Loadout"/>,
    /// na ordem da lista, com o id igual ao índice. Cada slot lê o ASC dono como o <see cref="AbilitySlotSource"/>: estado,
    /// recarga, cargas, motivo de falha, marcas de ativa e de autocast e rótulo da tecla, por evento, com tick só
    /// enquanto algum slot pede. A habilidade não concedida aparece como slot vazio. <see cref="SetLoadout"/> refaz a
    /// barra. Não ativa nem concede nada.
    /// </summary>
    [Serializable, TypeCategory("Habilidades", Summary = "Barra pelo loadout: um slot por entrada, na ordem da lista, com estado, recarga, cargas, motivo de falha, ativa, autocast e rótulo da tecla.",
        Line = "Barra · {loadout}")]
    public sealed class AbilityLoadoutSource : UIListSource<SlotModel>
    {
        [Tooltip("Loadout com as entradas da barra, na ordem, e o rótulo das teclas.")]
        [SerializeField] private AbilityLoadout loadout;

        [Tooltip("Segundos em que o motivo da última falha aparece em cada slot. 0: o contador sobe e o motivo não aparece.")]
        [SerializeField, Min(0f)] private float failureSeconds = 1f;

        [NonSerialized] private TextBuffer _preview;

        // Avisa as ligações vivas de que o loadout foi trocado ou deve ser relido
        [NonSerialized] private Action _loadoutChanged;

        /// <summary>O loadout lido.</summary>
        public AbilityLoadout Loadout => loadout;

        /// <summary>Segundos em que o motivo da última falha aparece; negativo vale 0.</summary>
        public float FailureSeconds { get => failureSeconds; set => failureSeconds = Mathf.Max(0f, value); }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <summary>
        /// Troca o loadout e refaz a barra de cada ligação viva (R2). Com o mesmo asset, relê as entradas, por exemplo
        /// depois de mudar a ordem.
        /// </summary>
        public void SetLoadout(AbilityLoadout value)
        {
            loadout = value;
            _loadoutChanged?.Invoke();
        }

        /// <inheritdoc />
        public override UIListBinding<SlotModel> CreateBinding() => new Binding(this);

        /// <summary>Sem loadout: erro no campo (Design 7; R14). O loadout se valida no próprio Inspector.</summary>
        public override void Validate(ValidationResult result, string propertyPath)
        {
            if (loadout == null)
            {
                result.AddError("Fonte sem loadout. Escolha o loadout com as entradas da barra.", $"{propertyPath}.{nameof(loadout)}");
            }
        }

        /// <summary>Um slot de prévia por entrada, na ordem, como a prévia do <see cref="AbilitySlotSource"/> (R12).</summary>
        public override void CreatePreview(in UIPreviewContext context, List<SlotModel> items)
        {
            if (loadout == null) return;

            TextBuffer labels = _preview ??= new TextBuffer(16);
            labels.Clear();
            IReadOnlyList<AbilityLoadout.Entry> entries = loadout.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                items.Add(AbilitySlotSource.Preview(entries[i].Ability, entries[i].Slot, loadout, context.Text, labels));
            }
        }

        // Um SlotReader por entrada, criado uma vez e reaproveitado a cada religação e troca de loadout (N3)
        private sealed class Binding : UIListBinding<SlotModel>
        {
            private readonly AbilityLoadoutSource _source;
            private readonly List<SlotReader> _readers = new List<SlotReader>(8);
            private readonly Action _onLoadoutChanged;
            private AbilitySystemComponent _asc;
            private int _count;

            public Binding(AbilityLoadoutSource source)
            {
                _source = source;
                _onLoadoutChanged = Rebuild;
            }

            public override void Tick(double time)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_readers[i].WantsTick) _readers[i].Tick();
                }
            }

            // Dono que não é ASC: nenhum slot, sem assinar o ASC
            protected override void OnAttach(Object owner)
            {
                _source._loadoutChanged += _onLoadoutChanged;
                _asc = owner as AbilitySystemComponent;
                Build();
            }

            protected override void OnDetach()
            {
                _source._loadoutChanged -= _onLoadoutChanged;
                Release();
                _asc = null;
            }

            private void Rebuild()
            {
                Release();
                ClearItems();
                Build();
            }

            // Liga um leitor por entrada e publica os slots na ordem da lista; o aviso do leitor antes do AddItem não
            // acha o id e não faz nada
            private void Build()
            {
                AbilityLoadout loadout = _source.loadout;
                if (_asc == null || loadout == null) return;

                IReadOnlyList<AbilityLoadout.Entry> entries = loadout.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (i == _readers.Count)
                    {
                        int id = i;
                        _readers.Add(new SlotReader(() => OnSlotChanged(id)));
                    }

                    AbilityLoadout.Entry entry = entries[i];
                    _readers[i].Attach(_asc, entry.Ability, entry.Slot.GetDefinition(), loadout.KeyLabels, Context.Text,
                        _source.failureSeconds);
                    _count = i + 1;
                    AddItem(i, _readers[i].Model);
                }

                RefreshTick();
            }

            private void Release()
            {
                for (int i = 0; i < _count; i++)
                {
                    _readers[i].Detach();
                }

                _count = 0;
                RequestTick(false);
            }

            private void OnSlotChanged(int id)
            {
                UpdateItem(id, _readers[id].Model);
                RefreshTick();
            }

            private void RefreshTick()
            {
                bool wants = false;
                for (int i = 0; i < _count && !wants; i++)
                {
                    wants = _readers[i].WantsTick;
                }

                RequestTick(wants);
            }
        }
    }
}
