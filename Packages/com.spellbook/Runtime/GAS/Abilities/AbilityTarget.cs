using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Alvo de uma ativação ou de uma condição (ADR 0032, item 8): por enquanto só um ponto no mundo.
    /// <see cref="None"/> é o alvo vazio, sem ponto.
    /// </summary>
    public readonly struct AbilityTarget
    {
        private AbilityTarget(Vector3 point)
        {
            HasPoint = true;
            Point = point;
        }

        /// <summary>Alvo vazio, sem ponto.</summary>
        public static AbilityTarget None => default;

        /// <summary>O alvo tem um ponto em <see cref="Point"/>.</summary>
        public bool HasPoint { get; }

        /// <summary>Ponto do alvo; <see cref="Vector3.zero"/> quando <see cref="HasPoint"/> é falso.</summary>
        public Vector3 Point { get; }

        /// <summary>Alvo no ponto <paramref name="point"/>.</summary>
        public static AbilityTarget AtPoint(Vector3 point) => new AbilityTarget(point);
    }
}
