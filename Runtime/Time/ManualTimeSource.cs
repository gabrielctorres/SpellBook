using System;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Time
{
    /// <summary>
    /// Fonte avançada à mão, para testes e simulação. Sem item de menu: crie com
    /// <c>ScriptableObject.CreateInstance&lt;ManualTimeSource&gt;()</c>.
    /// </summary>
    [HelpURL(DocUrls.Time + "#manualtimesource")]
    public sealed class ManualTimeSource : TimeSourceAsset
    {
        private double _elapsedTime;
        private float _deltaTime;

        public override double ElapsedTime => _elapsedTime;
        public override float DeltaTime => _deltaTime;

        /// <summary>Soma <paramref name="seconds"/> ao tempo acumulado e o define como delta.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Se <paramref name="seconds"/> for negativo ou NaN.</exception>
        public void Advance(float seconds)
        {
            if (!(seconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "O avanço não pode ser negativo.");
            }

            _elapsedTime += seconds;
            _deltaTime = seconds;
        }

        protected override void ResetState()
        {
            _elapsedTime = 0d;
            _deltaTime = 0f;
        }
    }
}
