using SpellBook.Core;
using TMPro;
using UnityEngine;

namespace SpellBook.UI.UGUI
{
    /// <summary>
    /// View uGUI de um número flutuante (R13, R14), no prefab que o <see cref="FloatingTextBinder"/> cria pelo spawner.
    /// No <see cref="Show"/>, converte o ponto do mundo do pedido para o <see cref="RectTransform"/> pai (o container do
    /// binder) pela câmera do canvas, aplica o deslocamento aleatório do estilo, escreve o texto sem criar
    /// <see cref="string"/> e pinta pelo papel. No <see cref="SetProgress"/>, sobe pelo estilo e some no fim. Só recebe o
    /// pedido; não lê o tempo nem volta ao pool sozinha (ADR 0039).
    /// </summary>
    [HelpURL(DocUrls.UiFloatingText)]
    public sealed class FloatingTextView : MonoBehaviour, IFloatingTextView
    {
        [SerializeField]
        [Tooltip("Texto do número; recebe o valor, a cor do papel e o desbotamento.")]
        private TMP_Text text;

        [SerializeField]
        [Tooltip("Tema com as cores por papel. Vazio: o tema padrão do pacote.")]
        private UITheme theme;

        private Vector3 _start;
        private Vector2 _rise;
        private Color _color;

        /// <inheritdoc />
        public void Show(in FloatingTextRequest request)
        {
            FloatingTextStyle style = request.Style;
            _rise = style != null ? style.Rise : Vector2.zero;
            Vector2 offset = style != null ? RandomOffset(style.RandomOffset) : Vector2.zero;
            Vector2 local = ToParentLocal(request.WorldPosition);
            _start = new Vector3(local.x + offset.x, local.y + offset.y, 0f);
            transform.localPosition = _start;

            if (text == null)
            {
                return;
            }

            RoleColors.Write(text, request.Text);
            _color = RoleColors.Get(theme, request.Role);
            _color.a = 1f;
            text.color = _color;
        }

        /// <inheritdoc />
        public void SetProgress(float normalized)
        {
            float progress = Mathf.Clamp01(normalized);
            transform.localPosition = _start + (Vector3)(_rise * progress);

            if (text == null)
            {
                return;
            }

            Color color = _color;
            color.a = 1f - progress;
            text.color = color;
        }

        // Ponto do mundo no plano do pai: projeção pela câmera do canvas; no canvas de mundo, direto
        private Vector2 ToParentLocal(Vector3 world)
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                return world;
            }

            Canvas canvas = text != null ? text.canvas : null;
            if (canvas != null && parent is RectTransform rect)
            {
                Canvas root = canvas.rootCanvas;
                if (root.renderMode != RenderMode.WorldSpace)
                {
                    Camera canvasCamera = root.renderMode == RenderMode.ScreenSpaceCamera ? root.worldCamera : null;
                    // No Overlay o canvas não tem câmera; o prefab não referencia a da cena
                    Camera projection = canvasCamera != null ? canvasCamera : Camera.main;
                    if (projection != null)
                    {
                        Vector2 screen = RectTransformUtility.WorldToScreenPoint(projection, world);
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, canvasCamera, out Vector2 local))
                        {
                            return local;
                        }
                    }
                }
            }

            return parent.InverseTransformPoint(world);
        }

        private static Vector2 RandomOffset(Vector2 range)
        {
            return new Vector2(Random.Range(-range.x, range.x), Random.Range(-range.y, range.y));
        }
    }
}
