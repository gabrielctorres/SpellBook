using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Tags;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Toca cues por tag e devolve ao spawner tudo o que eles criam. Componente de cena que se publica na
    /// <see cref="CueRouterAnchor"/>. O tempo de cada cue conta pela fonte de tempo do roteador, então pausa e câmera
    /// lenta valem.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#cuerouter")]
    [DisallowMultipleComponent]
    public sealed class CueRouter : MonoBehaviour
    {
        [SerializeField, Tooltip("Bibliotecas em que o roteador procura o cue de cada tag. Na tag repetida, vale a primeira.")]
        private List<CueLibrary> libraries = new List<CueLibrary>();

        [SerializeField, Tooltip("Âncora em que o roteador se publica enquanto está ativo.")]
        private CueRouterAnchor anchor;

        [SerializeField, Tooltip("Fonte de tempo dos cues. Vazio usa o tempo escalado padrão.")]
        private TimeSourceAsset timeSource;

        [SerializeField, Tooltip("Spawner dos itens sem spawner próprio. Vazio usa o spawner padrão por Instantiate.")]
        private SpawnerAsset defaultSpawner;

        [SerializeField, Tooltip("Prefab com AudioSource e CueAudioPlayer em que os sons tocam.")]
        private GameObject audioPrefab;

        [SerializeField, Min(1), Tooltip("Cues ativos ao mesmo tempo antes de crescer. Crescer avisa e aloca.")]
        private int capacity = 64;

        private readonly CueIndex _index = new CueIndex();
        private readonly List<CuePlayback> _slots = new List<CuePlayback>();
        private readonly List<int> _free = new List<int>();
        private readonly HashSet<Object> _warned = new HashSet<Object>();
        private ITimeSource _timeSourceOverride;
        private IRandomSource _random;
        private double _timeCursor;
        private long _sequence;
        private bool _initialized;
        private bool _warnedGrowth;

        /// <summary>Quantos cues estão ativos.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>
        /// Fonte de tempo em uso: a dada por <see cref="SetTimeSource"/>, senão a do Inspector, senão
        /// <see cref="TimeSourceAsset.Scaled"/>.
        /// </summary>
        public ITimeSource TimeSource
        {
            get
            {
                if (_timeSourceOverride != null) return _timeSourceOverride;
                return timeSource != null ? timeSource : TimeSourceAsset.Scaled;
            }
        }

        /// <summary>
        /// Fonte de aleatoriedade do sorteio de sons, de volume e de altura. Sem <see cref="SetRandomSource"/>, uma com
        /// semente pelo relógio.
        /// </summary>
        public IRandomSource Random => _random ??= new SeededRandomSource(System.Environment.TickCount);

        internal SpawnerAsset DefaultSpawner => SpawnerAsset.OrDefault(defaultSpawner);

        internal GameObject AudioPrefab => audioPrefab;

        // Mensagens da Unity internas para os testes EditMode, em que a Unity não as chama
        internal void Awake()
        {
            EnsureInitialized();
        }

        internal void OnEnable()
        {
            if (anchor != null) anchor.Provide(this);
        }

        internal void OnDisable()
        {
            // Desligado, o roteador não tem Tick: devolve na hora também o que esperava as partículas
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive || _slots[i].IsDraining) Release(_slots[i]);
            }

            if (anchor != null) anchor.Unset(this);
        }

        internal void Update()
        {
            // Consumo por diferença (ADR 0014)
            EnsureInitialized();
            double now = TimeSource.ElapsedTime;
            float deltaTime = (float)(now - _timeCursor);
            _timeCursor = now;
            Tick(deltaTime);
        }

        /// <summary>
        /// Troca a fonte de tempo por código (por exemplo, um <see cref="ManualTimeSource"/> num teste). <c>null</c> volta
        /// à fonte do Inspector. O tempo passado na fonte antiga não é contado.
        /// </summary>
        public void SetTimeSource(ITimeSource source)
        {
            _timeSourceOverride = source;
            _timeCursor = TimeSource.ElapsedTime;
        }

        /// <summary>
        /// Troca a fonte de aleatoriedade (por exemplo, uma <see cref="SeededRandomSource"/> com semente fixa num teste).
        /// <c>null</c> volta a uma com semente pelo relógio.
        /// </summary>
        public void SetRandomSource(IRandomSource source)
        {
            _random = source;
        }

        /// <summary>
        /// Toca o cue <see cref="CueKind.Burst"/> da <paramref name="tag"/>. Falso se nada tocou: tag sem definição, cue
        /// <see cref="CueKind.Looping"/> (use <see cref="Add"/>) ou limite com <see cref="CueLimitPolicy.DropNew"/>.
        /// </summary>
        public bool Execute(GameplayTag tag, in CueParameters parameters)
        {
            if (!TryFind(tag, out CueDefinition cue)) return false;

            if (cue.Kind == CueKind.Looping)
            {
                if (ShouldWarn(cue))
                {
                    Debug.LogWarning($"O cue '{cue.name}' é Looping e não toca por Execute; use Add.", this);
                }

                return false;
            }

            return Play(cue, parameters, out _);
        }

        /// <summary>
        /// Liga o cue da <paramref name="tag"/> e devolve o handle para <see cref="Remove"/>. O handle de um
        /// <see cref="CueKind.Burst"/> deixa de valer quando ele acaba. Handle inválido se nada tocou.
        /// </summary>
        public CueHandle Add(GameplayTag tag, in CueParameters parameters)
        {
            if (!TryFind(tag, out CueDefinition cue) || !Play(cue, parameters, out CueHandle handle)) return default;
            return handle;
        }

        /// <summary>
        /// Desliga o cue e devolve os objetos dele; as partículas param de emitir e voltam quando acabam. Falso se o
        /// handle não vale mais.
        /// </summary>
        public bool Remove(CueHandle handle)
        {
            if (!IsActive(handle)) return false;
            Stop(_slots[handle.Slot]);
            return true;
        }

        /// <summary>Desliga os cues da <paramref name="tag"/> e das tags filhas dela.</summary>
        public void RemoveAll(GameplayTag tag)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                CuePlayback slot = _slots[i];
                if (slot.IsActive && slot.Cue.Tag.Matches(tag)) Stop(slot);
            }
        }

        /// <summary>Desliga todos os cues.</summary>
        public void RemoveAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) Stop(_slots[i]);
            }
        }

        /// <summary>Verdadeiro se o cue do handle ainda está ativo.</summary>
        public bool IsActive(CueHandle handle)
        {
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _slots.Count) return false;
            CuePlayback slot = _slots[handle.Slot];
            return slot.IsActive && slot.Generation == handle.Generation;
        }

        /// <summary>
        /// Avança os cues em <paramref name="deltaTime"/> segundos: segue os alvos, conta as durações e devolve o que
        /// acabou. O próprio roteador chama a cada quadro, pela fonte de tempo.
        /// </summary>
        public void Tick(float deltaTime)
        {
            float delta = deltaTime > 0f ? deltaTime : 0f;
            for (int i = 0; i < _slots.Count; i++)
            {
                CuePlayback slot = _slots[i];
                if ((slot.IsActive || slot.IsDraining) && !slot.Tick(delta)) Release(slot);
            }
        }

        /// <summary>Verdadeiro na primeira vez para <paramref name="key"/>, só em desenvolvimento.</summary>
        internal bool ShouldWarn(Object key)
        {
            return Debug.isDebugBuild && _warned.Add(key);
        }

        private bool TryFind(GameplayTag tag, out CueDefinition cue)
        {
            EnsureInitialized();
            if (_index.TryFind(tag, out cue)) return true;

            if (ShouldWarn(tag.GetDefinition()))
            {
                Debug.LogWarning($"Nenhum cue para a tag '{tag.Name}' nas bibliotecas de '{name}'.", this);
            }

            return false;
        }

        private bool Play(CueDefinition cue, in CueParameters parameters, out CueHandle handle)
        {
            handle = default;
            if (cue.MaxSimultaneous > 0)
            {
                CuePlayback oldest = CountActive(cue, out int count);
                if (count >= cue.MaxSimultaneous)
                {
                    if (cue.LimitPolicy == CueLimitPolicy.DropNew) return false;
                    Stop(oldest);
                }
            }

            CuePlayback slot = Acquire(cue);
            IReadOnlyList<CueItem> items = cue.Items;
            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.Play(parameters, slot);
            }

            PlaySounds(cue, parameters, slot);

            handle = slot.Handle;
            if (!slot.HasObjects) Release(slot);
            return true;
        }

        private void PlaySounds(CueDefinition cue, in CueParameters parameters, CuePlayback slot)
        {
            IReadOnlyList<SoundCueItem> sounds = cue.Sounds;
            if (sounds.Count == 0) return;

            if (cue.SoundSelection == SoundSelection.All)
            {
                for (int i = 0; i < sounds.Count; i++)
                {
                    sounds[i]?.Play(parameters, slot);
                }

                return;
            }

            // Com um som só, não há o que sortear
            int index = sounds.Count == 1 ? 0 : Random.Range(sounds.Count);
            sounds[index]?.Play(parameters, slot);
        }

        private CuePlayback CountActive(CueDefinition cue, out int count)
        {
            count = 0;
            CuePlayback oldest = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                CuePlayback slot = _slots[i];
                if (!slot.IsActive || slot.Cue != cue) continue;

                count++;
                if (oldest == null || slot.Sequence < oldest.Sequence) oldest = slot;
            }

            return oldest;
        }

        private CuePlayback Acquire(CueDefinition cue)
        {
            if (_free.Count == 0)
            {
                if (ShouldWarnGrowth())
                {
                    Debug.LogWarning($"'{name}' passou de {_slots.Count} cues ativos e cresceu; aumente a capacidade.", this);
                }

                AddSlots(_slots.Count);
            }

            int index = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            CuePlayback slot = _slots[index];
            slot.Begin(cue, ++_sequence);
            ActiveCount++;
            return slot;
        }

        /// <summary>Remove o cue; o slot só volta ao livre se não ficou esperando partículas.</summary>
        private void Stop(CuePlayback slot)
        {
            ActiveCount--;
            if (!slot.Stop()) _free.Add(slot.Slot);
        }

        /// <summary>Devolve tudo na hora e libera o slot, ativo ou esvaziando.</summary>
        private void Release(CuePlayback slot)
        {
            if (slot.IsActive) ActiveCount--;
            slot.End();
            _free.Add(slot.Slot);
        }

        private bool ShouldWarnGrowth()
        {
            if (_warnedGrowth || !Debug.isDebugBuild) return false;
            _warnedGrowth = true;
            return true;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _initialized = true;
            _index.Rebuild(libraries);
            AddSlots(Mathf.Max(1, capacity));
            _timeCursor = TimeSource.ElapsedTime;
        }

        private void AddSlots(int count)
        {
            // Os índices livres saem do fim: os mais baixos são usados primeiro
            for (int i = _slots.Count + count - 1; i >= _slots.Count; i--)
            {
                _free.Add(i);
            }

            for (int i = 0; i < count; i++)
            {
                _slots.Add(new CuePlayback(this, _slots.Count));
            }
        }
    }
}
