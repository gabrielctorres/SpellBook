using SpellBook.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI da barra (R11): preenchimento e rastro por <see cref="Image.fillAmount"/>, texto opcional no TextMeshPro
    /// sem criar <see cref="string"/> e cor do preenchimento pelo papel do modelo, lida do <see cref="UITheme"/>. Só
    /// desenha o modelo; não busca dono, fonte nem componente (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiAttributeBar)]
    public sealed class BarView : MonoBehaviour, IView<BarModel>
    {
        [SerializeField]
        [Tooltip("Imagem do preenchimento (tipo Filled); recebe a fração e a cor do papel.")]
        private Image fill;

        [SerializeField]
        [Tooltip("Imagem do rastro atrasado (tipo Filled), atrás do preenchimento. Opcional.")]
        private Image trail;

        [SerializeField]
        [Tooltip("Texto da barra, por exemplo \"50 / 100\". Opcional; mantém a cor do próprio componente.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("Tema com as cores por papel. Vazio: o tema padrão do pacote.")]
        private UITheme theme;

        /// <inheritdoc />
        public void Render(in BarModel model)
        {
            if (model.IsEmpty)
            {
                if (fill != null) fill.fillAmount = 0f;
                if (trail != null) trail.fillAmount = 0f;
                if (label != null) label.text = string.Empty;
                return;
            }

            if (fill != null)
            {
                fill.fillAmount = model.Fraction;
                fill.color = RoleColors.Get(theme, model.Role);
            }

            if (trail != null) trail.fillAmount = model.TrailFraction;
            if (label != null) RoleColors.Write(label, model.Text);
        }
    }
}
