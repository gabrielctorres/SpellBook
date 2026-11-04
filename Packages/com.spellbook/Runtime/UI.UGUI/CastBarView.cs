using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI da barra de cast (R20 do PRD do B-016; Spec, Design 5): preenchimento, ícone, nome e segundos da
    /// canalização, marca do modo indeterminado e marcas distintas de concluída e cancelada, com um gatilho do
    /// <see cref="Animator"/> a cada fim. Só exibição: não conhece o GAS, não lê o tempo e não busca componente (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiCastBar + "#castbarview")]
    public sealed class CastBarView : MonoBehaviour, IView<CastBarModel>
    {
        [SerializeField]
        [Tooltip("Filho com o desenho da barra, escondido sem canalização e sem resultado. Opcional.")]
        private GameObject content;

        [SerializeField]
        [Tooltip("Preenchimento (tipo Filled); recebe a fração da canalização.")]
        private Image fill;

        [SerializeField]
        [Tooltip("Marca ligada na canalização sem duração, no lugar do preenchimento. Opcional.")]
        private GameObject indeterminate;

        [SerializeField]
        [Tooltip("Ícone da habilidade; desligado sem ícone.")]
        private Image icon;

        [SerializeField]
        [Tooltip("Nome da habilidade, já resolvido pelo provedor de texto. Opcional.")]
        private TMP_Text nameText;

        [SerializeField]
        [Tooltip("Segundos que faltam, com uma casa. Opcional.")]
        private TMP_Text secondsText;

        [SerializeField]
        [Tooltip("Marca ligada quando a última canalização foi concluída. Opcional.")]
        private GameObject completed;

        [SerializeField]
        [Tooltip("Marca ligada quando a última canalização foi cancelada. Opcional.")]
        private GameObject cancelled;

        [SerializeField]
        [Tooltip("Tema com a cultura dos segundos. Vazio: cultura invariante.")]
        private UITheme theme;

        [SerializeField]
        [Tooltip("Animator com o estado da canalização e os gatilhos do resultado. Opcional.")]
        private Animator animator;

        [SerializeField]
        [Tooltip("Parâmetro bool ligado enquanto há canalização.")]
        private string channelingParameter = "Channeling";

        [SerializeField]
        [Tooltip("Gatilho disparado quando uma canalização é concluída.")]
        private string completedTrigger = "Completed";

        [SerializeField]
        [Tooltip("Gatilho disparado quando uma canalização é cancelada.")]
        private string cancelledTrigger = "Cancelled";

        // Buffer dos segundos, criado uma vez e escrito do zero a cada desenho
        private readonly TextBuffer _seconds = new TextBuffer(16);

        // Base do contador de resultados: só uma mudança entre dois desenhos com base anima
        private int _lastResultCount;
        private bool _hasBase;
        private string _writtenName;
        private bool _nameWritten;

        /// <inheritdoc />
        public void Render(in CastBarModel model)
        {
            bool channeling = model.State == CastBarState.Channeling;
            bool empty = !channeling && model.LastResult == CastBarResult.None;
            Animate(model, channeling, empty);
            if (content != null) SetActive(content, !empty);
            if (empty) return;

            if (icon != null)
            {
                icon.sprite = model.Icon;
                icon.enabled = model.Icon != null;
            }

            // O nome é a string do provedor: entregue só quando muda, não a cada tick
            if (nameText != null && (!_nameWritten || !ReferenceEquals(_writtenName, model.Name)))
            {
                nameText.text = model.Name;
                _writtenName = model.Name;
                _nameWritten = true;
            }

            bool timed = channeling && !model.Indeterminate;
            if (indeterminate != null) SetActive(indeterminate, channeling && model.Indeterminate);
            if (completed != null) SetActive(completed, !channeling && model.LastResult == CastBarResult.Completed);
            if (cancelled != null) SetActive(cancelled, !channeling && model.LastResult == CastBarResult.Cancelled);
            if (fill != null)
            {
                // Cancelada: o preenchimento fica onde parou
                if (channeling) fill.fillAmount = timed ? model.Fraction : 0f;
                else if (model.LastResult == CastBarResult.Completed) fill.fillAmount = 1f;
            }

            if (secondsText != null)
            {
                _seconds.Clear();
                RoleColors.Write(secondsText,
                    timed ? _seconds.Append(model.Remaining, "0.0", theme != null ? theme.Culture : null) : default);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        // Primeiro desenho e o vazio (troca de dono) só guardam a base; depois, um gatilho por mudança do contador
        private void Animate(in CastBarModel model, bool channeling, bool empty)
        {
            if (animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(channelingParameter, channeling);
                if (_hasBase && !empty && model.ResultCount != _lastResultCount)
                {
                    animator.SetTrigger(model.LastResult == CastBarResult.Cancelled ? cancelledTrigger : completedTrigger);
                }
            }

            _hasBase = !empty;
            _lastResultCount = model.ResultCount;
        }
    }
}
