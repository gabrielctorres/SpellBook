using System;
using System.Globalization;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Tema da UI (ADR 0039): as cores por <see cref="TextRole"/> e o provedor dos textos que os binders entregam às
    /// ligações. Fontes e tamanhos ficam nas views; o tema não conhece o TextMeshPro.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/UI/Theme", fileName = "UITheme")]
    [HelpURL(DocUrls.Ui + "#uitheme")]
    public sealed class UITheme : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Cores por papel do texto. Vazio: o tema padrão do pacote.")]
        private TextRoleTheme roles;

        [SerializeField]
        [Tooltip("Provedor dos textos e da cultura dos números. Vazio: textos padrão e cultura invariante.")]
        private TextProviderAsset text;

        /// <summary>As cores por papel; o campo vazio vale <see cref="TextRoleTheme.Default"/>.</summary>
        public TextRoleTheme Roles => roles != null ? roles : TextRoleTheme.Default;

        /// <summary>O provedor dos textos, ou nulo (textos padrão).</summary>
        public ITextProvider Text => text != null ? text : null;

        /// <summary>A cultura do provedor ou, sem ele, a invariante.</summary>
        public IFormatProvider Culture => text != null ? text.Culture : CultureInfo.InvariantCulture;

        /// <summary>A cor de <paramref name="role"/> em <see cref="Roles"/>. Não aloca.</summary>
        public Color GetColor(TextRole role) => Roles.GetColor(role);
    }
}
