using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI do tooltip (Spec UI-3, Design 5): painel com título, subtítulo, ícone, linhas (rótulo e valor, este
    /// pintado pelo papel), pilhas, tempo restante e rodapé. Escreve os valores por <c>SetCharArray</c> e desliga o rich
    /// text dos textos que escreve: o modelo não traz markup. Posiciona o painel pelo <see cref="TooltipPlacement"/>.
    /// Só exibição (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiTooltip + "#tooltipview")]
    public sealed class TooltipView : MonoBehaviour, IView<TooltipModel>, ITooltipPlacementTarget
    {
        [SerializeField]
        [Tooltip("Painel do tooltip: escondido no modelo vazio e movido para perto do alvo.")]
        private RectTransform panel;

        [SerializeField]
        [Tooltip("Título.")]
        private TMP_Text title;

        [SerializeField]
        [Tooltip("Subtítulo; escondido sem texto. Opcional.")]
        private TMP_Text subtitle;

        [SerializeField]
        [Tooltip("Ícone; desligado sem ícone. Opcional.")]
        private Image icon;

        [SerializeField]
        [Tooltip("Linha-modelo, desligada; a view a clona para cada linha do modelo.")]
        private TooltipLineView lineTemplate;

        [SerializeField]
        [Tooltip("Spawner que cria os clones da linha-modelo. Vazio: o spawner padrão.")]
        private SpawnerAsset spawner;

        [SerializeField]
        [Tooltip("Pilhas; mostrado só quando o modelo tem pilhas. Opcional.")]
        private TMP_Text stacks;

        [SerializeField]
        [Tooltip("Tempo restante; mostrado só quando o modelo tem tempo. Opcional.")]
        private TMP_Text remaining;

        [SerializeField]
        [Tooltip("Rodapé; escondido sem texto. Opcional.")]
        private TMP_Text footer;

        [SerializeField]
        [Tooltip("Tema com as cores por papel. Vazio: o tema padrão do pacote.")]
        private UITheme theme;

        [SerializeField]
        [Tooltip("Área de que o tooltip não sai. Vazio: o pai do painel.")]
        private RectTransform area;

        [SerializeField]
        [Tooltip("Lado do alvo em que o tooltip prefere abrir; inverte na borda.")]
        private TooltipSide side = TooltipSide.Right;

        [SerializeField]
        [Tooltip("Distância entre o alvo e o tooltip.")]
        private float gap = 8f;

        // Clones da linha-modelo, na ordem das linhas, reaproveitados entre desenhos
        private readonly List<TooltipLineView> _rows = new List<TooltipLineView>();
        private readonly TextBuffer _numbers = new TextBuffer(16);
        private readonly Vector3[] _corners = new Vector3[4];

        /// <inheritdoc />
        public void Render(in TooltipModel model)
        {
            if (model.IsEmpty)
            {
                if (panel != null) SetActive(panel.gameObject, false);
                return;
            }

            if (panel != null) SetActive(panel.gameObject, true);
            if (title != null) WritePlain(title, model.Title);
            if (subtitle != null) WritePlain(subtitle, model.Subtitle);
            if (footer != null) WritePlain(footer, model.Footer);
            if (icon != null)
            {
                icon.sprite = model.Icon;
                icon.enabled = model.Icon != null;
            }

            RenderLines(model.Lines);
            _numbers.Clear();
            if (stacks != null) WriteSpan(stacks, model.HasStacks, model.HasStacks ? _numbers.Append(model.Stacks, "0", null) : default);
            if (remaining != null) WriteSpan(remaining, model.HasRemaining, model.RemainingText);
        }

        /// <inheritdoc />
        public void Place(RectTransform target)
        {
            RectTransform bounds = area != null ? area : panel != null ? panel.parent as RectTransform : null;
            if (target == null || panel == null || bounds == null) return;

            // Cantos do alvo no espaço da área; o layout é refeito para o tamanho do painel valer o conteúdo atual
            target.GetWorldCorners(_corners);
            Vector2 min = bounds.InverseTransformPoint(_corners[0]);
            Vector2 max = bounds.InverseTransformPoint(_corners[2]);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Vector2 size = panel.rect.size;
            Rect placed = TooltipPlacement.Compute(Rect.MinMaxRect(min.x, min.y, max.x, max.y), size, bounds.rect, side, gap);
            panel.position = bounds.TransformPoint(placed.min + Vector2.Scale(size, panel.pivot));
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        // Texto que já existe como string (do provedor); sem texto, o objeto some
        private static void WritePlain(TMP_Text target, string value)
        {
            bool show = !string.IsNullOrEmpty(value);
            SetActive(target.gameObject, show);
            if (!show) return;
            target.richText = false;
            target.text = value;
        }

        private static void WriteSpan(TMP_Text target, bool show, TextSpan value)
        {
            SetActive(target.gameObject, show);
            if (!show) return;
            target.richText = false;
            RoleColors.Write(target, value);
        }

        private void RenderLines(IReadOnlyList<TooltipLine> lines)
        {
            int count = lineTemplate != null ? lines.Count : 0;
            for (int i = 0; i < count; i++)
            {
                TooltipLineView row = Row(i);
                if (row == null)
                {
                    count = i;
                    break;
                }

                SetActive(row.gameObject, true);
                TooltipLine line = lines[i];
                if (row.Label != null) WritePlain(row.Label, line.Label);
                if (row.Value != null)
                {
                    WriteSpan(row.Value, true, line.Value);
                    row.Value.color = RoleColors.Get(theme, line.Role);
                }
            }

            for (int i = count; i < _rows.Count; i++)
            {
                if (_rows[i] != null) SetActive(_rows[i].gameObject, false);
            }
        }

        // A linha i: o clone guardado ou, se falta ou foi destruído, um novo do spawner (ADR 0015) logo depois da linha
        // anterior; nulo se o spawner não criar a instância
        private TooltipLineView Row(int index)
        {
            if (index < _rows.Count && _rows[index] != null) return _rows[index];

            Transform template = lineTemplate.transform;
            SpawnerAsset used = SpawnerAsset.OrDefault(spawner);
            GameObject instance = used.Spawn(lineTemplate.gameObject, template.position, template.rotation, template.parent);
            if (instance == null) return null;

            TooltipLineView row = instance.GetComponent<TooltipLineView>();
            if (row == null)
            {
                used.Release(instance);
                return null;
            }

            instance.transform.SetParent(template.parent, true);
            Transform previous = index > 0 ? _rows[index - 1].transform : lineTemplate.transform;
            row.transform.SetSiblingIndex(previous.GetSiblingIndex() + 1);
            if (index < _rows.Count) _rows[index] = row;
            else _rows.Add(row);
            return row;
        }
    }
}
