using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>Binder da barra de cast (Spec do B-016, Design 5): entrega o <see cref="CastBarModel"/> da fonte à view, sem lógica própria.</summary>
    [HelpURL(DocUrls.UiCastBar + "#castbarbinder")]
    public sealed class CastBarBinder : UIBinder<CastBarModel>
    {
    }
}
