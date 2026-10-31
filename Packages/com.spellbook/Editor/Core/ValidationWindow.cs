using System.Collections.Generic;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Editor
{
    /// <summary>
    /// Janela de <c>SpellBook &gt; Validar projeto</c> (R33, CA34): lista os erros e avisos do
    /// <see cref="ProjectValidator"/> sobre os assets do projeto e as cenas abertas, mais a regra de <c>[HelpURL]</c>
    /// sobre os tipos de runtime do pacote. Clicar num item seleciona o objeto (ou o script do tipo) e faz ping nele.
    /// </summary>
    public sealed class ValidationWindow : EditorWindow
    {
        private readonly List<ValidationMessage> _messages = new List<ValidationMessage>();
        private Label _summary;
        private ListView _list;

        [MenuItem("SpellBook/Validar projeto")]
        public static void Open()
        {
            GetWindow<ValidationWindow>("Validar projeto");
        }

        public void CreateGUI()
        {
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            toolbar.Add(new Button(Run) { text = "Validar de novo" });
            _summary = new Label();
            toolbar.Add(_summary);
            rootVisualElement.Add(toolbar);

            _list = new ListView
            {
                itemsSource = _messages,
                makeItem = MakeItem,
                bindItem = BindItem,
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                style = { flexGrow = 1 }
            };
            _list.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(_list);

            Run();
        }

        private void Run()
        {
            _messages.Clear();
            ProjectValidator.Validate(ProjectValidator.CollectProjectTargets(), _messages);
            ProjectValidator.ValidateTypes(HelpUrlRule.RuntimeAssemblies(), _messages);

            int errors = 0;
            foreach (ValidationMessage message in _messages)
            {
                if (message.Severity == ValidationSeverity.Error) errors++;
            }

            _summary.text = $"  {errors} erro(s), {_messages.Count - errors} aviso(s)";
            _list.RefreshItems();
        }

        private static VisualElement MakeItem()
        {
            return new HelpBox();
        }

        private void BindItem(VisualElement element, int index)
        {
            ValidationMessage message = _messages[index];
            var box = (HelpBox)element;
            box.messageType = message.Severity == ValidationSeverity.Error ? HelpBoxMessageType.Error : HelpBoxMessageType.Warning;
            string where = message.Context != null ? message.Context.name : "(objeto destruído)";
            box.text = $"{where}: {message.Message}";
        }

        private static void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                Object context = ((ValidationMessage)item).Context;
                if (context == null) return;

                // Um componente seleciona o GameObject dele, que é o que a Hierarchy e o Project mostram.
                Object selected = context is Component component ? component.gameObject : context;
                Selection.activeObject = selected;
                EditorGUIUtility.PingObject(selected);
                return;
            }
        }
    }
}
