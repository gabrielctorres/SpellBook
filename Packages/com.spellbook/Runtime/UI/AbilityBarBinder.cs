using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder da barra de habilidades (Spec do B-016, Design 3): cria um slot por item da fonte de lista, pelo
    /// <see cref="SpawnerAsset"/>, e entrega o <see cref="SlotModel"/> de cada um à view do slot, sem lógica própria.
    /// </summary>
    [HelpURL(DocUrls.UiAbilityBar + "#abilitybarbinder")]
    public sealed class AbilityBarBinder : UIListBinder<SlotModel>
    {
    }
}
