using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do texto (R11): entrega o trecho do buffer ao TextMeshPro sem criar <see cref="string"/> e pinta o texto
    /// pelo papel do modelo, lido do <see cref="UITheme"/>. Só desenha o modelo (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiAttributeText)]
    public sealed class TextView : MonoBehaviour, IView<TextModel>
    {
        [SerializeField]
        [Tooltip("Texto que recebe o modelo.")]
        private TMP_Text text;

        [SerializeField]
        [Tooltip("Tema com as cores por papel. Vazio: o tema padrão do pacote.")]
        private UITheme theme;

        /// <inheritdoc />
        public void Render(in TextModel model)
        {
            if (text == null) return;

            if (model.IsEmpty)
            {
                text.text = string.Empty;
                return;
            }

            RoleColors.Write(text, model.Text);
            text.color = RoleColors.Get(theme, model.Role);
        }
    }

    /// <summary>Cor do papel pelo tema da view e escrita do trecho no TextMeshPro, comuns às views do módulo.</summary>
    internal static class RoleColors
    {
        /// <summary>A cor de <paramref name="role"/> no <paramref name="theme"/> ou, sem tema, no tema padrão. Não aloca.</summary>
        public static Color Get(UITheme theme, TextRole role)
        {
            return theme != null ? theme.GetColor(role) : TextRoleTheme.Default.GetColor(role);
        }

        /// <summary>Entrega os caracteres do trecho ao TextMeshPro pelo <c>SetCharArray</c>, sem criar <see cref="string"/>.</summary>
        public static void Write(TMP_Text target, TextSpan span)
        {
            if (span.IsEmpty)
            {
                target.text = string.Empty;
                return;
            }

            target.SetCharArray(span.Buffer.Chars, span.Start, span.Length);
        }
    }
}
