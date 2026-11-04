using SpellBook.Core;
using TMPro;
using UnityEngine;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// Linha-modelo do <see cref="TooltipView"/>: rótulo e valor de uma <see cref="TooltipLine"/>. A view clona esta
    /// linha e escreve nos clones; o componente só guarda os dois textos.
    /// </summary>
    [HelpURL(DocUrls.UiTooltip + "#tooltiplineview")]
    public sealed class TooltipLineView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Rótulo da linha; escondido quando a linha não tem rótulo.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("Valor da linha, pintado pelo papel.")]
        private TMP_Text value;

        /// <summary>O texto do rótulo, ou <c>null</c>.</summary>
        public TMP_Text Label => label;

        /// <summary>O texto do valor, ou <c>null</c>.</summary>
        public TMP_Text Value => value;
    }
}
