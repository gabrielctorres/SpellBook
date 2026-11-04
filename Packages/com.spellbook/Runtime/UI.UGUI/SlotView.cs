using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do slot de habilidade (R10 do PRD do B-016; Spec, Design 6): ícone, radial e segundos da recarga,
    /// cargas e radial da próxima, rótulo da tecla, motivo da falha, escurecer em <see cref="SlotState.NoResource"/>,
    /// <see cref="SlotState.NoCharges"/> e <see cref="SlotState.Blocked"/>, realce com <see cref="SlotModel.IsActive"/>,
    /// pulso de pronto e tremor da falha pelo <see cref="Animator"/>. Escreve o assunto no <see cref="UIItemContext"/>.
    /// Só exibição: não ativa a habilidade (R11), não lê o tempo e não busca componente (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiAbilitySlot + "#slotview")]
    public sealed class SlotView : MonoBehaviour, IView<SlotModel>
    {
        [SerializeField]
        [Tooltip("Filho com o desenho do slot, escondido no slot vazio com Hide When Empty. Opcional.")]
        private GameObject content;

        [SerializeField]
        [Tooltip("Ícone da habilidade; desligado sem ícone.")]
        private Image icon;

        [SerializeField]
        [Tooltip("Máscara da recarga (tipo Filled, radial); recebe a fração que falta.")]
        private Image cooldownFill;

        [SerializeField]
        [Tooltip("Segundos de recarga que faltam, inteiros e arredondados para cima. Opcional.")]
        private TMP_Text cooldownText;

        [SerializeField]
        [Tooltip("Cargas atuais, só para habilidade com cargas. Opcional.")]
        private TMP_Text chargesText;

        [SerializeField]
        [Tooltip("Radial da próxima carga (tipo Filled), só para habilidade com cargas. Opcional.")]
        private Image chargeFill;

        [SerializeField]
        [Tooltip("Rótulo da tecla. Opcional.")]
        private TMP_Text keyLabel;

        [SerializeField]
        [Tooltip("Motivo da última falha, enquanto ele aparece. Opcional.")]
        private TMP_Text failureText;

        [SerializeField]
        [Tooltip("Realce ligado com a habilidade ativa. Use um filho, não o objeto desta view.")]
        private GameObject highlight;

        [SerializeField]
        [Tooltip("Cor do ícone sem recurso, sem cargas ou bloqueado.")]
        private Color dimmedTint = new Color(0.35f, 0.35f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Esconde o conteúdo no slot vazio; desligado, o slot vazio mostra só a moldura.")]
        private bool hideWhenEmpty;

        [SerializeField]
        [Tooltip("Contexto do item, que recebe a habilidade como assunto. Opcional.")]
        private UIItemContext itemContext;

        [SerializeField]
        [Tooltip("Animator com os gatilhos do pulso e do tremor. Opcional.")]
        private Animator animator;

        [SerializeField]
        [Tooltip("Gatilho disparado quando o slot volta a ficar pronto.")]
        private string pulseTrigger = "Pulse";

        [SerializeField]
        [Tooltip("Gatilho disparado a cada falha de ativação.")]
        private string shakeTrigger = "Shake";

        // Buffer dos números, criado uma vez: segundos e cargas são escritos do zero a cada desenho
        private readonly TextBuffer _numbers = new TextBuffer(16);

        // Base dos contadores: só uma mudança entre dois desenhos do mesmo assunto anima
        private ScriptableObject _lastSubject;
        private int _lastPulse;
        private int _lastFailure;
        private bool _hasBase;

        /// <inheritdoc />
        public void Render(in SlotModel model)
        {
            bool empty = model.State == SlotState.Empty;
            if (itemContext != null) itemContext.SetSubject(empty ? null : model.Subject);
            if (content != null) SetActive(content, !(empty && hideWhenEmpty));

            Animate(model, empty);
            if (empty)
            {
                ShowFrameOnly();
                return;
            }

            if (icon != null)
            {
                icon.sprite = model.Icon;
                icon.enabled = model.Icon != null;
                icon.color = IsDimmed(model.State) ? dimmedTint : Color.white;
            }

            if (highlight != null) SetActive(highlight, model.IsActive);
            if (cooldownFill != null) cooldownFill.fillAmount = model.CooldownFraction;
            if (chargeFill != null) chargeFill.fillAmount = model.HasCharges ? model.ChargeFraction : 0f;

            _numbers.Clear();
            if (cooldownText != null)
            {
                WriteOrClear(cooldownText, model.CooldownRemaining > 0f, Mathf.Ceil(model.CooldownRemaining));
            }

            if (chargesText != null) WriteOrClear(chargesText, model.HasCharges, model.Charges);
            if (keyLabel != null) RoleColors.Write(keyLabel, model.KeyLabel);
            if (failureText != null) RoleColors.Write(failureText, model.FailureVisible ? model.FailureText : default);
        }

        private static bool IsDimmed(SlotState state) =>
            state == SlotState.NoResource || state == SlotState.NoCharges || state == SlotState.Blocked;

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        // Moldura vazia: sem ícone, radiais, textos nem realce
        private void ShowFrameOnly()
        {
            if (icon != null) icon.enabled = false;
            if (highlight != null) SetActive(highlight, false);
            if (cooldownFill != null) cooldownFill.fillAmount = 0f;
            if (chargeFill != null) chargeFill.fillAmount = 0f;
            if (cooldownText != null) RoleColors.Write(cooldownText, default);
            if (chargesText != null) RoleColors.Write(chargesText, default);
            if (keyLabel != null) RoleColors.Write(keyLabel, default);
            if (failureText != null) RoleColors.Write(failureText, default);
        }

        private void WriteOrClear(TMP_Text target, bool show, float value)
        {
            RoleColors.Write(target, show ? _numbers.Append(value, "0", null) : default);
        }

        // Primeiro desenho, outro assunto ou vazio só guardam a base (R8); depois, um gatilho por mudança de contador
        private void Animate(in SlotModel model, bool empty)
        {
            bool sameSubject = _hasBase && ReferenceEquals(_lastSubject, model.Subject);
            if (sameSubject && !empty && animator != null && animator.isActiveAndEnabled
                && animator.runtimeAnimatorController != null)
            {
                if (model.ReadyPulseCount != _lastPulse) animator.SetTrigger(pulseTrigger);
                if (model.FailureCount != _lastFailure) animator.SetTrigger(shakeTrigger);
            }

            _hasBase = !empty;
            _lastSubject = empty ? null : model.Subject;
            _lastPulse = model.ReadyPulseCount;
            _lastFailure = model.FailureCount;
        }
    }
}
