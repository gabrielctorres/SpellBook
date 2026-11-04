using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Binder de lista (ADR 0039, itens 2 e 6): liga a <see cref="UIListSource{TItem}"/> ao dono e mostra cada item num
    /// objeto criado só pelo <see cref="SpawnerAsset"/> (ADR 0015), na ordem de chegada, no fim do container. Remover
    /// devolve o objeto ao spawner; mudar o conteúdo reaproveita o objeto. Escreve o dono no
    /// <see cref="UIItemContext"/> do item. Sem prefab, com prefab sem <see cref="IView{TItem}"/> ou com o spawner sem
    /// instância, o item é pulado com um aviso em desenvolvimento, sem exceção. Em edição, não cria itens.
    /// </summary>
    /// <typeparam name="TItem">Modelo de cada item.</typeparam>
    public abstract class UIListBinder<TItem> : UIBinderBase, IUIListReceiver<TItem> where TItem : struct
    {
        private const int InitialCapacity = 16;

        [SerializeReference, TypePicker]
        [Tooltip("O que o binder lê do dono.")]
        private UIListSource<TItem> source;

        [SerializeField]
        [Tooltip("Prefab de cada item, com um componente que implementa IView do modelo do item.")]
        private GameObject itemPrefab;

        [SerializeField]
        [Tooltip("Spawner que cria e recebe de volta os itens. Vazio: o spawner padrão.")]
        private SpawnerAsset spawner;

        [SerializeField]
        [Tooltip("Pai dos itens criados. Vazio: o objeto deste binder.")]
        private Transform container;

        private readonly List<Entry> _entries = new List<Entry>(InitialCapacity);

        // Views e contextos já lidos, por instância: o pool devolve as mesmas instâncias
        private readonly List<GameObject> _known = new List<GameObject>(InitialCapacity);
        private readonly List<KnownItem> _knownItems = new List<KnownItem>(InitialCapacity);
        private UIListBinding<TItem> _binding;
        private UIListSource<TItem> _bindingSource;
        private bool _warnedPrefab;
        private bool _warnedSpawn;
        private bool _warnedView;

        /// <summary>A fonte da lista.</summary>
        public UIListSource<TItem> Source => source;

        /// <summary>Prefab de cada item.</summary>
        public GameObject ItemPrefab => itemPrefab;

        /// <summary>O spawner do campo; vazio, o binder usa o <see cref="SpawnerAsset.Default"/>.</summary>
        public SpawnerAsset Spawner => spawner;

        /// <summary>O pai do campo; vazio, os itens nascem sob este binder.</summary>
        public Transform Container => container;

        /// <summary>Quantos itens estão na lista, com ou sem objeto.</summary>
        public int Count => _entries.Count;

        /// <summary>Troca a fonte e religa (em edição, redesenha a prévia). A ligação da fonte antiga é descartada.</summary>
        public void SetSource(UIListSource<TItem> newSource)
        {
            if (ReferenceEquals(source, newSource))
            {
                return;
            }

            source = newSource;
            Rebind();
        }

        // Só a ligação mexe na lista: uma chamada direta fora do Play salvaria itens na cena
        void IUIListReceiver<TItem>.AddItem(int id, in TItem item) => AddItem(id, item);

        void IUIListReceiver<TItem>.UpdateItem(int id, in TItem item) => UpdateItem(id, item);

        void IUIListReceiver<TItem>.RemoveItem(int id) => RemoveItem(id);

        void IUIListReceiver<TItem>.ClearItems() => ClearItems();

        /// <summary>Acrescenta o item no fim; um id já presente só muda o conteúdo.</summary>
        private void AddItem(int id, in TItem item)
        {
            if (IndexOf(id) >= 0)
            {
                UpdateItem(id, item);
                return;
            }

            _entries.Add(new Entry(id, item));
            Show(_entries.Count - 1);
        }

        /// <summary>Muda o conteúdo do item, reaproveitando o objeto; um id ausente não faz nada.</summary>
        private void UpdateItem(int id, in TItem item)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                return;
            }

            Entry entry = _entries[index];
            entry.Model = item;
            _entries[index] = entry;
            Show(index);
        }

        /// <summary>Tira o item e devolve o objeto dele. Um id ausente não faz nada.</summary>
        private void RemoveItem(int id)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                return;
            }

            Release(index);
            _entries.RemoveAt(index);
        }

        /// <summary>Tira todos os itens e devolve os objetos.</summary>
        private void ClearItems()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Release(i);
            }

            _entries.Clear();
        }

        private protected override Type SourceOwnerType => source?.OwnerType;

        private protected override UIBinding AcquireBinding()
        {
            if (_binding == null || !ReferenceEquals(_bindingSource, source))
            {
                _binding?.Detach();
                _bindingSource = source;
                _binding = source.CreateBinding();
                _binding?.SetReceiver(this);
            }

            return _binding;
        }

        // Ao desligar a fonte, os itens voltam ao spawner: o próximo dono publica os dele
        private protected override void DetachBinding()
        {
            _binding?.Detach();
            ClearItems();
        }

        private protected override void ShowEmpty() => ClearItems();

        // Em edição não há itens: um spawn fora do Play salvaria instâncias na cena
        private protected override void DrawPreview()
        {
        }

        private int IndexOf(int id)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        // Cria o objeto que falta, no fim do container, e desenha o modelo
        private void Show(int index)
        {
            Entry entry = _entries[index];
            if (entry.Instance == null)
            {
                if (!TrySpawn(out entry.Instance, out entry.View))
                {
                    return;
                }

                _entries[index] = entry;
            }

            entry.View.Render(entry.Model);
        }

        private bool TrySpawn(out GameObject instance, out IView<TItem> view)
        {
            view = null;
            instance = null;
            if (itemPrefab == null)
            {
                WarnOnce(ref _warnedPrefab, "sem prefab de item");
                return false;
            }

            Transform parent = container != null ? container : transform;
            SpawnerAsset used = SpawnerAsset.OrDefault(spawner);
            instance = used.Spawn(itemPrefab, parent.position, Quaternion.identity, parent);
            if (instance == null)
            {
                WarnOnce(ref _warnedSpawn, "o spawner não criou a instância");
                return false;
            }

            KnownItem known = Find(instance);
            if (known.View == null)
            {
                used.Release(instance);
                instance = null;
                WarnOnce(ref _warnedView, $"o prefab não tem um componente IView<{typeof(TItem).Name}>");
                return false;
            }

            instance.transform.SetParent(parent, true);
            instance.transform.SetAsLastSibling();
            if (known.Context != null)
            {
                known.Context.SetOwner(_binding?.Owner);
            }

            view = known.View;
            return true;
        }

        private void Release(int index)
        {
            GameObject instance = _entries[index].Instance;
            if (instance == null)
            {
                return;
            }

            UIItemContext context = Find(instance).Context;
            if (context != null)
            {
                context.SetOwner(null);
            }

            SpawnerAsset.OrDefault(spawner).Release(instance);
            Entry entry = _entries[index];
            entry.Instance = null;
            entry.View = null;
            _entries[index] = entry;
        }

        private KnownItem Find(GameObject instance)
        {
            // Instância destruída (spawner sem pool) libera a entrada, para o cache não crescer sem limite
            int free = -1;
            for (int i = 0; i < _known.Count; i++)
            {
                GameObject known = _known[i];
                if (ReferenceEquals(known, instance))
                {
                    return _knownItems[i];
                }

                if (free < 0 && known == null)
                {
                    free = i;
                }
            }

            var item = new KnownItem(instance.GetComponent<IView<TItem>>(), instance.GetComponent<UIItemContext>());
            if (free >= 0)
            {
                _known[free] = instance;
                _knownItems[free] = item;
            }
            else
            {
                _known.Add(instance);
                _knownItems.Add(item);
            }

            return item;
        }

        private void WarnOnce(ref bool warned, string reason)
        {
            if (warned || !Debug.isDebugBuild)
            {
                return;
            }

            warned = true;
            Debug.LogWarning($"[UI] '{name}': item de lista pulado, {reason}.", this);
        }

        private struct Entry
        {
            public readonly int Id;
            public TItem Model;
            public GameObject Instance;
            public IView<TItem> View;

            public Entry(int id, in TItem model)
            {
                Id = id;
                Model = model;
                Instance = null;
                View = null;
            }
        }

        private readonly struct KnownItem
        {
            public KnownItem(IView<TItem> view, UIItemContext context)
            {
                View = view;
                Context = context;
            }

            public IView<TItem> View { get; }

            public UIItemContext Context { get; }
        }
    }
}
