using SpellBook.GAS.Core;
using UnityEngine;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Alvo de uma ativação ou de uma condição (ADR 0032, item 8): um ponto no mundo e, opcionalmente, uma unidade.
    /// <see cref="None"/> é o alvo vazio, sem ponto e sem unidade.
    /// </summary>
    public readonly struct AbilityTarget
    {
        private AbilityTarget(Vector3 point, AbilitySystemComponent unit)
        {
            HasPoint = true;
            Point = point;
            Unit = unit;
        }

        /// <summary>Alvo vazio, sem ponto e sem unidade.</summary>
        public static AbilityTarget None => default;

        /// <summary>O alvo tem um ponto em <see cref="Point"/>.</summary>
        public bool HasPoint { get; }

        /// <summary>Ponto do alvo; <see cref="Vector3.zero"/> quando <see cref="HasPoint"/> é falso.</summary>
        public Vector3 Point { get; }

        /// <summary>Unidade-alvo; nula num alvo só de ponto.</summary>
        public AbilitySystemComponent Unit { get; }

        /// <summary>Alvo no ponto <paramref name="point"/>, sem unidade.</summary>
        public static AbilityTarget AtPoint(Vector3 point) => new AbilityTarget(point, null);

        /// <summary>
        /// Alvo na unidade <paramref name="unit"/>, com a posição dela como ponto. Unidade nula dá <see cref="None"/>.
        /// </summary>
        public static AbilityTarget AtUnit(AbilitySystemComponent unit)
        {
            return unit != null ? new AbilityTarget(unit.transform.position, unit) : None;
        }

        /// <summary>
        /// Alvo na unidade <paramref name="unit"/> com o ponto <paramref name="point"/> no lugar da posição dela. Unidade
        /// nula dá o alvo só de ponto.
        /// </summary>
        public static AbilityTarget AtUnit(AbilitySystemComponent unit, Vector3 point) => new AbilityTarget(point, unit);
    }
}
