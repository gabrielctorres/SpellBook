using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder da lista de buffs e debuffs (Spec do B-018, Design 3): cria um ícone por item da fonte de lista, pelo
    /// <see cref="SpawnerAsset"/>, e entrega o <see cref="EffectIconModel"/> de cada um à view do ícone. Acima de
    /// <see cref="UIListBinder{TItem}.MaxVisible"/>, a view de "+N" mostra os ocultos, e o oculto mais antigo entra
    /// quando um ícone sai. Sem lógica própria.
    /// </summary>
    [HelpURL(DocUrls.UiEffectList + "#effectlistbinder")]
    public sealed class EffectListBinder : UIListBinder<EffectIconModel>
    {
    }
}
