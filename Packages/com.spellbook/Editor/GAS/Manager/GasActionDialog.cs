using System;
using System.Linq;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>A ação do diálogo do gerenciador.</summary>
    internal enum GasAction
    {
        Duplicate,
        Rename,
        Delete,
        NewTag,
    }

    /// <summary>
    /// Diálogos Duplicar, Renomear, Excluir e Nova tag filha do gerenciador do GAS (DS, seção 6): o campo do nome (menos
    /// no Excluir), a lista do que muda ou de quem usa o item, a linha de motivo e o botão principal à direita, com Esc e
    /// Enter. A lógica fica no <see cref="GasDuplicate"/>, no <see cref="GasRenamePlan"/>, no
    /// <see cref="GasDeletePlan"/> e no <see cref="GasChildTag"/>.
    /// </summary>
    internal sealed class GasActionDialog : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.spellbook/Editor/GAS/Manager/GasManager.uss";

        private Object _asset;
        private GasAction _action;
        private Action<Object> _done;
        private TextField _field;
        private ScrollView _list;
        private Label _reason;
        private Button _confirm;
        private AbilitySetPlan _copy;
        private GasRenamePlan _renamePlan;
        private GasDeletePlan _deletePlan;

        /// <summary>
        /// Abre o diálogo modal; <paramref name="done"/> recebe a cópia, o asset renomeado, a tag nova ou, no Excluir,
        /// <c>null</c>.
        /// </summary>
        public static void Open(Object asset, GasAction action, Action<Object> done)
        {
            var dialog = CreateInstance<GasActionDialog>();
            dialog.titleContent = new GUIContent(Title(asset, action));
            dialog._done = done;
            dialog.Build(asset, action);
            dialog.minSize = new Vector2(420, 280);
            dialog.ShowModalUtility();
        }

        /// <summary>Se o Enter confirma: com o botão ligado e, no Excluir, só quando nada usa o item (DS 6).</summary>
        internal bool EnterConfirms => _confirm.enabledSelf && !(_action == GasAction.Delete && _deletePlan.Users.Count > 0);

        internal void Build(Object asset, GasAction action)
        {
            (_asset, _action) = (asset, action);
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath));
            root.AddToClassList("gas-manager");
            root.AddToClassList("gas-dialog");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("gas-manager--light");
            // Na descida, antes do campo, que consome o Enter e o Esc; a tecla tratada não segue adiante.
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) Close();
                else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && EnterConfirms) Confirm();
                else return;
                e.StopPropagation();
            }, TrickleDown.TrickleDown);

            if (action == GasAction.Delete)
            {
                root.Add(new Label(Title(asset, action)) { name = "gas-dialog__title" });
            }
            else
            {
                _field = new TextField(action == GasAction.NewTag ? "Nome" : "Nome novo");
                _field.RegisterValueChangedCallback(e => SetName(e.newValue));
                root.Add(_field);
                // O foco só cai no campo depois que a janela é mostrada (observação da revisão do ME9b).
                root.schedule.Execute(() => _field.Focus());
            }

            _list = new ScrollView();
            _list.AddToClassList("gas-dialog__list");
            root.Add(_list);
            _reason = new Label { name = "gas-dialog__reason" };
            _reason.AddToClassList("gas-manager__secondary");
            root.Add(_reason);
            var footer = new VisualElement();
            footer.AddToClassList("gas-manager__footer");
            footer.Add(new Button(Close) { text = "Cancelar" });
            _confirm = new Button(Confirm) { name = "gas-dialog__confirm" };
            footer.Add(_confirm);
            root.Add(footer);

            if (action == GasAction.Delete) ShowUsers();
            else if (action == GasAction.NewTag) SetName(GasChildTag.Prefix((GameplayTagDefinition)asset));
            else SetName(action == GasAction.Rename ? GasSetName.Of(asset) : GasDuplicate.Suggest(asset));
        }

        /// <summary>Remonta o plano com o nome digitado; o campo chama a cada tecla.</summary>
        internal void SetName(string name)
        {
            _field.SetValueWithoutNotify(name);
            _list.Clear();
            if (_action == GasAction.Rename)
            {
                GasRenamePlan previous = _renamePlan;
                _renamePlan = GasRenamePlan.Build(_asset, name);
                for (int i = 0; i < _renamePlan.Items.Count; i++)
                {
                    GasRenameItem item = _renamePlan.Items[i];
                    // O plano novo vem todo marcado: o que o designer desmarcou continua desmarcado ao editar o nome.
                    if (previous != null && previous.Items.Any(p => p.Asset == item.Asset && !p.Checked)) _renamePlan.SetChecked(i, false);
                    int index = i;
                    var toggle = new Toggle($"{item.Asset.name} → {item.NewName}") { value = item.Checked };
                    toggle.RegisterValueChangedCallback(e => SetChecked(index, e.newValue));
                    _list.Add(toggle);
                }
            }
            else if (_action == GasAction.Duplicate)
            {
                _copy = GasDuplicate.Plan(_asset, name);
                AddGroup("Copiado");
                foreach (PlannedAsset planned in _copy.Assets) AddRow(planned.Name);
                foreach (PlannedTag tag in _copy.Tags)
                {
                    if (tag.Source != null || tag.Status != PlannedStatus.Exists) AddRow(tag.Name);
                }

                if (_asset is GameplayAbility)
                {
                    AddGroup("Compartilhado");
                    foreach (GameplayEffect effect in GasDuplicate.Shared(_asset)) AddRow(effect.name);
                }
            }

            UpdateConfirm();
        }

        /// <summary>Marca ou desmarca um derivado do Renomear; a caixa chama ao mudar.</summary>
        internal void SetChecked(int index, bool value)
        {
            _renamePlan.SetChecked(index, value);
            _list.Query<Toggle>().AtIndex(index).SetValueWithoutNotify(value);
            UpdateConfirm();
        }

        private static string Title(Object asset, GasAction action) => action switch
        {
            GasAction.Rename => $"Renomear {asset.name}",
            GasAction.Delete => $"Excluir {asset.name}?",
            GasAction.NewTag => $"Nova tag filha de {asset.name}",
            _ => $"Duplicar {asset.name}",
        };

        // Excluir: quem usa o item (numa tag, também as filhas), o aviso das referências que ficam vazias e o botão
        // na cor de erro.
        private void ShowUsers()
        {
            _deletePlan = GasDeletePlan.Build(_asset);
            int count = _deletePlan.Users.Count;
            AddGroup($"Usado por ({count})");
            foreach (GasReferenceRow row in _deletePlan.Users) AddRow($"{row.Name} ({row.Location})");
            var note = new Label(GasReferenceList.ClosedScenesNote);
            note.AddToClassList("gas-manager__secondary");
            _list.Add(note);
            var message = new Label(count == 0 ? "Nada no projeto usa este item." : "⚠ Estas referências vão ficar vazias, e o validador vai acusá-las.");
            if (count > 0) message.AddToClassList("gas-manager__warning");
            rootVisualElement.Insert(rootVisualElement.IndexOf(_reason), message);
            _confirm.AddToClassList("gas-dialog__danger");
            UpdateConfirm();
        }

        private void AddGroup(string title)
        {
            var label = new Label(title);
            label.AddToClassList("gas-manager__group");
            _list.Add(label);
        }

        private void AddRow(string text) => _list.Add(new Label(text));

        private void UpdateConfirm()
        {
            string problem = _action switch
            {
                GasAction.Rename => _renamePlan.Problem,
                GasAction.Delete => _deletePlan.Problem,
                GasAction.NewTag => GasChildTag.Problem((GameplayTagDefinition)_asset, _field.value),
                _ => GasDuplicate.Problem(_copy),
            };
            _confirm.text = _action switch
            {
                GasAction.Rename => $"Renomear {_renamePlan.Count}",
                GasAction.Delete => "Excluir",
                GasAction.NewTag => "Criar tag",
                _ => "Duplicar",
            };
            _confirm.SetEnabled(problem == null);
            _confirm.tooltip = problem ?? string.Empty;
            _reason.text = problem ?? string.Empty;
        }

        private void Confirm()
        {
            try
            {
                Object result = _asset;
                if (_action == GasAction.Rename) _renamePlan.Apply();
                else if (_action == GasAction.Delete) result = Delete();
                else if (_action == GasAction.NewTag) result = GasChildTag.Create((GameplayTagDefinition)_asset, _field.value);
                else result = GasDuplicate.Create(_copy);
                Close();
                _done?.Invoke(result);
            }
            catch (InvalidOperationException e)
            {
                _reason.text = e.Message;
            }
        }

        private Object Delete()
        {
            _deletePlan.Apply();
            return null;
        }
    }
}
