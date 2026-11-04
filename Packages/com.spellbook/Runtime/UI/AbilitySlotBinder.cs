using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>Binder do slot de habilidade (Spec do B-016, Design 3): entrega o <see cref="SlotModel"/> da fonte à view, sem lógica própria.</summary>
    [HelpURL(DocUrls.UiAbilitySlot + "#abilityslotbinder")]
    public sealed class AbilitySlotBinder : UIBinder<SlotModel>
    {
    }
}
