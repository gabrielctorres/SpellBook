using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Time
{
    /// <summary>
    /// Grupo pausável: segue o tempo escalado da Unity e pode ser pausado e retomado. Todos os
    /// consumidores que leem o mesmo asset param e voltam juntos. Não tem escala própria (Spec, I-b).
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/Time/Pausable Time Group")]
    [HelpURL(DocUrls.Time + "#pausabletimesource")]
    public sealed class PausableTimeSource : TimeSourceAsset
    {
        private bool _isPaused;
        private double _pausedAt;
        private double _pausedTotal;
        private double _sessionStart;
        private bool _sessionStartPending;

        /// <summary>Se o grupo está pausado.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>Tempo escalado desde o início da sessão, sem os trechos pausados.</summary>
        public override double ElapsedTime
        {
            get
            {
                EnsureSessionStarted();
                double now = _isPaused ? _pausedAt : UnityEngine.Time.timeAsDouble;
                return now - _pausedTotal - _sessionStart;
            }
        }

        /// <summary>0 enquanto pausado; senão, o <c>Time.deltaTime</c>.</summary>
        public override float DeltaTime => _isPaused ? 0f : UnityEngine.Time.deltaTime;

        /// <summary>Pausa o grupo. Sem efeito se já estiver pausado.</summary>
        public void Pause()
        {
            SetPaused(true);
        }

        /// <summary>Retoma o grupo do ponto em que parou. Sem efeito se não estiver pausado.</summary>
        public void Resume()
        {
            SetPaused(false);
        }

        /// <summary>Pausa ou retoma o grupo. Útil num <c>UnityEvent&lt;bool&gt;</c>, como um Toggle.</summary>
        public void SetPaused(bool paused)
        {
            EnsureSessionStarted();
            if (paused == _isPaused)
            {
                return;
            }

            double now = UnityEngine.Time.timeAsDouble;
            if (paused)
            {
                _pausedAt = now;
            }
            else
            {
                _pausedTotal += now - _pausedAt;
            }

            _isPaused = paused;
        }

        protected override void ResetState()
        {
            _isPaused = false;
            _pausedAt = 0d;
            _pausedTotal = 0d;
            _sessionStartPending = true;
        }

        /// <summary>
        /// Grava o início da sessão no primeiro uso depois do reset, e não no reset: o relógio da Unity
        /// não tem valor garantido em <c>SubsystemRegistration</c> (Spec 4.3).
        /// </summary>
        private void EnsureSessionStarted()
        {
            if (!_sessionStartPending)
            {
                return;
            }

            _sessionStart = UnityEngine.Time.timeAsDouble;
            _sessionStartPending = false;
        }
    }
}
