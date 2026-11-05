using System;
using System.Collections.Generic;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Tipo de um item do gerenciador do GAS, na ordem dos grupos da lista (DS, seção 5.1).</summary>
    internal enum GasIndexKind
    {
        Ability,
        Effect,
        Attribute,
        Tag
    }

    /// <summary>Um asset do GAS no índice: o asset, o nome, o tipo e as tags usadas pelo filtro de tag.</summary>
    internal sealed class GasIndexEntry
    {
        public GasIndexEntry(Object asset, GasIndexKind kind, IReadOnlyList<GameplayTagDefinition> tags)
        {
            Asset = asset;
            Name = asset.name;
            Kind = kind;
            Tags = tags;
        }

        public Object Asset { get; }

        /// <summary>Nome do asset quando o índice foi montado.</summary>
        public string Name { get; }

        public GasIndexKind Kind { get; }

        /// <summary>
        /// Tags próprias do item: a <c>AbilityTag</c> da habilidade, as <c>GrantedTags</c> e as <c>DamageTags</c> do
        /// efeito, a <c>StateTagWhileDepleted</c> do atributo e, numa tag, a própria tag. A tag que o item só exige ou
        /// que uma ação dele usa fica fora; quem a usa aparece no painel de referências (CA9).
        /// </summary>
        public IReadOnlyList<GameplayTagDefinition> Tags { get; }

        /// <summary>Alguma tag do item é <paramref name="tag"/> ou descende dela.</summary>
        public bool HasTag(GameplayTagDefinition tag)
        {
            for (int i = 0; i < Tags.Count; i++)
            {
                if (Tags[i].Matches(tag)) return true;
            }

            return false;
        }
    }

    /// <summary>Filtros do gerenciador. Os definidos se combinam; os vazios não filtram.</summary>
    internal struct GasIndexFilter
    {
        /// <summary>Só os itens deste tipo; nulo para todos.</summary>
        public GasIndexKind? Kind;

        /// <summary>Só os itens com esta tag ou com uma filha dela; nulo para todos.</summary>
        public GameplayTagDefinition Tag;

        /// <summary>Trecho do nome, sem diferenciar maiúscula; vazio para todos.</summary>
        public string Text;
    }

    /// <summary>
    /// Índice do gerenciador do GAS (ME7, CA7 e CA8): as habilidades, os efeitos, os atributos e as tags, ordenados
    /// por tipo e por nome, com a contagem por tipo e os filtros. Não lê o projeto; quem lê é o
    /// <see cref="GasProjectIndex"/>.
    /// </summary>
    internal sealed class GasIndex
    {
        private readonly List<GasIndexEntry> _entries;
        private readonly int[] _counts = new int[Enum.GetValues(typeof(GasIndexKind)).Length];

        private GasIndex(List<GasIndexEntry> entries)
        {
            _entries = entries;
            foreach (GasIndexEntry entry in entries)
            {
                _counts[(int)entry.Kind]++;
            }
        }

        public IReadOnlyList<GasIndexEntry> Entries => _entries;

        /// <summary>Monta o índice com os assets do GAS da lista; os outros e os nulos ficam fora.</summary>
        public static GasIndex Build(IEnumerable<Object> assets)
        {
            var entries = new List<GasIndexEntry>();
            foreach (Object asset in assets)
            {
                if (asset == null || !TryGetKind(asset, out GasIndexKind kind)) continue;
                entries.Add(new GasIndexEntry(asset, kind, OwnTags(asset)));
            }

            entries.Sort(Compare);
            return new GasIndex(entries);
        }

        public int Count(GasIndexKind kind) => _counts[(int)kind];

        /// <summary>Os itens que passam em todos os filtros definidos, na ordem do índice.</summary>
        public List<GasIndexEntry> Query(GasIndexFilter filter)
        {
            var result = new List<GasIndexEntry>();
            foreach (GasIndexEntry entry in _entries)
            {
                if (filter.Kind.HasValue && entry.Kind != filter.Kind.Value) continue;
                if (filter.Tag != null && !entry.HasTag(filter.Tag)) continue;
                if (!string.IsNullOrEmpty(filter.Text)
                    && entry.Name.IndexOf(filter.Text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(entry);
            }

            return result;
        }

        private static bool TryGetKind(Object asset, out GasIndexKind kind)
        {
            switch (asset)
            {
                case GameplayAbility _:
                    kind = GasIndexKind.Ability;
                    return true;
                case GameplayEffect _:
                    kind = GasIndexKind.Effect;
                    return true;
                case AttributeDefinition _:
                    kind = GasIndexKind.Attribute;
                    return true;
                case GameplayTagDefinition _:
                    kind = GasIndexKind.Tag;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static List<GameplayTagDefinition> OwnTags(Object asset)
        {
            var tags = new List<GameplayTagDefinition>();
            switch (asset)
            {
                case GameplayAbility ability:
                    AddTag(tags, ability.AbilityTag);
                    break;
                case GameplayEffect effect:
                    AddTags(tags, effect.GrantedTags);
                    AddTags(tags, effect.DamageTags);
                    break;
                case AttributeDefinition attribute:
                    AddTag(tags, attribute.StateTagWhileDepleted);
                    break;
                case GameplayTagDefinition tag:
                    tags.Add(tag);
                    break;
            }

            return tags;
        }

        private static void AddTags(List<GameplayTagDefinition> tags, IReadOnlyList<GameplayTag> source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                AddTag(tags, source[i]);
            }
        }

        private static void AddTag(List<GameplayTagDefinition> tags, GameplayTag tag)
        {
            GameplayTagDefinition definition = tag.GetDefinition();
            if (definition != null && !tags.Contains(definition)) tags.Add(definition);
        }

        private static int Compare(GasIndexEntry a, GasIndexEntry b)
        {
            int byKind = a.Kind.CompareTo(b.Kind);
            if (byKind != 0) return byKind;
            int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
        }
    }
}
