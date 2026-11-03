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
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Linha de uma ação (DS do Inspector de ações, 4.2 e 4.3): seta de dobra, pastilha e barra da categoria, resumo de uma
    /// linha, chip de alvo e ícone de validação. Expandida, mostra o bloco "Alvo" e os campos da ação abaixo do cabeçalho.
    /// As listas filhas de uma ação de fluxo aparecem como <see cref="ActionListView"/> aninhadas, com a mesma linha (4.4).
    /// </summary>
    internal sealed class ActionRowView : VisualElement
    {
        internal const string ChooseText = "(Escolha uma ação)";
        internal const string ClassName = "action-row";
        internal const string EmptyClass = "action-row--empty";
        internal const string WarningClass = "action-row__validation--warning";
        internal const string ErrorClass = "action-row__validation--error";
        /// <summary>Nível a partir do qual o recuo não aumenta e a linha mostra o indicador do nível (DS 4.4).</summary>
        internal const int FlatDepth = 4;
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
        private readonly VisualElement _children;
        private readonly int _depth;
        private SerializedProperty _element;
        private IReadOnlyList<ValidationMessage> _messages;
        private string _summaryText;
        private string _category;

        /// <param name="depth">Nível da ação: 1 na lista do evento, 2 nas filhas dela.</param>
        internal ActionRowView(int depth = 1)
        {
            _depth = depth;
            AddToClassList(ClassName);
            var header = new VisualElement();
            header.AddToClassList(ClassName + "__header");
            _fold = new Label();
            _fold.AddToClassList(ClassName + "__fold");
            _icon = new Label();
            _icon.AddToClassList(ClassName + "__icon");
            _summary = new Label();
            _summary.AddToClassList(ClassName + "__summary");
            _choose = new Button { text = "Escolher" };
            _choose.clicked += () => Choose(_choose);
            ValidationIcon = new Label();
            ValidationIcon.AddToClassList(ClassName + "__validation");
            header.Add(_fold);
            header.Add(_icon);
            header.Add(_summary);
            LevelLabel.text = $"⋯ nível {depth}";
            LevelLabel.AddToClassList(ClassName + "__level");
            Show(LevelLabel, depth >= FlatDepth);
            header.Add(LevelLabel);
            header.Add(_choose);
            header.Add(Chip);
            header.Add(ValidationIcon);
            _choose.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            header.RegisterCallback<ClickEvent>(_ => Toggle());
            _body = new VisualElement();
            _body.AddToClassList(ClassName + "__body");
            _children = new VisualElement();
            _children.AddToClassList(ClassName + "__children");
            Add(header);
            Add(_body);
            Add(_children);
        }

        /// <summary>Indicador do nível, visível do nível 4 em diante.</summary>
        internal Label LevelLabel { get; } = new Label();

        /// <summary>Elemento da lista ligado à linha.</summary>
        internal SerializedProperty Element => _element;

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
            _messages = messages;
            object value = element.managedReferenceValue;
            _summaryText = value == null ? ChooseText : SummaryFormatter.Format(element);
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
            List<SerializedProperty> lists = ChildLists(_element);
            int children = lists.Sum(l => l.arraySize);
            // Recolhida, a ação de fluxo diz quantas filhas tem; expandida, as filhas estão à vista
            string summary = expanded || lists.Count == 0 ? _summaryText
                : $"{_summaryText} · {children} {(children == 1 ? "ação" : "ações")}";
            _summary.text = summary;
            tooltip = summary;
            _fold.text = expanded ? "▼" : "▶";
            _body.Clear();
            _children.Clear();
            Show(_body, expanded);
            Show(_children, expanded && lists.Count > 0);
            if (!expanded) return;
            VisualElement block = TargetChipView.BuildTargetBlock(_element);
            if (block != null) _body.Add(block);
            if (lists.Count == 0)
            {
                var field = new PropertyField(_element, "Tipo");
                field.BindProperty(_element);
                _body.Add(field);
                return;
            }

            // Com filhas, a linha monta os campos, para cada lista filha virar a mesma lista de ações, aninhada
            var change = new Button { text = "Trocar tipo" };
            change.clicked += () => Choose(change);
            _body.Add(change);
            SerializedProperty child = _element.Copy();
            SerializedProperty end = _element.GetEndProperty();
            for (bool more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end);
                 more = child.NextVisible(false))
            {
                string path = child.propertyPath;
                if (lists.Any(l => l.propertyPath == path)) continue;
                var field = new PropertyField(child.Copy());
                field.BindProperty(child.Copy());
                _body.Add(field);
            }

            foreach (SerializedProperty list in lists)
            {
                var label = new Label(list.displayName);
                label.AddToClassList(ClassName + "__child-label");
                var nested = new ActionListView(list, _depth + 1);
                nested.Refresh(_messages);
                _children.Add(label);
                _children.Add(nested);
            }
        }

        // Campos List<GameplayAction> serializados e visíveis do tipo concreto, na ordem da declaração
        private static List<SerializedProperty> ChildLists(SerializedProperty action)
        {
            var lists = new List<SerializedProperty>();
            for (Type type = action.managedReferenceValue?.GetType(); type != null && type != typeof(GameplayAction);
                 type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                             | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken))
                {
                    if (field.FieldType != typeof(List<GameplayAction>) || field.IsDefined(typeof(HideInInspector))) continue;
                    SerializedProperty list = action.FindPropertyRelative(field.Name);
                    if (list != null) lists.Add(list);
                }
            }

            return lists;
        }

        private void Choose(VisualElement anchor)
        {
            SerializedProperty element = _element;
            TypePickerPopup.Show(anchor.worldBound, typeof(GameplayAction), picked =>
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
