using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>Lado do alvo em que o tooltip prefere abrir.</summary>
    public enum TooltipSide
    {
        Right,
        Left,
        Above,
        Below
    }

    /// <summary>
    /// Posição do tooltip (Spec UI-3, P4): função pura, sem Canvas, com y para cima, como o espaço local do Canvas.
    /// </summary>
    public static class TooltipPlacement
    {
        /// <summary>
        /// O retângulo do tooltip de tamanho <paramref name="size"/> perto de <paramref name="target"/>, dentro de
        /// <paramref name="bounds"/>. Fica no lado <paramref name="preferred"/>, a <paramref name="gap"/> do alvo; se não
        /// cabe ali e cabe no oposto, inverte; se não cabe em nenhum, fica no lado com mais espaço. À direita e à
        /// esquerda, o topo alinha com o do alvo; em cima e embaixo, a borda esquerda alinha com a do alvo. Por fim, o
        /// retângulo é limitado a <paramref name="bounds"/> nos dois eixos; maior que eles, encosta no mínimo.
        /// </summary>
        public static Rect Compute(Rect target, Vector2 size, Rect bounds, TooltipSide preferred, float gap = 0f)
        {
            bool horizontal = preferred == TooltipSide.Right || preferred == TooltipSide.Left;
            float extent = horizontal ? size.x : size.y;
            float roomAfter = horizontal ? bounds.xMax - target.xMax - gap : bounds.yMax - target.yMax - gap;
            float roomBefore = horizontal ? target.xMin - gap - bounds.xMin : target.yMin - gap - bounds.yMin;
            bool after = preferred == TooltipSide.Right || preferred == TooltipSide.Above;
            float room = after ? roomAfter : roomBefore;
            float opposite = after ? roomBefore : roomAfter;
            // Não cabe no preferido: inverte se o oposto tem mais espaço, caiba ou não
            if (room < extent && opposite > room)
            {
                after = !after;
            }

            float x;
            float y;
            if (horizontal)
            {
                x = after ? target.xMax + gap : target.xMin - gap - size.x;
                y = target.yMax - size.y;
            }
            else
            {
                x = target.xMin;
                y = after ? target.yMax + gap : target.yMin - gap - size.y;
            }

            return new Rect(Limit(x, size.x, bounds.xMin, bounds.xMax), Limit(y, size.y, bounds.yMin, bounds.yMax), size.x, size.y);
        }

        // Começo do segmento de comprimento extent dentro de [min, max]; maior que o intervalo, encosta no mínimo
        private static float Limit(float start, float extent, float min, float max) =>
            Mathf.Max(min, Mathf.Min(start, max - extent));
    }
}
