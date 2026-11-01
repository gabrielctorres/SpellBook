using System;
using System.Collections;
using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Tags
{
    /// <summary>
    /// Conjunto de tags sem repetição, na ordem de inserção. As consultas usam a regra de
    /// <see cref="GameplayTag.Matches"/> (uma tag casa com ela mesma e com os ancestrais),
    /// salvo <see cref="HasTagExact"/>. Tag inválida nunca casa. As consultas não alocam.
    /// </summary>
    [Serializable]
    [HelpURL(DocUrls.Tags + "#gameplaytagcontainer")]
    public sealed class GameplayTagContainer : IReadOnlyList<GameplayTag>
    {
        [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();

        public GameplayTagContainer()
        {
        }

        /// <summary>Cria o conjunto com as tags dadas, ignorando repetidas e inválidas.</summary>
        public GameplayTagContainer(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (GameplayTag tag in tags)
            {
                Add(tag);
            }
        }

        /// <summary>Cria o conjunto com as tags dadas, ignorando repetidas e inválidas.</summary>
        public GameplayTagContainer(params GameplayTag[] tags)
            : this((IEnumerable<GameplayTag>)tags)
        {
        }

        public int Count => tags.Count;
        public bool IsEmpty => tags.Count == 0;
        public GameplayTag this[int index] => tags[index];

        /// <summary>Alguma tag do conjunto é <paramref name="tag"/> ou descende dela.</summary>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].Matches(tag)) return true;
            }

            return false;
        }

        /// <summary>O conjunto contém exatamente <paramref name="tag"/>, sem olhar a hierarquia.</summary>
        public bool HasTagExact(GameplayTag tag) => tag.IsValid && tags.Contains(tag);

        /// <summary>O conjunto tem alguma das tags (hierárquico). Falso para <c>null</c> ou vazio.</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> query)
        {
            if (query == null) return false;
            for (int i = 0; i < query.Count; i++)
            {
                if (HasTag(query[i])) return true;
            }

            return false;
        }

        /// <summary>O conjunto tem todas as tags (hierárquico). Verdadeiro para <c>null</c> ou vazio.</summary>
        public bool HasAll(IReadOnlyList<GameplayTag> query)
        {
            if (query == null) return true;
            for (int i = 0; i < query.Count; i++)
            {
                if (!HasTag(query[i])) return false;
            }

            return true;
        }

        /// <summary>Acrescenta a tag no fim. Falso se ela é inválida ou já está no conjunto.</summary>
        public bool Add(GameplayTag tag)
        {
            if (!tag.IsValid || tags.Contains(tag)) return false;
            tags.Add(tag);
            return true;
        }

        /// <summary>Remove a tag exata (os filhos ficam). Falso se ela não estava no conjunto.</summary>
        public bool Remove(GameplayTag tag) => tags.Remove(tag);

        public void Clear() => tags.Clear();

        public Enumerator GetEnumerator() => new Enumerator(tags);

        IEnumerator<GameplayTag> IEnumerable<GameplayTag>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Enumerador sem alocação, na ordem de inserção.</summary>
        public struct Enumerator : IEnumerator<GameplayTag>
        {
            private readonly List<GameplayTag> _tags;
            private List<GameplayTag>.Enumerator _inner;

            internal Enumerator(List<GameplayTag> tags)
            {
                _tags = tags;
                _inner = tags.GetEnumerator();
            }

            public GameplayTag Current => _inner.Current;

            object IEnumerator.Current => Current;

            public bool MoveNext() => _inner.MoveNext();

            void IEnumerator.Reset() => _inner = _tags.GetEnumerator();

            public void Dispose() => _inner.Dispose();
        }
    }
}
