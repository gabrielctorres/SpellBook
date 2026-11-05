using System.Collections.Generic;
using SpellBook.Tags;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>Uma linha da lista do gerenciador: o cabeçalho de um grupo (sem <see cref="Entry"/>) ou um item.</summary>
    internal readonly struct GasManagerRow
    {
        public readonly GasIndexKind Kind;
        public readonly GasIndexEntry Entry;

        public GasManagerRow(GasIndexKind kind, GasIndexEntry entry)
        {
            Kind = kind;
            Entry = entry;
        }

        public bool IsHeader => Entry == null;
    }

    /// <summary>Um nó da árvore de tags (DS 5.3): a tag e as filhas que passam no filtro.</summary>
    internal sealed class GasTagNode
    {
        public GasTagNode(GasIndexEntry entry) => Entry = entry;

        public GasIndexEntry Entry { get; }
        public List<GasTagNode> Children { get; } = new List<GasTagNode>();
    }

    /// <summary>
    /// Estado do gerenciador do GAS sem UI (ME8, CA7, CA8 e CA15): o índice, os filtros, as linhas com os grupos, os
    /// rótulos da barra e dos chips, os estados vazios (DS 7), a árvore de tags e o seletor de tag em árvore.
    /// </summary>
    internal sealed class GasManagerModel
    {
        private static readonly string[] KindNames = { "Habilidades", "Efeitos", "Atributos", "Tags" };

        private readonly Dictionary<Object, int> _usedBy = new Dictionary<Object, int>();
        private GasIndex _index = GasIndex.Build(System.Array.Empty<Object>());
        private GasIndexFilter _filter;

        public GasIndexFilter Filter => _filter;

        /// <summary>Os itens que passam nos filtros; sem filtro de tipo, com um cabeçalho antes de cada grupo.</summary>
        public List<GasManagerRow> Rows { get; } = new List<GasManagerRow>();

        public bool HasFilters => _filter.Kind.HasValue || _filter.Tag != null || !string.IsNullOrEmpty(_filter.Text);

        /// <summary>A mensagem da lista vazia (DS 7), ou nulo quando há item.</summary>
        public string EmptyMessage =>
            _index.Entries.Count == 0 ? "Nenhuma habilidade ou efeito no projeto."
            : Rows.Count == 0 ? "Nada com estes filtros."
            : null;

        /// <summary>Troca o índice (um asset mudou) e descarta as contagens de uso.</summary>
        public void SetIndex(GasIndex index)
        {
            _index = index;
            _usedBy.Clear();
            Refresh();
        }

        public void SetKind(GasIndexKind? kind) => Apply(ref _filter.Kind, kind);
        public void SetTag(GameplayTagDefinition tag) => Apply(ref _filter.Tag, tag);
        public void SetText(string text) => Apply(ref _filter.Text, text);

        public void ClearFilters()
        {
            _filter = default;
            Refresh();
        }

        /// <summary>Botão do filtro de tipo com a contagem: "Habilidades 12".</summary>
        public string KindLabel(GasIndexKind kind) => $"{KindName(kind)} {_index.Count(kind)}";

        public static string KindName(GasIndexKind kind) => KindNames[(int)kind];

        /// <summary>Chip do filtro de tag, que inclui as filhas (DS 5.2).</summary>
        public static string TagChip(GameplayTagDefinition tag) => $"{tag.name} e filhas";

        /// <summary>Até duas tags do item e "+N" para o resto (DS 5.1); vazio numa tag, que já mostra o nome.</summary>
        public static string TagSummary(GasIndexEntry entry)
        {
            if (entry.Kind == GasIndexKind.Tag || entry.Tags.Count == 0) return string.Empty;
            string text = entry.Tags.Count > 1 ? $"{entry.Tags[0].name}, {entry.Tags[1].name}" : entry.Tags[0].name;
            return entry.Tags.Count > 2 ? $"{text} · +{entry.Tags.Count - 2}" : text;
        }

        /// <summary>As tags das linhas em árvore pelo pai; a tag cujo pai não passou no filtro vira raiz.</summary>
        public List<GasTagNode> TagTree()
        {
            var nodes = new Dictionary<Object, GasTagNode>();
            var roots = new List<GasTagNode>();
            foreach (GasManagerRow row in Rows)
            {
                if (!row.IsHeader && row.Kind == GasIndexKind.Tag) nodes[row.Entry.Asset] = new GasTagNode(row.Entry);
            }

            foreach (GasManagerRow row in Rows)
            {
                if (row.IsHeader || !nodes.TryGetValue(row.Entry.Asset, out GasTagNode node)) continue;
                GameplayTagDefinition parent = ((GameplayTagDefinition)row.Entry.Asset).Parent;
                if (parent != null && nodes.TryGetValue(parent, out GasTagNode parentNode)) parentNode.Children.Add(node);
                else roots.Add(node);
            }

            return roots;
        }

        /// <summary>
        /// Itens do seletor de tag, com "/" para os submenus. A tag com filhas abre um submenu com o próprio nome, e
        /// ela mesma é o primeiro item dele: <c>Ability/Ability</c>, <c>Ability/Ability.Fireball</c>.
        /// </summary>
        public List<(string Path, GameplayTagDefinition Tag)> TagMenu()
        {
            var parents = new HashSet<GameplayTagDefinition>();
            foreach (GasIndexEntry entry in _index.Entries)
            {
                if (entry.Asset is GameplayTagDefinition tag && tag.Parent != null) parents.Add(tag.Parent);
            }

            var menu = new List<(string, GameplayTagDefinition)>();
            foreach (GasIndexEntry entry in _index.Entries)
            {
                if (!(entry.Asset is GameplayTagDefinition tag)) continue;
                string path = parents.Contains(tag) ? $"{tag.name}/{tag.name}" : tag.name;
                for (GameplayTagDefinition parent = tag.Parent; parent != null; parent = parent.Parent)
                {
                    path = $"{parent.name}/{path}";
                }

                menu.Add((path, tag));
            }

            return menu;
        }

        /// <summary>"Usado por N" da linha (DS 5.1), pela busca do painel; guardado até o índice mudar.</summary>
        public int UsedByCount(Object asset)
        {
            if (!_usedBy.TryGetValue(asset, out int count))
            {
                count = GasReferenceList.UsedBy(asset).Count;
                _usedBy[asset] = count;
            }

            return count;
        }

        private void Apply<T>(ref T field, T value)
        {
            field = value;
            Refresh();
        }

        private void Refresh()
        {
            Rows.Clear();
            GasIndexKind? group = null;
            foreach (GasIndexEntry entry in _index.Query(_filter))
            {
                if (!_filter.Kind.HasValue && group != entry.Kind)
                {
                    group = entry.Kind;
                    Rows.Add(new GasManagerRow(entry.Kind, null));
                }

                Rows.Add(new GasManagerRow(entry.Kind, entry));
            }
        }
    }
}
