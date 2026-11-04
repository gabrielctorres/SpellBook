using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Abilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.UI.Editor
{
    /// <summary>
    /// Inspector do <see cref="AbilityLoadout"/> (Spec do B-016, Design 7; R13 do PRD): a validação no topo, o Inspector
    /// padrão e a seção "Ordem da barra", uma linha por entrada na ordem da lista, que é a ordem da barra também em
    /// edição (R12): "1. Slot.Q · Fireball · recarga 8 s". Em multi-seleção, a seção some.
    /// </summary>
    [CustomEditor(typeof(AbilityLoadout))]
    [CanEditMultipleObjects]
    public sealed class AbilityLoadoutEditor : SpellBookEditor
    {
        /// <summary>Nome do contêiner das linhas.</summary>
        public const string RowsName = "spellbook-loadout-rows";

        /// <summary>Classe de cada linha.</summary>
        public const string RowClass = "spellbook-loadout-row";

        private VisualElement _rows;

        protected override void BuildAfterDefault(VisualElement root)
        {
            _rows = new VisualElement { name = RowsName };
            root.Add(_rows);
            RefreshRows();
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshRows());
        }

        /// <summary>Refaz as linhas pela lista atual do loadout.</summary>
        public void RefreshRows()
        {
            if (_rows == null)
            {
                return;
            }

            _rows.Clear();
            if (serializedObject.isEditingMultipleObjects || !(target is AbilityLoadout loadout))
            {
                _rows.style.display = DisplayStyle.None;
                return;
            }

            _rows.style.display = DisplayStyle.Flex;
            _rows.Add(new Label("Ordem da barra"));
            IReadOnlyList<AbilityLoadout.Entry> entries = loadout.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var row = new Label(FormatRow(i, entries[i]));
                row.AddToClassList(RowClass);
                _rows.Add(row);
            }
        }

        // "N. Slot · Habilidade · recarga X s", com a recarga no nível 1, sem a redução do atributo
        private static string FormatRow(int index, in AbilityLoadout.Entry entry)
        {
            string slot = entry.Slot.IsValid ? entry.Slot.Name : SummaryFormatter.None;
            GameplayAbility ability = entry.Ability;
            string name = ability != null ? ability.name : SummaryFormatter.None;
            string cooldown = ability != null && ability.CooldownEffect != null
                ? $"recarga {ability.GetCooldownDuration(null).ToString("0.###", CultureInfo.CurrentCulture)} s"
                : "sem recarga";
            return $"{index + 1}. {slot} · {name} · {cooldown}";
        }
    }
}
