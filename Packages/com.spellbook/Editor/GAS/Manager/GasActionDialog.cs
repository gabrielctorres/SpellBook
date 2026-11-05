using System;
using System.Linq;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Diálogos Duplicar e Renomear do gerenciador do GAS (DS, seção 6): campo do nome novo, a lista do que muda, a linha
    /// de motivo e o botão principal à direita, com Esc e Enter. A lógica fica no <see cref="GasDuplicate"/> e no
    /// <see cref="GasRenamePlan"/>.
    /// </summary>
    internal sealed class GasActionDialog : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.spellbook/Editor/GAS/Manager/GasManager.uss";

        private Object _asset;
        private bool _rename;
        private Action<Object> _done;
        private TextField _field;
        private ScrollView _list;
        private Label _reason;
        private Button _confirm;
        private AbilitySetPlan _copy;
        private GasRenamePlan _renamePlan;

        /// <summary>Abre o diálogo modal; <paramref name="done"/> recebe a cópia ou o asset renomeado.</summary>
        public static void Open(Object asset, bool rename, Action<Object> done)
        {
            var dialog = CreateInstance<GasActionDialog>();
            dialog.titleContent = new GUIContent($"{(rename ? "Renomear" : "Duplicar")} {asset.name}");
            dialog._done = done;
            dialog.Build(asset, rename);
            dialog.minSize = new Vector2(420, 280);
            dialog.ShowModalUtility();
        }

        internal void Build(Object asset, bool rename)
        {
            (_asset, _rename) = (asset, rename);
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath));
            root.AddToClassList("gas-manager");
            root.AddToClassList("gas-dialog");
            if (!EditorGUIUtility.isProSkin) root.AddToClassList("gas-manager--light");
            // Na descida, antes do campo, que consome o Enter e o Esc.
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape) Close();
                else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && _confirm.enabledSelf) Confirm();
            }, TrickleDown.TrickleDown);

            _field = new TextField("Nome novo");
            _field.RegisterValueChangedCallback(e => SetName(e.newValue));
            root.Add(_field);
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

            SetName(rename ? GasSetName.Of(asset) : GasDuplicate.Suggest(asset));
            _field.Focus();
        }

        /// <summary>Remonta o plano com o nome digitado; o campo chama a cada tecla.</summary>
        internal void SetName(string name)
        {
            _field.SetValueWithoutNotify(name);
            _list.Clear();
            if (_rename)
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
            else
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

        private void AddGroup(string title)
        {
            var label = new Label(title);
            label.AddToClassList("gas-manager__group");
            _list.Add(label);
        }

        private void AddRow(string text) => _list.Add(new Label(text));

        private void UpdateConfirm()
        {
            string problem = _rename ? _renamePlan.Problem : GasDuplicate.Problem(_copy);
            _confirm.text = _rename ? $"Renomear {_renamePlan.Count}" : "Duplicar";
            _confirm.SetEnabled(problem == null);
            _confirm.tooltip = problem ?? string.Empty;
            _reason.text = problem ?? string.Empty;
        }

        private void Confirm()
        {
            try
            {
                Object result = _asset;
                if (_rename) _renamePlan.Apply();
                else result = GasDuplicate.Create(_copy);
                Close();
                _done?.Invoke(result);
            }
            catch (InvalidOperationException e)
            {
                _reason.text = e.Message;
            }
        }
    }
}
