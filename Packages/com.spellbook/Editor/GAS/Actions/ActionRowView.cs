using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Linha de uma ação (DS do Inspector de ações, 4.2 e 4.3): seta de dobra, pastilha e barra da categoria, resumo de uma
    /// linha, chip de alvo e ícone de validação. Expandida, mostra o bloco "Alvo" e os campos da ação abaixo do cabeçalho.
    /// </summary>
    internal sealed class ActionRowView : VisualElement
    {
        internal const string ChooseText = "(Escolha uma ação)";
        internal const string ClassName = "action-row";
        internal const string EmptyClass = "action-row--empty";
        internal const string WarningClass = "action-row__validation--warning";
        internal const string ErrorClass = "action-row__validation--error";
        private const string CategoryPrefix = "action-row--cat-";

        private static readonly Dictionary<string, string> Categories = new Dictionary<string, string>
        {
            { "Fluxo", "flow" }, { "Efeito", "effect" }, { "Alvo", "target" }, { "Mundo", "world" },
            { "Apresentação", "present" }, { "Evento", "event" }, { "Depuração", "debug" },
        };

        private readonly Label _fold;
        private readonly Label _icon;
        private readonly Label _summary;
        private readonly Button _choose;
        private readonly VisualElement _body;
        private SerializedProperty _element;
        private string _category;

        internal ActionRowView()
        {
            AddToClassList(ClassName);
            var header = new VisualElement();
            header.AddToClassList(ClassName + "__header");
            _fold = new Label();
            _fold.AddToClassList(ClassName + "__fold");
            _icon = new Label();
            _icon.AddToClassList(ClassName + "__icon");
            _summary = new Label();
            _summary.AddToClassList(ClassName + "__summary");
            _choose = new Button(Choose) { text = "Escolher" };
            ValidationIcon = new Label();
            ValidationIcon.AddToClassList(ClassName + "__validation");
            header.Add(_fold);
            header.Add(_icon);
            header.Add(_summary);
            header.Add(_choose);
            header.Add(Chip);
            header.Add(ValidationIcon);
            _choose.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            header.RegisterCallback<ClickEvent>(_ => Toggle());
            _body = new VisualElement();
            _body.AddToClassList(ClassName + "__body");
            Add(header);
            Add(_body);
        }

        /// <summary>Resumo mostrado na linha.</summary>
        internal string SummaryText => _summary.text;

        /// <summary>Chip do seletor de alvo da ação.</summary>
        internal TargetChipView Chip { get; } = new TargetChipView();

        /// <summary>Ícone de aviso ou erro, visível só quando a ação tem mensagem.</summary>
        internal Label ValidationIcon { get; }

        /// <summary>Se a mensagem de <paramref name="path"/> pertence ao campo ou elemento <paramref name="owner"/>.</summary>
        internal static bool IsUnder(string path, string owner)
        {
            return path != null && path.StartsWith(owner, StringComparison.Ordinal)
                && (path.Length == owner.Length || path[owner.Length] == '.');
        }

        /// <summary>Liga a linha ao elemento da lista, com as mensagens de validação do dono.</summary>
        internal void Bind(SerializedProperty element, IReadOnlyList<ValidationMessage> messages)
        {
            _element = element.Copy();
            object value = element.managedReferenceValue;
            string summary = value == null ? ChooseText : SummaryFormatter.Format(element);
            _summary.text = summary;
            tooltip = summary;
            EnableInClassList(EmptyClass, value == null);
            Show(_choose, value == null);

            if (_category != null) RemoveFromClassList(CategoryPrefix + _category);
            string root = value?.GetType().GetCustomAttribute<TypeCategoryAttribute>(false)?.Path?.Split('/')[0];
            _category = value == null ? null : root != null && Categories.TryGetValue(root, out string key) ? key : "other";
            if (_category != null) AddToClassList(CategoryPrefix + _category);
            _icon.text = value == null ? string.Empty : (root ?? "Outro").Substring(0, 1);
            _icon.tooltip = root ?? "Outro";

            List<ValidationMessage> own = messages.Where(m => IsUnder(m.PropertyPath, element.propertyPath)).ToList();
            bool error = own.Any(m => m.Severity == ValidationSeverity.Error);
            ValidationIcon.text = error ? "✖" : "⚠";
            ValidationIcon.tooltip = string.Join("\n", own.Select(m => m.Message));
            ValidationIcon.EnableInClassList(ErrorClass, error);
            ValidationIcon.EnableInClassList(WarningClass, own.Count > 0 && !error);
            Show(ValidationIcon, own.Count > 0);
            Chip.Bind(element, messages);
            RefreshBody();
        }

        private void Toggle()
        {
            if (_element == null) return;
            _element.isExpanded = !_element.isExpanded;
            RefreshBody();
        }

        private void RefreshBody()
        {
            bool expanded = _element.isExpanded;
            _fold.text = expanded ? "▼" : "▶";
            _body.Clear();
            Show(_body, expanded);
            if (!expanded) return;
            VisualElement block = TargetChipView.BuildTargetBlock(_element);
            if (block != null) _body.Add(block);
            var field = new PropertyField(_element, "Tipo");
            field.BindProperty(_element);
            _body.Add(field);
        }

        private void Choose()
        {
            SerializedProperty element = _element;
            TypePickerPopup.Show(_choose.worldBound, typeof(GameplayAction), picked =>
            {
                TypePickerCatalog.Assign(element, picked);
                element.isExpanded = true;
            });
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
