using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Âncora em que o <see cref="TooltipBinder"/> da tela se publica, pelo <see cref="AnchorRegistrar"/>. Os gatilhos
    /// apontam a âncora para abrir o tooltip sem procurar o binder (Spec UI-3, Design 2).
    /// </summary>
    [HelpURL(DocUrls.UiTooltip + "#tooltipbinderanchor")]
    [CreateAssetMenu(menuName = "SpellBook/UI/Tooltip Binder Anchor", fileName = "New Tooltip Binder Anchor")]
    public sealed class TooltipBinderAnchor : RuntimeAnchor<TooltipBinder>
    {
    }
}
