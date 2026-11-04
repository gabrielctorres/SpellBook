using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Driver único de tick da UI (ADR 0039, item 1; C7). Guarda só as ligações registradas que pedem tick e chama
    /// <see cref="UIBinding.Tick"/> delas com o tempo da fonte. Sem ligação pedindo, fica desabilitado e o <c>Update</c>
    /// não roda; com a fonte em pausa, o tempo entregue fica parado. Os binders filhos acham o driver no pai.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(DocUrls.Ui + "#uiupdatedriver")]
    public sealed class UIUpdateDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Fonte do tempo entregue às ligações. Vazio: tempo escalado.")]
        private TimeSourceAsset timeSource;

        private readonly List<UIBinding> _ticking = new List<UIBinding>();

        // Posição do laço do Tick; -1 fora dele
        private int _cursor = -1;

        /// <summary>A fonte de tempo em uso; o campo vazio vale <see cref="TimeSourceAsset.Scaled"/>.</summary>
        public TimeSourceAsset TimeSource => timeSource != null ? timeSource : TimeSourceAsset.Scaled;

        /// <summary>Quantas ligações registradas pedem tick agora.</summary>
        public int TickingCount => _ticking.Count;

        /// <summary>Troca a fonte de tempo (nulo vale o tempo escalado).</summary>
        public void SetTimeSource(TimeSourceAsset source)
        {
            timeSource = source;
        }

        /// <summary>
        /// Registra <paramref name="binding"/>: ela entra na lista enquanto pedir tick. Se estava em outro driver, sai dele
        /// antes. Sem efeito se já é deste driver.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="binding"/> é nula.</exception>
        public void Register(UIBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            if (ReferenceEquals(binding.Driver, this))
            {
                return;
            }

            if (binding.Driver != null)
            {
                binding.Driver.Unregister(binding);
            }

            binding.Driver = this;
            OnWantsTickChanged(binding);
        }

        /// <summary>Tira <paramref name="binding"/> do driver. Sem efeito se ela não é deste driver.</summary>
        public void Unregister(UIBinding binding)
        {
            if (binding == null || !ReferenceEquals(binding.Driver, this))
            {
                return;
            }

            binding.Driver = null;
            Remove(binding);
        }

        /// <summary>
        /// Chama <see cref="UIBinding.Tick"/> de cada ligação que pede tick, com o tempo da fonte lido uma vez. Uma ligação
        /// pode sair ou entrar durante o laço. Não aloca.
        /// </summary>
        public void Tick()
        {
            if (_ticking.Count == 0)
            {
                return;
            }

            double time = TimeSource.ElapsedTime;
            for (_cursor = 0; _cursor < _ticking.Count; _cursor++)
            {
                _ticking[_cursor].Tick(time);
            }

            _cursor = -1;
        }

        internal void OnWantsTickChanged(UIBinding binding)
        {
            if (binding.WantsTick)
            {
                if (!_ticking.Contains(binding))
                {
                    _ticking.Add(binding);
                }
            }
            else
            {
                Remove(binding);
            }

            UpdateEnabled();
        }

        private void Awake()
        {
            UpdateEnabled();
        }

        private void Update()
        {
            Tick();
        }

        private void Remove(UIBinding binding)
        {
            int index = _ticking.IndexOf(binding);
            if (index < 0)
            {
                return;
            }

            _ticking.RemoveAt(index);

            // A saída antes do cursor puxa a lista: o cursor volta um para não pular a próxima
            if (index <= _cursor)
            {
                _cursor--;
            }

            UpdateEnabled();
        }

        private void UpdateEnabled()
        {
            enabled = _ticking.Count > 0;
        }
    }
}
