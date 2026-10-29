using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>
    /// Popup do seletor de tipo em UI Toolkit (ADR 0026, item 4; Spec, seção 4.10). Busca no topo, um nível de categoria
    /// por vez, resumo no rodapé; setas, Enter, Esc, e Backspace com busca vazia volta um nível.
    /// </summary>
    public sealed class TypePickerPopup : EditorWindow
    {
        private const float MinWidth = 260f;
        private const float Height = 320f;

        private TypePickerTree _tree;
        private Action<Type> _onPicked;
        private TypePickerNode _level;
        private bool _closing;
        private readonly List<TypePickerNode> _items = new List<TypePickerNode>();

        private ToolbarSearchField _search;
        private Button _back;
        private Label _title;
        private ListView _list;
        private Label _summary;

        /// <summary>
        /// Abre o popup ancorado no elemento cujo <c>worldBound</c> foi passado. Chame num callback de evento do UI Toolkit
        /// (clique do botão), para a conversão para coordenadas de tela usar a janela certa.
        /// </summary>
        public static void Show(Rect activatorWorldBound, Type baseType, Action<Type> onPicked)
        {
            if (baseType == null)
            {
                throw new ArgumentNullException(nameof(baseType));
            }

            var popup = CreateInstance<TypePickerPopup>();
            popup._tree = TypePickerTree.Build(TypePickerCatalog.GetCandidates(baseType));
            popup._onPicked = onPicked;
            popup._level = popup._tree.Root;
            Rect screenRect = GUIUtility.GUIToScreenRect(activatorWorldBound);
            popup.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(MinWidth, screenRect.width), Height));
        }

        private void CreateGUI()
        {
            if (_tree == null)
            {
                // Depois de um domain reload o callback se perdeu; não há o que escolher.
                EditorApplication.delayCall += CloseOnce;
                return;
            }

            VisualElement root = rootVisualElement;
            _search = new ToolbarSearchField();
            _search.RegisterValueChangedCallback(_ => Refresh());

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _back = new Button(GoUp) { text = "‹" };
            _title = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            header.Add(_back);
            header.Add(_title);

            _list = new ListView
            {
                itemsSource = _items,
                fixedItemHeight = 20f,
                selectionType = SelectionType.Single,
                makeItem = () => new Label(),
                bindItem = (element, index) => ((Label)element).text = ItemText(_items[index]),
                // Sem foco na lista: o teclado fica no campo de busca e é tratado só em OnKeyDown.
                focusable = false,
                style = { flexGrow = 1f },
            };
            _list.selectionChanged += _ => UpdateSummary();
            _list.itemsChosen += chosen => Activate(chosen.OfType<TypePickerNode>().FirstOrDefault());

            _summary = new Label { style = { whiteSpace = WhiteSpace.Normal, minHeight = 32f } };

            root.Add(_search);
            root.Add(header);
            root.Add(_list);
            root.Add(_summary);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Refresh();
            root.schedule.Execute(() => _search.Q<TextField>()?.Focus());
        }

        private void OnLostFocus()
        {
            CloseOnce();
        }

        private void CloseOnce()
        {
            if (!_closing)
            {
                _closing = true;
                Close();
            }
        }

        private bool Searching => !string.IsNullOrEmpty(_search.value);

        private void Refresh()
        {
            _items.Clear();
            if (Searching)
            {
                _items.AddRange(_tree.Search(_search.value).Select(TypePickerNode.ForEntry));
            }
            else
            {
                _items.AddRange(_tree.Children(_level));
            }

            bool atRoot = Searching || _level == _tree.Root;
            _back.style.display = atRoot ? DisplayStyle.None : DisplayStyle.Flex;
            _title.text = Searching ? "Busca" : atRoot ? "Tipo" : _level.CategoryPath;
            _list.RefreshItems();
            if (_items.Count > 0)
            {
                _list.SetSelection(0);
            }
            else
            {
                _list.ClearSelection();
            }

            UpdateSummary();
        }

        private string ItemText(TypePickerNode node)
        {
            if (node.Kind == TypePickerNodeKind.Category)
            {
                return node.DisplayName + "  ›";
            }

            return Searching && node.CategoryPath.Length > 0 ? $"{node.DisplayName}  ({node.CategoryPath})" : node.DisplayName;
        }

        private void UpdateSummary()
        {
            var node = _list.selectedItem as TypePickerNode;
            _summary.text = node?.Kind == TypePickerNodeKind.Type ? node.Entry.Summary : string.Empty;
        }

        private void GoUp()
        {
            TypePickerNode parent = _tree.Parent(_level);
            if (parent != null)
            {
                _level = parent;
                Refresh();
            }
        }

        private void Activate(TypePickerNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.Kind == TypePickerNodeKind.Category)
            {
                _level = node;
                Refresh();
                return;
            }

            Action<Type> onPicked = _onPicked;
            _onPicked = null;
            CloseOnce();
            onPicked?.Invoke(node.Type);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                    Move(evt.keyCode == KeyCode.UpArrow ? -1 : 1);
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Activate(_list.selectedItem as TypePickerNode);
                    break;
                case KeyCode.Escape:
                    CloseOnce();
                    break;
                case KeyCode.Backspace when !Searching:
                    GoUp();
                    break;
                default:
                    return;
            }

            evt.StopImmediatePropagation();
        }

        private void Move(int step)
        {
            if (_items.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(_list.selectedIndex + step, 0, _items.Count - 1);
            _list.SetSelection(index);
            _list.ScrollToItem(index);
        }
    }
}
