using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// <c>SpellBook &gt; Gerenciador do GAS</c> (ME8, CA7 a CA10): lista as habilidades, os efeitos, os atributos e as
    /// tags com filtros, e mostra o que o item selecionado usa e quem o usa. Não edita campos. O estado fica no
    /// <see cref="GasManagerModel"/>; esta classe só monta a UI (DS, seção 5).
    /// </summary>
    internal sealed class GasManagerWindow : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.spellbook/Editor/GAS/Manager/GasManager.uss";
        private const string NoSelection = "Selecione um item para ver o que ele usa e quem o usa.";

        private readonly GasManagerModel _model = new GasManagerModel();
        private readonly List<ToolbarToggle> _kindToggles = new List<ToolbarToggle>();
        private GasProjectIndex _projectIndex;
        private Toolbar _toolbar;
        private ToolbarSearchField _search;
        private ToolbarMenu _tagMenu;
        private VisualElement _chips;
        private Label _empty;
        private ListView _list;
        private TreeView _tree;
        private ScrollView _detail;
        private Label _reason;
        private Button _open;
        private Object _selected;

        [MenuItem("SpellBook/Gerenciador do GAS")]
        public static void Open() => GetWindow<GasManagerWindow>("Gerenciador do GAS");

        private void OnEnable()
        {
            _projectIndex = new GasProjectIndex();
            _projectIndex.Changed += Reload;
        }

        private void OnDisable() => _projectIndex.Dispose();

        internal void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath));
            root.AddToClassList("gas-manager");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("gas-manager--light");

            _toolbar = new Toolbar();
            _search = new ToolbarSearchField { tooltip = "Filtra pelo nome, sem diferenciar maiúscula." };
            _search.RegisterValueChangedCallback(e => Change(() => _model.SetText(e.newValue)));
            _toolbar.Add(_search);
            AddKindToggle(null);
            for (int kind = 0; kind < 4; kind++) AddKindToggle((GasIndexKind)kind);
            _tagMenu = new ToolbarMenu { text = "Tag", tooltip = "Filtra pela tag e pelas filhas dela." };
            _toolbar.Add(_tagMenu);
            _toolbar.SetEnabled(false);
            root.Add(_toolbar);

            _chips = new VisualElement();
            _chips.AddToClassList("gas-manager__chips");
            root.Add(_chips);

            var split = new TwoPaneSplitView(0, 320, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "gas-manager-split" };
            var left = new VisualElement();
            _empty = new Label("Lendo o projeto…");
            _empty.AddToClassList("gas-manager__secondary");
            left.Add(_empty);
            _list = new ListView { makeItem = MakeRow, bindItem = BindListRow, selectionType = SelectionType.Single };
            _list.selectionChanged += _ => Select(_list.selectedIndex >= 0 ? _model.Rows[_list.selectedIndex].Entry?.Asset : null);
            _list.itemsChosen += _ => OpenInInspector();
            left.Add(_list);
            _tree = new TreeView { makeItem = MakeRow, bindItem = BindTreeRow, selectionType = SelectionType.Single };
            _tree.selectionChanged += _ => Select(_tree.selectedIndex >= 0 ? _tree.GetItemDataForIndex<GasIndexEntry>(_tree.selectedIndex).Asset : null);
            _tree.itemsChosen += _ => OpenInInspector();
            left.Add(_tree);
            split.Add(left);
            _detail = new ScrollView();
            _detail.AddToClassList("gas-manager__detail");
            split.Add(_detail);
            root.Add(split);

            _reason = new Label();
            _reason.AddToClassList("gas-manager__secondary");
            root.Add(_reason);
            // Duplicar e Renomear (ME9) e Excluir (ME10) entram nesta fileira, à esquerda do botão principal.
            var footer = new VisualElement();
            footer.AddToClassList("gas-manager__footer");
            _open = new Button(OpenInInspector) { text = "Abrir no Inspector" };
            footer.Add(_open);
            root.Add(footer);

            Select(null);
            root.schedule.Execute(() =>
            {
                Reload();
                _search.Focus();
            });
        }

        private void Reload()
        {
            if (_toolbar == null) return;
            _model.SetIndex(_projectIndex.Index);
            _toolbar.SetEnabled(true);
            RefreshView();
        }

        private void Change(System.Action change)
        {
            change();
            RefreshView();
        }

        private void AddKindToggle(GasIndexKind? kind)
        {
            var toggle = new ToolbarToggle { userData = kind };
            toggle.RegisterValueChangedCallback(_ => Change(() => _model.SetKind(kind)));
            _kindToggles.Add(toggle);
            _toolbar.Add(toggle);
        }

        private void RefreshView()
        {
            GasIndexFilter filter = _model.Filter;
            foreach (ToolbarToggle toggle in _kindToggles)
            {
                var kind = (GasIndexKind?)toggle.userData;
                toggle.text = kind.HasValue ? _model.KindLabel(kind.Value) : "Todos";
                toggle.SetValueWithoutNotify(kind == filter.Kind);
            }

            _tagMenu.menu.ClearItems();
            foreach ((string path, var tag) in _model.TagMenu())
            {
                _tagMenu.menu.AppendAction(path, _ => Change(() => _model.SetTag(tag)),
                    _ => tag == filter.Tag ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }

            _chips.Clear();
            if (filter.Kind.HasValue) AddChip(GasManagerModel.KindName(filter.Kind.Value), () => _model.SetKind(null));
            if (filter.Tag != null) AddChip(GasManagerModel.TagChip(filter.Tag), () => _model.SetTag(null));
            if (!string.IsNullOrEmpty(filter.Text)) AddChip($"“{filter.Text}”", () => _search.value = string.Empty);
            if (_model.HasFilters) _chips.Add(new Button(() => { _search.SetValueWithoutNotify(string.Empty); Change(_model.ClearFilters); }) { text = "Limpar filtros" });

            bool tree = filter.Kind == GasIndexKind.Tag;
            _empty.text = _model.EmptyMessage ?? string.Empty;
            _empty.style.display = _model.EmptyMessage != null ? DisplayStyle.Flex : DisplayStyle.None;
            _list.style.display = tree ? DisplayStyle.None : DisplayStyle.Flex;
            _tree.style.display = tree ? DisplayStyle.Flex : DisplayStyle.None;
            if (tree)
            {
                _tree.SetRootItems(ToTreeItems(_model.TagTree()));
                _tree.Rebuild();
                _tree.ExpandAll();
            }
            else
            {
                _list.itemsSource = _model.Rows;
                _list.Rebuild();
            }

            Select(_selected);
        }

        private void AddChip(string text, System.Action clear)
        {
            var chip = new Button(() => Change(clear)) { text = text + "  ✕", tooltip = "Tira este filtro." };
            chip.AddToClassList("gas-manager__chip");
            _chips.Add(chip);
        }

        private static List<TreeViewItemData<GasIndexEntry>> ToTreeItems(List<GasTagNode> nodes)
        {
            var items = new List<TreeViewItemData<GasIndexEntry>>();
            foreach (GasTagNode node in nodes)
            {
                items.Add(new TreeViewItemData<GasIndexEntry>(node.Entry.Asset.GetInstanceID(), node.Entry, ToTreeItems(node.Children)));
            }

            return items;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("gas-manager__row");
            row.Add(new Image());
            for (int i = 0; i < 4; i++) row.Add(new Label());
            row[3].AddToClassList("gas-manager__secondary");
            row[4].AddToClassList("gas-manager__secondary");
            return row;
        }

        private void BindListRow(VisualElement row, int index) => BindRow(row, _model.Rows[index]);

        private void BindTreeRow(VisualElement row, int index) =>
            BindRow(row, new GasManagerRow(GasIndexKind.Tag, _tree.GetItemDataForIndex<GasIndexEntry>(index)));

        private void BindRow(VisualElement row, GasManagerRow data)
        {
            row.EnableInClassList("gas-manager__row--header", data.IsHeader);
            ((Image)row[0]).image = data.IsHeader ? null : AssetPreview.GetMiniThumbnail(data.Entry.Asset);
            ((Label)row[1]).text = data.IsHeader ? GasManagerModel.KindName(data.Kind) : data.Entry.Name;
            ((Label)row[2]).text = data.IsHeader || _model.Filter.Kind.HasValue ? string.Empty : TypeName(data.Kind);
            ((Label)row[3]).text = data.IsHeader ? string.Empty : GasManagerModel.TagSummary(data.Entry);
            ((Label)row[4]).text = data.IsHeader ? string.Empty : $"usado por {_model.UsedByCount(data.Entry.Asset)}";
        }

        private static string TypeName(GasIndexKind kind) =>
            kind == GasIndexKind.Ability ? "Habilidade" : kind == GasIndexKind.Effect ? "Efeito" : kind == GasIndexKind.Attribute ? "Atributo" : "Tag";

        private void Select(Object asset)
        {
            _selected = asset;
            _open.SetEnabled(asset != null);
            _reason.text = asset != null ? string.Empty : "Selecione um item na lista.";
            _open.tooltip = asset != null ? "Seleciona o asset no Inspector (Enter)." : _reason.text;
            _detail.Clear();
            if (asset == null)
            {
                _detail.Add(new Label(NoSelection) { name = "gas-manager__no-selection" });
                return;
            }

            _detail.Add(new Label(asset.name) { name = "gas-manager__title" });
            AddGroup("Usa", GasReferenceList.Uses(asset), null);
            AddGroup("Usado por", GasReferenceList.UsedBy(asset), GasReferenceList.ClosedScenesNote);
        }

        private void AddGroup(string title, List<GasReferenceRow> rows, string note)
        {
            var header = new Label($"{title} ({rows.Count})");
            header.AddToClassList("gas-manager__group");
            _detail.Add(header);
            foreach (GasReferenceRow row in rows)
            {
                Object target = row.Target;
                var button = new Button(() => ReferenceFinder.Reveal(target)) { tooltip = "Destaca no Project window ou na Hierarchy." };
                button.AddToClassList("gas-manager__reference");
                button.Add(new Image { image = AssetPreview.GetMiniThumbnail(target) });
                button.Add(new Label(row.Name));
                var location = new Label(row.Location);
                location.AddToClassList("gas-manager__secondary");
                button.Add(location);
                _detail.Add(button);
            }

            if (note == null) return;
            var noteLabel = new Label(note);
            noteLabel.AddToClassList("gas-manager__secondary");
            _detail.Add(noteLabel);
        }

        private void OpenInInspector()
        {
            if (_selected != null) ReferenceFinder.Reveal(_selected);
        }
    }
}
