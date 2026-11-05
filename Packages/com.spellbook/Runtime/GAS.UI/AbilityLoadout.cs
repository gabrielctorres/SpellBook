using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.Tags;
using SpellBook.UI;
using UnityEngine;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Habilidades da barra (Spec do B-016, Design 3; R1 e R2 do PRD): uma lista ordenada de entradas, tag do slot e
    /// habilidade, na ordem visual, e o provedor do rótulo das teclas. Só lista: não concede nada ao ASC, e o mesmo
    /// asset serve a vários personagens. O slot acha o spec da habilidade pelo <c>FindSpec</c> do ASC (P4). A ordem da
    /// lista é a ordem da barra, também em edição, no Inspector (R12).
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/GAS/UI/Ability Loadout", fileName = "New Ability Loadout")]
    [HelpURL(DocUrls.UiAbilityBar + "#abilityloadout")]
    public sealed class AbilityLoadout : ScriptableObject, IValidatable
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

        /// <summary>
        /// Loadout vazio (aviso); entrada sem tag ou sem habilidade (erro); tag repetida (erro); habilidade repetida,
        /// sem ícone ou com cargas inválidas (aviso). Cada mensagem aponta o campo da entrada (Design 7; R14).
        /// </summary>
        public void Validate(ValidationResult result)
        {
            if (entries.Count == 0)
            {
                result.AddWarning("Loadout vazio: a barra não mostra nenhum slot. Adicione uma entrada.", nameof(entries));
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                string path = $"{nameof(entries)}.Array.data[{i}]";
                ValidateSlot(result, i, path + ".slot");
                ValidateAbility(result, i, path + ".ability");
            }
        }

        private void ValidateSlot(ValidationResult result, int index, string path)
        {
            GameplayTag slot = entries[index].Slot;
            if (!slot.IsValid)
            {
                result.AddError($"Entrada sem tag em entries (posição {index}). Escolha a tag do slot.", path);
                return;
            }

            for (int first = 0; first < index; first++)
            {
                if (entries[first].Slot != slot) continue;

                result.AddError($"A tag {slot.Name} se repete em entries (posições {first} e {index}): o slot mostra só a " +
                    "primeira. Use uma tag por entrada.", path);
                return;
            }
        }

        // Ícone e cargas só na primeira vez que a habilidade aparece, para não repetir o aviso
        private void ValidateAbility(ValidationResult result, int index, string path)
        {
            GameplayAbility ability = entries[index].Ability;
            if (ability == null)
            {
                result.AddError($"Entrada sem habilidade em entries (posição {index}). Escolha a habilidade do slot.", path);
                return;
            }

            for (int first = 0; first < index; first++)
            {
                if (entries[first].Ability != ability) continue;

                result.AddWarning($"A habilidade {ability.name} se repete em entries (posições {first} e {index}): os dois " +
                    "slots mostram o mesmo estado.", path);
                return;
            }

            if (ability.Icon == null)
            {
                result.AddWarning($"A habilidade {ability.name} não tem ícone (posição {index}): o slot fica sem imagem.", path);
            }

            if (ability.Charges.Enabled)
            {
                var charges = new ValidationResult(ability);
                ability.Charges.Validate(charges, nameof(GameplayAbility.Charges), ability.Values);
                if (charges.Messages.Count > 0)
                {
                    result.AddWarning($"A habilidade {ability.name} tem cargas inválidas (posição {index}): " +
                        charges.Messages[0].Message, path);
                }
            }
        }
    }
}
