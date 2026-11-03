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
    /// "Expandir tudo" e "Recolher tudo", texto da aba vazia e o botão "+ Adicionar ação" com o contador.
    /// </summary>
    internal sealed class ActionListView : VisualElement
    {
        internal const string EmptyText = "Nenhuma ação neste evento.";

        private readonly SerializedProperty _list;
        private readonly ListView _view;
        private readonly Label _empty;
        private readonly Label _count;
        private readonly Button _add;
        private IReadOnlyList<ValidationMessage> _messages = new List<ValidationMessage>();

        internal ActionListView(SerializedProperty list)
        {
            _list = list.Copy();
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
                makeItem = () => new ActionRowView(),
                bindItem = (row, i) => ((ActionRowView)row).Bind(_list.GetArrayElementAtIndex(i), _messages),
            };
            _view.BindProperty(_list);
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
    }
}
