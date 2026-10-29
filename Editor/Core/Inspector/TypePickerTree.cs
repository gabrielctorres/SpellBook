using System;
using System.Collections.Generic;
using System.Linq;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>Tipo de um nó da árvore do seletor.</summary>
    public enum TypePickerNodeKind
    {
        /// <summary>"(Nenhum)": deixa o campo nulo. Só existe na raiz.</summary>
        None,
        Category,
        Type,
    }

    /// <summary>Nó da árvore do seletor: a raiz, uma categoria, "(Nenhum)" ou um tipo.</summary>
    public sealed class TypePickerNode
    {
        internal TypePickerNode(TypePickerNodeKind kind, string displayName, string categoryPath, TypePickerEntry entry)
        {
            Kind = kind;
            DisplayName = displayName;
            CategoryPath = categoryPath;
            Entry = entry;
        }

        public TypePickerNodeKind Kind { get; }

        /// <summary>Nome do tipo, o último trecho do caminho da categoria ou "(Nenhum)".</summary>
        public string DisplayName { get; }

        /// <summary>Caminho completo da categoria (do próprio nó, ou onde o tipo está); vazio na raiz.</summary>
        public string CategoryPath { get; }

        /// <summary>Entrada do catálogo; só tem valor em nó de tipo.</summary>
        public TypePickerEntry Entry { get; }

        /// <summary>Tipo escolhido por este nó; <c>null</c> para "(Nenhum)" e para categorias.</summary>
        public Type Type => Kind == TypePickerNodeKind.Type ? Entry.Type : null;

        internal static TypePickerNode ForEntry(TypePickerEntry entry)
        {
            return new TypePickerNode(TypePickerNodeKind.Type, entry.DisplayName, entry.CategoryPath, entry);
        }
    }

    /// <summary>
    /// Modelo puro do popup do seletor (Spec, seção 4.10): níveis por <see cref="TypePickerEntry.CategoryPath"/>,
    /// em ordem alfabética, categorias antes dos tipos e "(Nenhum)" primeiro na raiz.
    /// </summary>
    public sealed class TypePickerTree
    {
        public const string NoneLabel = "(Nenhum)";

        private static readonly IReadOnlyList<TypePickerNode> NoChildren = Array.Empty<TypePickerNode>();

        private readonly Dictionary<TypePickerNode, List<TypePickerNode>> _children =
            new Dictionary<TypePickerNode, List<TypePickerNode>>();

        private readonly Dictionary<TypePickerNode, TypePickerNode> _parents =
            new Dictionary<TypePickerNode, TypePickerNode>();

        private readonly List<TypePickerEntry> _entries;

        private TypePickerTree(List<TypePickerEntry> entries)
        {
            _entries = entries;
            Root = new TypePickerNode(TypePickerNodeKind.Category, string.Empty, string.Empty, default);
        }

        public TypePickerNode Root { get; }

        public static TypePickerTree Build(IEnumerable<TypePickerEntry> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var tree = new TypePickerTree(entries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList());
            tree.Add(tree.Root, new TypePickerNode(TypePickerNodeKind.None, NoneLabel, string.Empty, default));
            var categories = new Dictionary<string, TypePickerNode>(StringComparer.Ordinal);
            foreach (TypePickerEntry entry in tree._entries)
            {
                TypePickerNode level = tree.GetCategory(categories, entry.CategoryPath);
                tree.Add(level, TypePickerNode.ForEntry(entry));
            }

            foreach (List<TypePickerNode> children in tree._children.Values)
            {
                children.Sort(CompareSiblings);
            }

            return tree;
        }

        /// <summary>Filhos de um nível, já ordenados; lista vazia para nós de tipo e "(Nenhum)".</summary>
        public IReadOnlyList<TypePickerNode> Children(TypePickerNode level)
        {
            return level != null && _children.TryGetValue(level, out List<TypePickerNode> children) ? children : NoChildren;
        }

        /// <summary>Nível acima do nó; <c>null</c> para a raiz.</summary>
        public TypePickerNode Parent(TypePickerNode node)
        {
            return node != null && _parents.TryGetValue(node, out TypePickerNode parent) ? parent : null;
        }

        /// <summary>Tipos de todos os níveis que casam com a busca, numa lista plana; vazia quando a busca é vazia.</summary>
        public IReadOnlyList<TypePickerEntry> Search(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return Array.Empty<TypePickerEntry>();
            }

            return TypePickerCatalog.Filter(_entries, query).ToList();
        }

        private TypePickerNode GetCategory(Dictionary<string, TypePickerNode> categories, string path)
        {
            TypePickerNode level = Root;
            string current = string.Empty;
            foreach (string segment in (path ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current.Length == 0 ? segment : current + "/" + segment;
                if (!categories.TryGetValue(current, out TypePickerNode category))
                {
                    category = new TypePickerNode(TypePickerNodeKind.Category, segment, current, default);
                    categories[current] = category;
                    Add(level, category);
                }

                level = category;
            }

            return level;
        }

        private void Add(TypePickerNode parent, TypePickerNode child)
        {
            if (!_children.TryGetValue(parent, out List<TypePickerNode> children))
            {
                children = new List<TypePickerNode>();
                _children[parent] = children;
            }

            children.Add(child);
            _parents[child] = parent;
        }

        private static int CompareSiblings(TypePickerNode a, TypePickerNode b)
        {
            int byKind = Rank(a.Kind).CompareTo(Rank(b.Kind));
            return byKind != 0 ? byKind : StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName);
        }

        private static int Rank(TypePickerNodeKind kind)
        {
            return kind == TypePickerNodeKind.None ? 0 : kind == TypePickerNodeKind.Category ? 1 : 2;
        }
    }
}
