using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Targeting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Chip do seletor de alvo de uma ação (DS do Inspector de ações, 5.1 e 5.2): mostra o seletor atual, abre o popup de
    /// <see cref="TargetSelector"/> no clique e fica vermelho quando o validador acusa o seletor, sem bloquear a troca.
    /// </summary>
    internal sealed class TargetChipView : Label
    {
        internal const string DefaultLabel = "(Padrão do evento)";
        internal const string ClassName = "target-chip";
        internal const string DefaultClass = "target-chip--default";
        internal const string ErrorClass = "target-chip--error";
        internal const string BlockClass = "action-row__target-block";
        private const string TargetField = "target";
        private const string ArrayElement = ".Array.data[";

        private SerializedProperty _target;

        internal TargetChipView()
        {
            AddToClassList(ClassName);
            focusable = true;
            RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                Pick();
            });
            // Enter abre o popup, que fecha com Esc (DS 7); a tecla não chega à lista, que expandiria a linha
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                evt.StopImmediatePropagation();
                Pick();
            });
        }

        /// <summary>Texto do chip, com o ✖ quando o seletor tem erro.</summary>
        internal string Text => text;

        /// <summary>Liga o chip ao <c>target</c> da ação; ação nula ou que não usa alvos esconde o chip.</summary>
        internal void Bind(SerializedProperty action, IReadOnlyList<ValidationMessage> messages)
        {
            var value = action.managedReferenceValue as GameplayAction;
            style.display = value != null && value.UsesTargets ? DisplayStyle.Flex : DisplayStyle.None;
            if (value == null) return;

            _target = action.FindPropertyRelative(TargetField);
            bool isDefault = _target.managedReferenceValue == null;
            string described = "→ " + (isDefault ? "Padrão do evento" : Describe(_target));
            List<ValidationMessage> own = messages.Where(m => ActionRowView.IsUnder(m.PropertyPath, _target.propertyPath)).ToList();
            bool error = own.Any(m => m.Severity == ValidationSeverity.Error);
            text = error ? "✖ " + described : described;
            EnableInClassList(DefaultClass, isDefault);
            EnableInClassList(ErrorClass, error);
            tooltip = own.Count > 0 ? string.Join("\n", own.Select(m => m.Message))
                : isDefault ? DefaultTooltip(action) : described.Substring(2);
        }

        /// <summary>Bloco "Alvo" com os campos do seletor, ou <c>null</c> se o seletor não tem campos.</summary>
        internal static VisualElement BuildTargetBlock(SerializedProperty action)
        {
            SerializedProperty target = action.FindPropertyRelative(TargetField);
            if (target == null || target.managedReferenceValue == null || !HasFields(target)) return null;

            var block = new VisualElement();
            block.AddToClassList(BlockClass);
            block.Add(new Label("Alvo"));
            SerializedProperty child = target.Copy();
            SerializedProperty end = target.GetEndProperty();
            // Next(true) entra nos filhos mesmo com o target em [HideInInspector]; NextVisible(true) não entraria
            for (bool more = child.Next(true); more && !SerializedProperty.EqualContents(child, end);
                 more = child.NextVisible(false))
            {
                var field = new PropertyField(child.Copy());
                field.BindProperty(child.Copy());
                block.Add(field);
            }

            return block;
        }

        private static bool HasFields(SerializedProperty target)
        {
            SerializedProperty child = target.Copy();
            return child.Next(true) && !SerializedProperty.EqualContents(child, target.GetEndProperty());
        }

        private static string Describe(SerializedProperty target)
        {
            if (!(target.managedReferenceValue is AreaSelector area)) return SummaryFormatter.Format(target);

            string text = "Área " + RadiusText(target.FindPropertyRelative("radius"));
            if (area.MaxTargets > 0) text += $" · {area.MaxTargets} alvos";
            SerializedProperty filter = target.FindPropertyRelative("filter");
            if (filter != null && filter.managedReferenceValue != null) text += " · " + SummaryFormatter.Format(filter);
            return text;
        }

        // Raio pelo modo do ScalableFloat: constante em metros, tabela pelo nome, array ou curva "por nível"
        private static string RadiusText(SerializedProperty radius)
        {
            SerializedProperty mode = radius?.FindPropertyRelative("mode");
            if (mode == null) return "?";
            string name = mode.enumNames[mode.enumValueIndex];
            if (name == "Constant") return radius.FindPropertyRelative("constant").floatValue.ToString("0.##") + " m";
            return name == "Table" ? "%" + radius.FindPropertyRelative("tableName").stringValue : SummaryFormatter.PerLevel;
        }

        private static string DefaultTooltip(SerializedProperty action)
        {
            string path = action.propertyPath;
            SerializedObject serialized = action.serializedObject;
            // Um ForEach acima da ação: o padrão é o alvo corrente da repetição
            for (int i = path.IndexOf(ArrayElement, StringComparison.Ordinal); i >= 0;
                 i = path.IndexOf(ArrayElement, i + 1, StringComparison.Ordinal))
            {
                int close = path.IndexOf(']', i);
                if (close < 0 || close + 1 >= path.Length) break;
                if (serialized.FindProperty(path.Substring(0, close + 1))?.managedReferenceValue is ForEachTargetAction)
                {
                    return "Dentro do ForEach, o padrão é o alvo corrente.";
                }
            }

            int dot = path.IndexOf('.');
            string field = dot < 0 ? path : path.Substring(0, dot);
            ActionEventAttribute ev = serialized.targetObject.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetCustomAttribute<ActionEventAttribute>();
            if (ev == null) return "O alvo padrão do evento.";
            return ev.DefaultTarget == DefaultTarget.Owner
                ? "Neste evento, o padrão é o dono."
                : "Neste evento, o padrão são os alvos do evento.";
        }

        private void Pick()
        {
            SerializedProperty target = _target;
            if (target == null) return;
            SerializedProperty action = target.serializedObject.FindProperty(
                target.propertyPath.Substring(0, target.propertyPath.Length - TargetField.Length - 1));
            TypePickerPopup.Show(worldBound, typeof(TargetSelector), picked =>
            {
                TypePickerCatalog.Assign(target, picked);
                if (action != null && HasFields(target)) action.isExpanded = true;
            }, DefaultLabel);
        }
    }
}
