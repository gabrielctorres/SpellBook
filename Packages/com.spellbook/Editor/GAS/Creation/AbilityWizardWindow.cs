using System;
using System.Collections;
using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// <c>SpellBook &gt; Criar habilidade ou efeito…</c> (ME4, CA1, CA2 e CA4): escolhe o modelo, o nome e a pasta, mostra o
    /// que será criado e cria o conjunto, com "Desfazer criação" enquanto a janela está aberta. O estado fica no
    /// <see cref="AbilityWizardModel"/>; esta classe só monta a UI (DS, seções 3 e 4).
    /// </summary>
    internal sealed class AbilityWizardWindow : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.spellbook/Editor/GAS/Creation/AbilityWizard.uss";

        private readonly AbilityWizardModel _model = new AbilityWizardModel();
        private Label _title;
        private Label _step;
        private ListView _list;
        private ScrollView _detail;
        private Label _nameMessage;
        private TextField _folderField;
        private Label _reason;
        private Button _back;
        private Button _next;
        private Button _create;
        private Button _undo;

        [MenuItem("SpellBook/Criar habilidade ou efeito…")]
        public static void Open() => Open(null);

        [MenuItem("Assets/Create/SpellBook/GAS/Ability from Template…")]
        private static void OpenFromCreate() => Open(AbilityWizardModel.FolderOf(AssetDatabase.GetAssetPath(Selection.activeObject)));

        /// <summary>Abre o assistente; com uma pasta, ela já vem preenchida no passo 2.</summary>
        internal static void Open(string folder)
        {
            var window = GetWindow<AbilityWizardWindow>("Criar habilidade ou efeito");
            if (folder != null) window.UseFolder(folder);
        }

        /// <summary>O estado que a janela mostra; os testes o mudam pelo <see cref="Change"/>.</summary>
        internal AbilityWizardModel Model => _model;

        /// <summary>Troca a pasta do conjunto, como o Create com uma pasta selecionada.</summary>
        internal void UseFolder(string folder) => Change(() => _model.SetFolder(folder));

        internal void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath));
            root.AddToClassList("ability-wizard");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("ability-wizard--light");
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            var toolbar = new Toolbar();
            _title = new Label { name = "ability-wizard__title" };
            _step = new Label { name = "ability-wizard__step" };
            toolbar.Add(_title);
            toolbar.Add(_step);
            root.Add(toolbar);

            _model.Load();
            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal) { viewDataKey = "ability-wizard-split" };
            var left = new VisualElement();
            _list = new ListView((IList)_model.Templates, -1, MakeTemplateRow, BindTemplateRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            };
            _list.selectionChanged += _ => Change(() => _model.SelectTemplate(_list.selectedItem as AbilityTemplate));
            _list.itemsChosen += _ => Change(_model.Next);
            left.Add(_list);
            if (_model.BrokenNote != null) left.Add(Secondary(_model.BrokenNote));
            split.Add(left);
            _detail = new ScrollView();
            _detail.AddToClassList("ability-wizard__detail");
            split.Add(_detail);
            root.Add(split);

            _reason = Secondary(string.Empty);
            root.Add(_reason);
            var footer = new VisualElement();
            footer.AddToClassList("ability-wizard__footer");
            _back = AddButton(footer, "Voltar", () => Change(_model.Back));
            _undo = AddButton(footer, "Desfazer criação", ConfirmUndo);
            _next = AddButton(footer, "Avançar", () => Change(_model.Next));
            _create = AddButton(footer, "Criar", Create);
            root.Add(footer);

            ShowStep();
        }

        // Os passos 1, 3 e 4 mostram o modelo, o plano ou o resultado, e o painel é remontado a cada mudança. No passo 2
        // os campos têm o foco e já mostram o que foi digitado, então só o estado é atualizado.
        internal void Change(Action change)
        {
            int step = _model.Step;
            change();
            if (_detail == null) return;
            if (step == 2 && _model.Step == 2) RefreshState();
            else ShowStep();
        }

        private void ShowStep()
        {
            _detail.Clear();
            _nameMessage = null;
            _folderField = null;
            switch (_model.Step)
            {
                case 1:
                    AbilityTemplate template = _model.Template;
                    _detail.Add(new Label(template != null ? template.DisplayName : "Escolha um modelo na lista.") { name = "ability-wizard__heading" });
                    if (template != null) _detail.Add(Secondary(AbilityWizardModel.Describe(template)));
                    break;
                case 2:
                    var nameField = new TextField("Nome") { value = _model.Name };
                    nameField.RegisterValueChangedCallback(e => Change(() => _model.SetName(e.newValue)));
                    _detail.Add(nameField);
                    _nameMessage = new Label();
                    _nameMessage.AddToClassList("ability-wizard__error");
                    _detail.Add(_nameMessage);
                    var folderRow = new VisualElement();
                    folderRow.AddToClassList("ability-wizard__folder");
                    _folderField = new TextField("Pasta") { value = _model.Folder };
                    _folderField.RegisterValueChangedCallback(e => Change(() => _model.SetFolder(e.newValue)));
                    folderRow.Add(_folderField);
                    folderRow.Add(new Button(ChooseFolder) { text = "Escolher…", tooltip = "Escolhe uma pasta dentro de Assets." });
                    _detail.Add(folderRow);
                    nameField.schedule.Execute(() => nameField.Focus());
                    break;
                case 3:
                    foreach (AbilityWizardRow row in _model.Rows) _detail.Add(MakePlanRow(row));
                    break;
                default:
                    _detail.Add(new Label(_model.ResultText) { name = "ability-wizard__heading" });
                    foreach (string path in _model.Created.Paths)
                    {
                        Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                        if (asset == null) continue;
                        _detail.Add(new Button(() => ReferenceFinder.Reveal(asset)) { text = asset.name, tooltip = "Destaca no Project window." });
                    }

                    break;
            }

            RefreshState();
        }

        private void RefreshState()
        {
            if (_detail == null) return;
            int step = _model.Step;
            string reason = _model.Reason;
            _title.text = _model.StepTitle;
            _step.text = _model.StepLabel;
            _list.SetEnabled(step == 1);
            _list.RefreshItems();
            if (_folderField != null && _folderField.value != _model.Folder) _folderField.SetValueWithoutNotify(_model.Folder);
            if (_nameMessage != null) _nameMessage.text = _model.Plan?.Problem ?? string.Empty;
            _reason.text = reason ?? string.Empty;

            _back.SetEnabled(step > 1);
            Show(_next, step < 3, reason == null, "Vai para o próximo passo (Enter).", reason);
            Show(_create, step == 3, _model.CanCreate, "Cria o conjunto e seleciona o asset principal (Enter).", reason);
            Show(_undo, step == 4, true, "Apaga os assets e as tags que este conjunto criou.", null);
        }

        private static void Show(Button button, bool visible, bool enabled, string tooltip, string reason)
        {
            button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            button.SetEnabled(enabled);
            button.tooltip = enabled ? tooltip : reason;
        }

        private void Create()
        {
            try
            {
                Change(_model.Create);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _reason.text = "A criação falhou, e nada do conjunto ficou no projeto. Veja o Console.";
            }
        }

        private void ConfirmUndo()
        {
            if (EditorUtility.DisplayDialog("Desfazer criação", _model.UndoConfirm, "Apagar", "Cancelar")) Change(_model.UndoCreation);
        }

        private void ChooseFolder()
        {
            string chosen = EditorUtility.OpenFolderPanel("Pasta do conjunto", _model.Folder, string.Empty);
            if (string.IsNullOrEmpty(chosen)) return;
            string assets = Application.dataPath;
            if (chosen.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) chosen = "Assets" + chosen.Substring(assets.Length);
            _folderField.value = chosen;
        }

        private void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            // Um botão e a lista (itemsChosen) já tratam o próprio Enter; tratar de novo aqui avançaria dois passos.
            if (e.target is Button || (e.target is VisualElement target && (target == _list || _list.Contains(target)))) return;
            if (_model.Step < 3) Change(_model.Next);
            else if (_model.Step == 3) Create();
        }

        private static VisualElement MakeTemplateRow()
        {
            var row = new VisualElement();
            row.AddToClassList("ability-wizard__template");
            row.Add(new Image());
            var texts = new VisualElement();
            texts.Add(new Label());
            texts.Add(Secondary(string.Empty));
            row.Add(texts);
            row.Add(new Label());
            return row;
        }

        private void BindTemplateRow(VisualElement row, int index)
        {
            AbilityTemplate template = _model.Templates[index];
            bool selected = template == _model.Template;
            row.EnableInClassList("ability-wizard__template--selected", selected);
            ((Image)row[0]).image = AssetPreview.GetMiniThumbnail(template.Assets[0].Source);
            ((Label)row[1][0]).text = template.DisplayName;
            ((Label)row[1][1]).text = AbilityWizardModel.Describe(template);
            ((Label)row[2]).text = selected ? "✓" : string.Empty;
        }

        private static VisualElement MakePlanRow(AbilityWizardRow data)
        {
            var row = new VisualElement { tooltip = data.Tooltip };
            row.AddToClassList("ability-wizard__row");
            row.Add(new Image { image = data.Icon != null ? AssetPreview.GetMiniThumbnail(data.Icon) : null });
            row.Add(new Label(data.Name));
            row.Add(Secondary(data.Type));
            Label marker = Secondary(data.Marker);
            marker.EnableInClassList("ability-wizard__error", data.Marker.StartsWith("✖"));
            row.Add(marker);
            row.Add(new Label(data.IsMain ? "★" : string.Empty));
            return row;
        }

        private static Button AddButton(VisualElement footer, string text, Action click)
        {
            var button = new Button(click) { text = text };
            footer.Add(button);
            return button;
        }

        private static Label Secondary(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ability-wizard__secondary");
            return label;
        }
    }
}
