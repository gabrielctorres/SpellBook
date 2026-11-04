using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Habilidades da barra (Spec do B-016, Design 3; R1 e R2 do PRD): uma lista ordenada de entradas, tag do slot e
    /// habilidade, na ordem visual, e o provedor do rótulo das teclas. Só lista: não concede nada ao ASC, e o mesmo
    /// asset serve a vários personagens. O slot acha o spec da habilidade pelo <c>FindSpec</c> do ASC (P4).
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/UI/Ability Loadout", fileName = "AbilityLoadout")]
    [HelpURL(DocUrls.UiAbilityBar + "#abilityloadout")]
    public sealed class AbilityLoadout : ScriptableObject
    {
        /// <summary>A tag do slot e a habilidade dele.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Tag do slot, como Slot.Q. É também a chave do rótulo da tecla.")]
            [SerializeField] private GameplayTag slot;

            [Tooltip("Habilidade mostrada no slot. O loadout não a concede.")]
            [SerializeField] private GameplayAbility ability;

            /// <summary>Cria a entrada.</summary>
            public Entry(GameplayTag slot, GameplayAbility ability)
            {
                this.slot = slot;
                this.ability = ability;
            }

            /// <summary>A tag do slot.</summary>
            public GameplayTag Slot => slot;

            /// <summary>A habilidade do slot.</summary>
            public GameplayAbility Ability => ability;
        }

        [SerializeField, Tooltip("Uma entrada por slot, na ordem da barra.")]
        private List<Entry> entries = new List<Entry>();

        [SerializeField, Tooltip("Rótulo da tecla de cada slot, pela tag. Vazio: sem rótulo.")]
        private KeyLabelProvider keyLabels;

        /// <summary>As entradas, na ordem da barra.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>O provedor do rótulo das teclas, ou nulo.</summary>
        public KeyLabelProvider KeyLabels => keyLabels;

        /// <summary>
        /// A entrada do <paramref name="slot"/>. Tag inválida ou ausente devolve <c>false</c>; na tag repetida, a primeira
        /// entrada vence. Não aloca.
        /// </summary>
        public bool TryFindEntry(GameplayTag slot, out Entry entry)
        {
            if (slot.IsValid)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Slot != slot) continue;

                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
