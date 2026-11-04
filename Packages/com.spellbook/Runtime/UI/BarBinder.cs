using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder da barra (R7): entrega o <see cref="BarModel"/> da fonte à view. O rastro acompanha a fração
    /// (<see cref="BarModel.TrailFraction"/> igual à <see cref="BarModel.Fraction"/>).
    /// </summary>
    [HelpURL(DocUrls.UiAttributeBar)]
    public sealed class BarBinder : UIBinder<BarModel>
    {
    }
}
