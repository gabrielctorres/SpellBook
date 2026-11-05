using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Lista de tags compacta (DS do Inspector da habilidade, seção 6). Vazia, ocupa uma linha com o nome da lista em
    /// texto secundário, com o <c>[Tooltip]</c> do campo, e o botão "+ adicionar" à direita. Com itens, mostra o
    /// <see cref="PropertyField"/> da lista, com os itens e o botão no rodapé. Com vários assets, só fica compacta se a
    /// lista estiver vazia em todos. Serve à habilidade e ao efeito.
    /// </summary>
    internal sealed class CompactTagList : VisualElement
    {
        internal const string ClassName = "spellbook-compact-tag-list";
        internal const string EmptyRowClass = "spellbook-compact-tag-list__empty";
        internal const string CountName = "spellbook-compact-tag-list-count";
        internal const string AddText = "+ adicionar";
        private const float SecondaryOpacity = 0.7f;

        private readonly SerializedProperty _list;
        private readonly VisualElement _empty;

        /// <param name="field">Campo da lista no Inspector padrão; passa a ser filho deste elemento.</param>
        /// <param name="list">Propriedade da lista (array) do mesmo campo.</param>
        internal CompactTagList(PropertyField field, SerializedProperty list)
        {
            Field = field;
            _list = list;
            AddToClassList(ClassName);

            _empty = new VisualElement();
            _empty.AddToClassList(EmptyRowClass);
            _empty.style.flexDirection = FlexDirection.Row;
            _empty.style.alignItems = Align.Center;
            _empty.style.minHeight = EditorGUIUtility.singleLineHeight;
            var name = new Label(list.displayName) { tooltip = list.tooltip };
            name.style.flexGrow = 1;
            name.style.opacity = SecondaryOpacity;
            name.style.marginLeft = 3;
            _empty.Add(name);
            _empty.Add(new Button(AddItem) { text = AddText });

            Add(_empty);
            Add(field);
            Refresh();
        }

        /// <summary>Campo da lista no Inspector padrão.</summary>
        internal PropertyField Field { get; }

        /// <summary>Se a lista está vazia em todos os assets e ocupa só uma linha.</summary>
        internal bool IsCompact => !HasItems;

        private bool HasItems => _list.arraySize > 0 || _list.hasMultipleDifferentValues;

        /// <summary>Acrescenta um item vazio em todos os assets, com desfazer, e mostra a lista inteira.</summary>
        internal void AddItem()
        {
            _list.serializedObject.Update();
            _list.arraySize++;
            _list.serializedObject.ApplyModifiedProperties();
            Refresh();
            if (parent != null) RefreshCounter(parent);
        }

        /// <summary>Mostra a linha compacta ou a lista inteira, pelo conteúdo atual.</summary>
        internal void Refresh()
        {
            bool compact = IsCompact;
            _empty.style.display = compact ? DisplayStyle.Flex : DisplayStyle.None;
            Field.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Embrulha cada campo de <paramref name="properties"/> que está em <paramref name="group"/> numa lista compacta e
        /// põe ao lado do título do grupo a etiqueta "N de M com itens".
        /// </summary>
        internal static void WrapGroup(VisualElement group, SerializedObject serializedObject, params string[] properties)
        {
            foreach (string property in properties)
            {
                PropertyField field = group.Q<PropertyField>("PropertyField:" + property);
                SerializedProperty list = serializedObject.FindProperty(property);
                if (field == null || list == null || !list.isArray) continue;

                int index = group.IndexOf(field);
                var compact = new CompactTagList(field, list);
                group.Insert(index, compact);
            }

            AddCounter(group);
            RefreshAll(group);
        }

        /// <summary>Atualiza as listas compactas do grupo e o contador do título.</summary>
        internal static void RefreshAll(VisualElement group)
        {
            group.Query<CompactTagList>().ForEach(list => list.Refresh());
            RefreshCounter(group);
        }

        private static void RefreshCounter(VisualElement group)
        {
            Label counter = group.Q<Label>(CountName);
            if (counter == null) return;
            List<CompactTagList> lists = group.Query<CompactTagList>().ToList();
            int withItems = 0;
            foreach (CompactTagList list in lists)
            {
                if (!list.IsCompact) withItems++;
            }

            counter.text = $"{withItems} de {lists.Count} com itens";
        }

        private static void AddCounter(VisualElement group)
        {
            Label title = group.Q<Label>(className: InspectorTabsView.GroupTitleClass);
            if (title == null) return;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            group.Insert(group.IndexOf(title), row);
            row.Add(title);
            var counter = new Label { name = CountName };
            counter.style.opacity = SecondaryOpacity;
            counter.style.marginLeft = 6;
            counter.style.marginTop = title.style.marginTop;
            counter.style.marginBottom = title.style.marginBottom;
            row.Add(counter);
        }
    }
}
