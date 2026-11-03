using System;
using System.Collections.Generic;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Lista de ações de um evento (DS do Inspector de ações, 4.1): <see cref="ListView"/> reordenável e virtualizado,
    /// "Expandir tudo" e "Recolher tudo", texto da aba vazia e o botão "+ Adicionar ação" com o contador. Uma ação arrastada
    /// de outra lista da mesma aba é movida para esta (4.4); de outra aba, é recusada.
    /// </summary>
    internal sealed class ActionListView : VisualElement
    {
        internal const string EmptyText = "Nenhuma ação neste evento.";
        internal const string NestedClass = "action-list--nested";
        internal const string IndentedClass = "action-list--indented";
        private const string DragKey = "SpellBook.ActionListView.Drag";
        private const string UndoName = "Mover ação";

        private readonly SerializedProperty _list;
        private readonly ListView _view;
        private readonly Label _empty;
        private readonly Label _count;
        private readonly Button _add;
        private IReadOnlyList<ValidationMessage> _messages = new List<ValidationMessage>();

        /// <param name="depth">Nível das ações da lista: 1 na lista do evento, 2 nas filhas de uma ação dela.</param>
        internal ActionListView(SerializedProperty list, int depth = 1)
        {
            _list = list.Copy();
            Depth = depth;
            EnableInClassList(NestedClass, depth > 1);
            EnableInClassList(IndentedClass, depth > 1 && depth < ActionRowView.FlatDepth);
            var header = new VisualElement();
            header.AddToClassList("action-list__header");
            header.Add(new Button(() => SetExpanded(true)) { text = "Expandir tudo" });
            header.Add(new Button(() => SetExpanded(false)) { text = "Recolher tudo" });
            _empty = new Label(EmptyText);
            _empty.AddToClassList("action-list__empty");
            _view = new ListView
            {
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                showBoundCollectionSize = false,
                showAddRemoveFooter = false,
                makeItem = () => new ActionRowView(depth),
                bindItem = (row, i) => ((ActionRowView)row).Bind(_list.GetArrayElementAtIndex(i), _messages),
            };
            _view.BindProperty(_list);
            _view.setupDragAndDrop += SetupDrag;
            _view.dragAndDropUpdate += args => Drag(args, false);
            _view.handleDrop += args => Drag(args, true);
            var footer = new VisualElement();
            footer.AddToClassList("action-list__footer");
            _add = new Button(AddAction) { text = "+ Adicionar ação" };
            _count = new Label();
            footer.Add(_add);
            footer.Add(_count);
            Add(header);
            Add(_empty);
            Add(_view);
            Add(footer);
        }

        /// <summary>Nível das ações da lista: 1 na lista do evento, 2 nas filhas dela.</summary>
        internal int Depth { get; }

        /// <summary>Caminho da lista no asset.</summary>
        internal string ListPath => _list.propertyPath;

        /// <summary>
        /// Se a ação <paramref name="fromIndex"/> de <paramref name="fromList"/> pode ir para outra lista, <paramref name="toList"/>:
        /// só no mesmo asset, na mesma aba e fora da própria descendência. Reordenar na mesma lista é do <see cref="ListView"/>.
        /// </summary>
        internal static bool CanMove(SerializedProperty fromList, int fromIndex, SerializedProperty toList)
        {
            if (fromIndex < 0 || fromIndex >= fromList.arraySize) return false;
            if (fromList.serializedObject.targetObject != toList.serializedObject.targetObject) return false;
            if (fromList.propertyPath == toList.propertyPath) return false;
            if (RootField(fromList.propertyPath) != RootField(toList.propertyPath)) return false;
            return !ActionRowView.IsUnder(toList.propertyPath, fromList.GetArrayElementAtIndex(fromIndex).propertyPath);
        }

        /// <summary>
        /// Move a ação, com as filhas e os dados, de <paramref name="fromList"/> para a posição <paramref name="toIndex"/> de
        /// <paramref name="toList"/>, num passo de Undo. Devolve <c>false</c>, sem mudar nada, se <see cref="CanMove"/> recusa.
        /// </summary>
        internal static bool Move(SerializedProperty fromList, int fromIndex, SerializedProperty toList, int toIndex)
        {
            if (!CanMove(fromList, fromIndex, toList)) return false;

            SerializedObject serialized = fromList.serializedObject;
            serialized.Update();
            string toPath = ShiftAfterRemoval(toList.propertyPath, fromList.propertyPath, fromIndex);
            SerializedProperty from = serialized.FindProperty(fromList.propertyPath);
            object value = from.GetArrayElementAtIndex(fromIndex).managedReferenceValue;
            from.DeleteArrayElementAtIndex(fromIndex);
            SerializedProperty to = serialized.FindProperty(toPath);
            int at = Math.Max(0, Math.Min(toIndex, to.arraySize));
            to.arraySize++;
            to.GetArrayElementAtIndex(to.arraySize - 1).managedReferenceValue = value;
            to.MoveArrayElement(to.arraySize - 1, at);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            serialized.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Atualiza o contador, o texto da aba vazia e as linhas visíveis com as mensagens novas.</summary>
        internal void Refresh(IReadOnlyList<ValidationMessage> messages)
        {
            _messages = messages;
            int size = _list.arraySize;
            _count.text = $"{size} ações";
            _empty.style.display = size == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _view.RefreshItems();
        }

        private void SetExpanded(bool expanded)
        {
            for (int i = 0; i < _list.arraySize; i++) _list.GetArrayElementAtIndex(i).isExpanded = expanded;
            _view.RefreshItems();
        }

        private StartDragArgs SetupDrag(SetupDragAndDropArgs args)
        {
            StartDragArgs start = args.startDragArgs;
            SerializedProperty element = (args.draggedElement as ActionRowView ?? args.draggedElement?.Q<ActionRowView>()
                ?? args.draggedElement?.GetFirstAncestorOfType<ActionRowView>())?.Element;
            int index = element == null ? _view.selectedIndex : IndexOf(element.propertyPath);
            if (index >= 0) start.SetGenericData(DragKey, new DragSource(_list, index));
            return start;
        }

        // None deixa o ListView reordenar a própria lista; de outra lista, move se CanMove aceita
        private DragVisualMode Drag(HandleDragAndDropArgs args, bool drop)
        {
            if (!(args.dragAndDropData.GetGenericData(DragKey) is DragSource source)) return DragVisualMode.None;
            if (source.List.propertyPath == _list.propertyPath
                && source.List.serializedObject.targetObject == _list.serializedObject.targetObject)
            {
                return DragVisualMode.None;
            }

            if (!CanMove(source.List, source.Index, _list)) return DragVisualMode.Rejected;
            if (drop) Move(source.List, source.Index, _list, args.insertAtIndex < 0 ? _list.arraySize : args.insertAtIndex);
            return DragVisualMode.Move;
        }

        private static string RootField(string path)
        {
            int dot = path.IndexOf('.');
            return dot < 0 ? path : path.Substring(0, dot);
        }

        private static int IndexOf(string elementPath)
        {
            int open = elementPath.LastIndexOf('[');
            return open < 0 ? -1 : int.Parse(elementPath.Substring(open + 1, elementPath.Length - open - 2));
        }

        // Tirar o elemento sobe as irmãs seguintes, e o destino pode estar dentro de uma delas
        private static string ShiftAfterRemoval(string toPath, string fromPath, int fromIndex)
        {
            string prefix = fromPath + ".Array.data[";
            if (!toPath.StartsWith(prefix, StringComparison.Ordinal)) return toPath;
            int close = toPath.IndexOf(']', prefix.Length);
            int index = int.Parse(toPath.Substring(prefix.Length, close - prefix.Length));
            return index > fromIndex ? prefix + (index - 1) + toPath.Substring(close) : toPath;
        }

        private void AddAction()
        {
            SerializedProperty list = _list;
            TypePickerPopup.Show(_add.worldBound, typeof(GameplayAction), picked =>
            {
                list.serializedObject.Update();
                list.arraySize++;
                SerializedProperty element = list.GetArrayElementAtIndex(list.arraySize - 1);
                TypePickerCatalog.Assign(element, picked);
                element.isExpanded = true;
            });
        }

        /// <summary>Origem de um arrasto: a lista e o índice da ação arrastada.</summary>
        private sealed class DragSource
        {
            internal readonly SerializedProperty List;
            internal readonly int Index;

            internal DragSource(SerializedProperty list, int index)
            {
                List = list.Copy();
                Index = index;
            }
        }
    }
}
