using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do ícone de efeito (R9 do PRD do B-018; Spec, Design 3): imagem, com o ícone padrão quando o efeito não
    /// tem (CA17), moldura pela cor do papel no <see cref="UITheme"/>, radial e segundos da duração, pilhas e o sinal de
    /// fim pelo bool do <see cref="Animator"/>, que pisca sem código por quadro. No infinito, sem relógio. Escreve o
    /// assunto no <see cref="UIItemContext"/>. Só exibição: não conhece o GAS, não lê o tempo e não busca componente (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiEffectList + "#effecticonview")]
    public sealed class EffectIconView : MonoBehaviour, IView<EffectIconModel>
    {
        [SerializeField]
        [Tooltip("Moldura; recebe a cor do papel do efeito (benéfico, nocivo ou neutro).")]
        private Image frame;

        [SerializeField]
        [Tooltip("Ícone do efeito; sem ícone, mostra o padrão; sem os dois, fica desligado.")]
        private Image icon;

        [SerializeField]
        [Tooltip("Ícone usado quando o efeito não tem ícone. Opcional.")]
        private Sprite defaultIcon;

        [SerializeField]
        [Tooltip("Radial da duração (tipo Filled); recebe a fração que falta. Vazio no infinito. Opcional.")]
        private Image durationFill;

        [SerializeField]
        [Tooltip("Segundos que faltam. Vazio no infinito. Opcional.")]
        private TMP_Text secondsText;

        [SerializeField]
        [Tooltip("Pilhas, mostradas a partir do mínimo da fonte. Opcional.")]
        private TMP_Text stacksText;

        [SerializeField]
        [Tooltip("Tema com as cores por papel. Vazio: o tema padrão do pacote.")]
        private UITheme theme;

        [SerializeField]
        [Tooltip("Contexto do item, que recebe o efeito como assunto. Opcional.")]
        private UIItemContext itemContext;

        [SerializeField]
        [Tooltip("Animator com o bool do fim próximo, que faz o ícone piscar. Opcional.")]
        private Animator animator;

        [SerializeField]
        [Tooltip("Parâmetro bool ligado nos últimos segundos do efeito.")]
        private string endingSoonParameter = "EndingSoon";

        // Buffer das pilhas, criado uma vez e escrito do zero a cada desenho
        private readonly TextBuffer _stacks = new TextBuffer(8);

        /// <inheritdoc />
        public void Render(in EffectIconModel model)
        {
            if (itemContext != null) itemContext.SetSubject(model.Subject);
            if (frame != null) frame.color = RoleColors.Get(theme, model.Role);

            if (icon != null)
            {
                Sprite sprite = model.Icon != null ? model.Icon : defaultIcon;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            bool timed = !model.IsInfinite;
            if (durationFill != null) durationFill.fillAmount = timed ? model.Fraction : 0f;
            if (secondsText != null) RoleColors.Write(secondsText, timed ? model.RemainingText : default);

            if (stacksText != null)
            {
                _stacks.Clear();
                RoleColors.Write(stacksText, model.ShowStacks ? _stacks.Append(model.Stacks, "0", null) : default);
            }

            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(endingSoonParameter, timed && model.EndingSoon);
            }
        }
    }
}
