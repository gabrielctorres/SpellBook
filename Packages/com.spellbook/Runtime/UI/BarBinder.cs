using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder da barra (R7) com rastro atrasado: na queda da fração, segura o rastro no valor antigo, espera
    /// <see cref="TrailDelay"/> e o baixa a <see cref="TrailSpeed"/> por segundo até a fração, pelo tick do
    /// <see cref="UIUpdateDriver"/>; só uma nova queda reinicia o atraso. Só pede tick enquanto o rastro está acima da
    /// fração. Na subida, sem driver, em edição ou com velocidade 0, o rastro acompanha a fração na hora.
    /// </summary>
    [HelpURL(DocUrls.UiAttributeBar)]
    public sealed class BarBinder : UIBinder<BarModel>
    {
        [SerializeField, Min(0f)]
        [Tooltip("Segundos que o rastro espera depois de uma queda antes de baixar.")]
        private float trailDelay = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Fração da barra que o rastro baixa por segundo. 0 desliga o rastro.")]
        private float trailSpeed = 1f;

        private BarModel _model;
        private float _trail;
        private bool _restartHold;
        private double _holdUntil;
        private double _lastTick;

        /// <summary>Segundos de espera do rastro depois de uma queda.</summary>
        public float TrailDelay => trailDelay;

        /// <summary>Fração que o rastro baixa por segundo; 0 desliga o rastro.</summary>
        public float TrailSpeed => trailSpeed;

        /// <summary>Define o atraso e a velocidade do rastro (negativos valem 0).</summary>
        public void SetTrail(float delay, float speed)
        {
            trailDelay = Mathf.Max(0f, delay);
            trailSpeed = Mathf.Max(0f, speed);
        }

        /// <inheritdoc />
        protected override void OnModel(in BarModel model)
        {
            float previous = _model.IsEmpty ? 0f : _model.Fraction;
            bool hadValue = !_model.IsEmpty;
            _model = model;
            float fraction = model.Fraction;
            if (model.IsEmpty || !hadValue || fraction > previous || fraction >= _trail || trailSpeed <= 0f)
            {
                // Subida, vazio ou rastro desligado: rastro na fração e sem tick
                _trail = fraction;
                _restartHold = false;
                RequestBinderTick(false);
            }
            else if (fraction < previous)
            {
                // Só uma nova queda reinicia o atraso; a mesma fração republicada não mexe no rastro
                _restartHold = true;
                if (!RequestBinderTick(true))
                {
                    _trail = fraction;
                }
            }

            base.OnModel(model.WithTrail(_trail));
        }

        // Ao desligar a fonte (desabilitar, religar ou trocar de âncora) o rastro zera: o tick é solto, e a fonte
        // republica o estado atual como primeiro valor, com o rastro na fração
        private protected override void DetachBinding()
        {
            base.DetachBinding();
            _model = default;
            _trail = 0f;
            _restartHold = false;
        }

        /// <inheritdoc />
        protected override void OnBinderTick(double time)
        {
            if (trailSpeed <= 0f)
            {
                _trail = _model.Fraction;
                _restartHold = false;
                RequestBinderTick(false);
                base.OnModel(_model);
                return;
            }

            if (_restartHold)
            {
                // O atraso conta a partir do primeiro tick depois da queda: o tempo só vem do driver
                _restartHold = false;
                _holdUntil = time + trailDelay;
                _lastTick = time;
                return;
            }

            double from = _lastTick > _holdUntil ? _lastTick : _holdUntil;
            _lastTick = time;
            if (time <= from)
            {
                return;
            }

            _trail = Mathf.Max(_model.Fraction, _trail - (float)((time - from) * trailSpeed));
            if (_trail <= _model.Fraction)
            {
                RequestBinderTick(false);
            }

            base.OnModel(_model.WithTrail(_trail));
        }
    }
}
