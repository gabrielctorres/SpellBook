using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Rótulos fixos por chave de slot (Spec do B-016, P5): uma linha por tag de slot, com o texto do rótulo. Editar no
    /// Inspector dispara <see cref="KeyLabelProvider.Changed"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/UI/Fixed Key Labels", fileName = "FixedKeyLabels")]
    [HelpURL(DocUrls.UiAbilitySlot + "#fixedkeylabelprovider")]
    public sealed class FixedKeyLabelProvider : KeyLabelProvider
    {
        /// <summary>A chave do slot e o rótulo dela.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Asset da tag do slot, como Slot.Q.")]
            [SerializeField] private ScriptableObject key;

            [Tooltip("Texto da tecla, como Q. A chave opcional passa pelo provedor de texto do tema.")]
            [SerializeField] private LocalizedText label;

            /// <summary>Cria a linha.</summary>
            public Entry(ScriptableObject key, LocalizedText label)
            {
                this.key = key;
                this.label = label;
            }

            /// <summary>O asset da tag do slot.</summary>
            public ScriptableObject Key => key;

            /// <summary>O rótulo.</summary>
            public LocalizedText Label => label;
        }

        [SerializeField, Tooltip("Uma linha por slot. Na mesma chave, a primeira linha vence.")]
        private List<Entry> entries = new List<Entry>();

        /// <summary>As linhas.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <inheritdoc />
        public override bool TryWriteLabel(ScriptableObject slotKey, ITextProvider text, TextBuffer into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            if (slotKey == null) return false;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key != slotKey) continue;

                into.Append(entries[i].Label.Resolve(text));
                return true;
            }

            return false;
        }

        private void OnValidate()
        {
            RaiseChanged();
        }
    }
}
