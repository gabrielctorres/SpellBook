using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder dos números flutuantes (R14). Cada pedido publicado pela fonte cria um número só pelo
    /// <see cref="SpawnerAsset"/> (ADR 0015), entrega o pedido ao <see cref="IFloatingTextView"/> da instância e, pelo
    /// tick do <see cref="UIUpdateDriver"/> (ADR 0014), avança o progresso e devolve o número ao spawner no fim da duração
    /// do estilo. Sem prefab, sem driver ou com o spawner sem instância, o número é pulado com um aviso em
    /// desenvolvimento, sem exceção. Nunca usa <c>Instantiate</c>, <c>Destroy</c> nem corrotina. Em edição, nada nasce.
    /// </summary>
    [HelpURL(DocUrls.UiFloatingText)]
    public sealed class FloatingTextBinder : UIBinder<FloatingTextRequest>
    {
        private const int InitialCapacity = 16;

        [SerializeField]
        [Tooltip("Prefab do número, com um componente que implementa IFloatingTextView.")]
        private GameObject prefab;

        [SerializeField]
        [Tooltip("Spawner que cria e recebe de volta os números. Vazio: o spawner padrão.")]
        private SpawnerAsset spawner;

        [SerializeField]
        [Tooltip("Pai dos números criados. Vazio: o objeto deste binder.")]
        private Transform container;

        // Views já lidas, por instância: o pool devolve as mesmas instâncias
        private readonly List<GameObject> _known = new List<GameObject>(InitialCapacity);
        private readonly List<IFloatingTextView> _knownViews = new List<IFloatingTextView>(InitialCapacity);
        private readonly List<LiveNumber> _live = new List<LiveNumber>(InitialCapacity);
        private bool _warnedPrefab;
        private bool _warnedDriver;
        private bool _warnedSpawn;
        private bool _warnedView;

        /// <summary>Prefab do número.</summary>
        public GameObject Prefab => prefab;

        /// <summary>O spawner do campo; vazio, o binder usa o <see cref="SpawnerAsset.Default"/>.</summary>
        public SpawnerAsset Spawner => spawner;

        /// <summary>O pai do campo; vazio, os números nascem sob este binder.</summary>
        public Transform Container => container;

        /// <inheritdoc />
        protected override void OnModel(in FloatingTextRequest model)
        {
            // Estado vazio e prévia não criam número
            if (model.IsEmpty || !Application.isPlaying)
            {
                return;
            }

            if (prefab == null)
            {
                WarnOnce(ref _warnedPrefab, "sem prefab");
                return;
            }

            if (!RequestBinderTick(true))
            {
                WarnOnce(ref _warnedDriver, "sem UIUpdateDriver ativo, o número não voltaria ao spawner");
                return;
            }

            Transform parent = container != null ? container : transform;
            GameObject instance = SpawnerAsset.OrDefault(spawner).Spawn(prefab, model.WorldPosition, Quaternion.identity, parent);
            IFloatingTextView view = instance != null ? FindView(instance) : null;
            if (view == null)
            {
                if (instance != null)
                {
                    SpawnerAsset.OrDefault(spawner).Release(instance);
                    WarnOnce(ref _warnedView, "o prefab não tem um componente IFloatingTextView");
                }
                else
                {
                    WarnOnce(ref _warnedSpawn, "o spawner não criou a instância");
                }

                RequestBinderTick(_live.Count > 0);
                return;
            }

            float duration = model.Style != null ? model.Style.Duration : FloatingTextStyle.DefaultDuration;
            _live.Add(new LiveNumber(instance, view, duration));
            view.Show(model);
        }

        /// <inheritdoc />
        protected override void OnBinderTick(double time)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                LiveNumber number = _live[i];
                if (!number.Started)
                {
                    // O início conta do primeiro tick depois do nascimento: o tempo só vem do driver
                    number = number.StartAt(time);
                    _live[i] = number;
                }

                double elapsed = time - number.Start;
                float progress = number.Duration > 0f ? Mathf.Clamp01((float)(elapsed / number.Duration)) : 1f;
                number.View.SetProgress(progress);
                if (progress >= 1f)
                {
                    ReleaseAt(i);
                }
            }

            if (_live.Count == 0)
            {
                RequestBinderTick(false);
            }
        }

        // Ao desligar a fonte, o tick é solto: os números vivos voltam ao spawner na hora
        private protected override void DetachBinding()
        {
            base.DetachBinding();
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                ReleaseAt(i);
            }
        }

        private void ReleaseAt(int index)
        {
            GameObject instance = _live[index].Instance;
            int last = _live.Count - 1;
            _live[index] = _live[last];
            _live.RemoveAt(last);
            if (instance != null)
            {
                SpawnerAsset.OrDefault(spawner).Release(instance);
            }
        }

        private IFloatingTextView FindView(GameObject instance)
        {
            // Instância destruída (spawner sem pool) libera a entrada, para o cache não crescer sem limite
            int free = -1;
            for (int i = 0; i < _known.Count; i++)
            {
                GameObject known = _known[i];
                if (ReferenceEquals(known, instance))
                {
                    return _knownViews[i];
                }

                if (free < 0 && known == null)
                {
                    free = i;
                }
            }

            var view = instance.GetComponent<IFloatingTextView>();
            if (free >= 0)
            {
                _known[free] = instance;
                _knownViews[free] = view;
            }
            else
            {
                _known.Add(instance);
                _knownViews.Add(view);
            }

            return view;
        }

        private void WarnOnce(ref bool warned, string reason)
        {
            if (warned || !Debug.isDebugBuild)
            {
                return;
            }

            warned = true;
            Debug.LogWarning($"[UI] '{name}': número flutuante pulado, {reason}.", this);
        }

        private readonly struct LiveNumber
        {
            public LiveNumber(GameObject instance, IFloatingTextView view, float duration)
                : this(instance, view, duration, 0d, false)
            {
            }

            private LiveNumber(GameObject instance, IFloatingTextView view, float duration, double start, bool started)
            {
                Instance = instance;
                View = view;
                Duration = duration;
                Start = start;
                Started = started;
            }

            public GameObject Instance { get; }

            public IFloatingTextView View { get; }

            public float Duration { get; }

            public double Start { get; }

            public bool Started { get; }

            public LiveNumber StartAt(double time) => new LiveNumber(Instance, View, Duration, time, true);
        }
    }
}
