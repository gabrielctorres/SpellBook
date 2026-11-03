using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>Toca cues por tag e devolve ao spawner tudo o que eles criam. Componente de cena.</summary>
    [HelpURL(DocUrls.Cues + "#cuerouter")]
    [DisallowMultipleComponent]
    public sealed class CueRouter : MonoBehaviour
    {
        [SerializeField, Tooltip("Bibliotecas em que o roteador procura o cue de cada tag. Na tag repetida, vale a primeira.")]
        private List<CueLibrary> libraries = new List<CueLibrary>();

        [SerializeField, Tooltip("Spawner dos itens sem spawner próprio. Vazio usa o spawner padrão por Instantiate.")]
        private SpawnerAsset defaultSpawner;

        [SerializeField, Min(1), Tooltip("Cues ativos ao mesmo tempo antes de crescer. Crescer aloca.")]
        private int capacity = 64;

        private readonly CueIndex _index = new CueIndex();
        private readonly List<CuePlayback> _slots = new List<CuePlayback>();
        private readonly List<int> _free = new List<int>();
        private readonly HashSet<Object> _warned = new HashSet<Object>();
        private long _sequence;
        private bool _initialized;

        /// <summary>Quantos cues estão ativos.</summary>
        public int ActiveCount { get; private set; }

        internal SpawnerAsset DefaultSpawner => SpawnerAsset.OrDefault(defaultSpawner);

        // Mensagens da Unity internas para os testes EditMode, em que a Unity não as chama
        internal void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Toca o cue <see cref="CueKind.Burst"/> da <paramref name="tag"/>. Falso se nada tocou: tag sem definição ou cue
        /// <see cref="CueKind.Looping"/> (use <see cref="Add"/>).
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

        /// <summary>Desliga o cue e devolve os objetos dele. Falso se o handle não vale mais.</summary>
        public bool Remove(CueHandle handle)
        {
            if (!IsActive(handle)) return false;
            Release(_slots[handle.Slot]);
            return true;
        }

        /// <summary>Desliga os cues da <paramref name="tag"/> e das tags filhas dela.</summary>
        public void RemoveAll(GameplayTag tag)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                CuePlayback slot = _slots[i];
                if (slot.IsActive && slot.Cue.Tag.Matches(tag)) Release(slot);
            }
        }

        /// <summary>Desliga todos os cues.</summary>
        public void RemoveAll()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsActive) Release(_slots[i]);
            }
        }

        /// <summary>Verdadeiro se o cue do handle ainda está ativo.</summary>
        public bool IsActive(CueHandle handle)
        {
            if (!handle.IsValid || handle.Slot < 0 || handle.Slot >= _slots.Count) return false;
            CuePlayback slot = _slots[handle.Slot];
            return slot.IsActive && slot.Generation == handle.Generation;
        }

        /// <summary>Verdadeiro na primeira vez para <paramref name="key"/>, só em desenvolvimento.</summary>
        internal bool ShouldWarn(Object key)
        {
            return Debug.isDebugBuild && _warned.Add(key);
        }

        private bool TryFind(GameplayTag tag, out CueDefinition cue)
        {
            EnsureInitialized();
            return _index.TryFind(tag, out cue);
        }

        private bool Play(CueDefinition cue, in CueParameters parameters, out CueHandle handle)
        {
            CuePlayback slot = Acquire(cue);
            IReadOnlyList<CueItem> items = cue.Items;
            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.Play(parameters, slot);
            }

            handle = slot.Handle;
            if (!slot.HasObjects) Release(slot);
            return true;
        }

        private CuePlayback Acquire(CueDefinition cue)
        {
            if (_free.Count == 0) AddSlots(_slots.Count);

            int index = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            CuePlayback slot = _slots[index];
            slot.Begin(cue, ++_sequence);
            ActiveCount++;
            return slot;
        }

        private void Release(CuePlayback slot)
        {
            slot.End();
            _free.Add(slot.Slot);
            ActiveCount--;
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;

            _initialized = true;
            _index.Rebuild(libraries);
            AddSlots(Mathf.Max(1, capacity));
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
