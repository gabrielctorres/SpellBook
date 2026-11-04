using NUnit.Framework;
using UnityEngine;

namespace SpellBook.UI.Tests
{
    /// <summary>Posição do tooltip como função pura, sem Canvas (Spec UI-3, P4; CA8, parte automatizável).</summary>
    public class TooltipPlacementTests
    {
        private static readonly Rect Bounds = new Rect(0f, 0f, 1000f, 600f);
        private const float Gap = 10f;

        // Alvo (x, y, largura, altura), tamanho do tooltip, lado preferido e o canto mínimo esperado
        [TestCase(100f, 300f, 50f, 50f, 200f, 100f, TooltipSide.Right, 160f, 250f)] // Right_Fits_TopAligned
        [TestCase(850f, 300f, 50f, 50f, 200f, 100f, TooltipSide.Right, 640f, 250f)] // Right_AtEdge_FlipsLeft
        [TestCase(100f, 300f, 50f, 50f, 200f, 100f, TooltipSide.Left, 160f, 250f)] // Left_AtEdge_FlipsRight
        [TestCase(100f, 20f, 50f, 50f, 200f, 100f, TooltipSide.Right, 160f, 0f)] // Right_NearBottom_ClampsUp
        [TestCase(100f, 580f, 50f, 50f, 200f, 100f, TooltipSide.Right, 160f, 500f)] // Right_NearTop_ClampsDown
        [TestCase(380f, 300f, 200f, 50f, 450f, 100f, TooltipSide.Left, 550f, 250f)] // NoSideFits_MoreRoom_Inside
        [TestCase(100f, 300f, 50f, 50f, 200f, 100f, TooltipSide.Above, 100f, 360f)] // Above_Fits_LeftAligned
        [TestCase(100f, 300f, 50f, 50f, 200f, 100f, TooltipSide.Below, 100f, 190f)] // Below_Fits
        [TestCase(100f, 520f, 50f, 50f, 200f, 100f, TooltipSide.Above, 100f, 410f)] // Above_AtEdge_FlipsBelow
        [TestCase(950f, 300f, 40f, 50f, 200f, 100f, TooltipSide.Below, 800f, 190f)] // Below_NearRight_ClampsLeft
        [TestCase(100f, 300f, 50f, 50f, 1200f, 100f, TooltipSide.Right, 0f, 250f)] // LargerThanBounds_AtMin
        public void Placement_PreferredSide_FlipsAtEdge_ClampsVertical(
            float targetX, float targetY, float targetWidth, float targetHeight,
            float width, float height, TooltipSide side, float expectedX, float expectedY)
        {
            var target = new Rect(targetX, targetY, targetWidth, targetHeight);
            var size = new Vector2(width, height);

            Rect placed = TooltipPlacement.Compute(target, size, Bounds, side, Gap);

            Assert.That(placed.position, Is.EqualTo(new Vector2(expectedX, expectedY)));
            Assert.That(placed.size, Is.EqualTo(size), "a posição não muda o tamanho do tooltip");
        }
    }
}
