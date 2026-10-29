using System;
using System.Collections.Generic;
using SpellBook.Core.Validation;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Editor
{
    /// <summary>
    /// Inspector base do SpellBook em UI Toolkit (ADR 0026, item 3a). Monta, nesta ordem: o contêiner de avisos do
    /// topo, as mensagens de <see cref="IValidatable"/> com o botão "Revalidar", <see cref="BuildBeforeDefault"/>, o
    /// Inspector padrão e <see cref="BuildAfterDefault"/>.
    /// </summary>
    public class SpellBookEditor : UnityEditor.Editor
    {
        public const string TopNoticesClass = "spellbook-top-notices";
        public const string ValidationClass = "spellbook-validation";

        private VisualElement _topNotices;
        private VisualElement _validation;
        private VisualElement _messages;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            _topNotices = new VisualElement();
            _topNotices.AddToClassList(TopNoticesClass);
            root.Add(_topNotices);

            _validation = new VisualElement();
            _validation.AddToClassList(ValidationClass);
            _messages = new VisualElement();
            _validation.Add(_messages);
            _validation.Add(new Button(Revalidate) { text = "Revalidar" });
            root.Add(_validation);

            BuildBeforeDefault(root);
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            BuildAfterDefault(root);

            RefreshTopNotices();
            RefreshValidation();
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshValidation());
            return root;
        }

        /// <summary>Ponto de extensão antes do Inspector padrão.</summary>
        protected virtual void BuildBeforeDefault(VisualElement root) { }

        /// <summary>Ponto de extensão depois do Inspector padrão, para os botões do módulo.</summary>
        protected virtual void BuildAfterDefault(VisualElement root) { }

        /// <summary>Recalcula as mensagens de validação e reconstrói os <see cref="HelpBox"/>.</summary>
        protected void RefreshValidation()
        {
            if (_messages == null)
            {
                return;
            }

            _messages.Clear();
            foreach (ValidationMessage message in CollectMessages(targets))
            {
                HelpBoxMessageType type = message.Severity == ValidationSeverity.Error
                    ? HelpBoxMessageType.Error
                    : HelpBoxMessageType.Warning;
                _messages.Add(new HelpBox(WithName(message.Context, message.Message), type));
            }

            Show(_validation, _messages.childCount > 0);
        }

        /// <summary>
        /// Valida cada alvo <see cref="IValidatable"/> num <see cref="ValidationResult"/> próprio. Uma exceção em
        /// <c>Validate</c> vira um erro com o tipo e a mensagem da exceção, e os outros alvos continuam.
        /// </summary>
        public static IReadOnlyList<ValidationMessage> CollectMessages(IEnumerable<Object> targets)
        {
            var messages = new List<ValidationMessage>();
            foreach (Object target in targets)
            {
                if (target == null || !(target is IValidatable validatable))
                {
                    continue;
                }

                var result = new ValidationResult(target);
                try
                {
                    validatable.Validate(result);
                    messages.AddRange(result.Messages);
                }
                catch (Exception exception)
                {
                    string text = $"A validação lançou {exception.GetType().Name}: {exception.Message}";
                    messages.Add(new ValidationMessage(ValidationSeverity.Error, text, target, null));
                }
            }

            return messages;
        }

        private void Revalidate()
        {
            RefreshTopNotices();
            RefreshValidation();
        }

        /// <summary>
        /// Reconstrói os avisos do topo, que vêm antes da validação, e esconde o contêiner quando não há aviso.
        /// </summary>
        private void RefreshTopNotices()
        {
            _topNotices.Clear();
            Show(_topNotices, _topNotices.childCount > 0);
        }

        private string WithName(Object context, string message)
        {
            return FormatMessage(context, message, targets.Length > 1);
        }

        /// <summary>
        /// Texto do <see cref="HelpBox"/>: em multi-seleção, a mensagem leva o nome do objeto como prefixo
        /// (ADR 0026, item 3a). Sem multi-seleção ou sem contexto, a mensagem fica como está.
        /// </summary>
        public static string FormatMessage(Object context, string message, bool multipleTargets)
        {
            return multipleTargets && context != null ? $"{context.name}: {message}" : message;
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
