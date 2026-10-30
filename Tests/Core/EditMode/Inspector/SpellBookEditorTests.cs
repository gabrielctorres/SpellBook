using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>
    /// Inspector base <see cref="SpellBookEditor"/> (ADR 0026, item 3a; Spec, seção 4.10). Conferem a estrutura e as
    /// funções puras, sem bind (risco 33).
    /// </summary>
    public class SpellBookEditorTests
    {
        private const string TopNoticesClass = "spellbook-top-notices";
        private const string ValidationClass = "spellbook-validation";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void CreateInspectorGUI_TopNoticesContainerFirstAndHidden_WhenNoNotices()
        {
            VisualElement root = BuildInspector(Make<ValidatableHolder>(), null);

            VisualElement notices = root.Q(className: TopNoticesClass);
            Assert.That(notices, Is.Not.Null);
            Assert.That(root.IndexOf(notices), Is.EqualTo(0));
            Assert.That(notices.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(notices.childCount, Is.EqualTo(0));
        }

        [Test]
        public void CollectMessages_InvalidTarget_ReturnsError()
        {
            ValidatableHolder holder = Make<ValidatableHolder>();
            holder.valid = false;

            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { holder });

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(messages[0].Message, Is.EqualTo(ValidatableHolder.InvalidMessage));
            Assert.That(messages[0].Context, Is.SameAs(holder));
        }

        [Test]
        public void CollectMessages_ValidTarget_ReturnsNone()
        {
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { Make<ValidatableHolder>() }), Is.Empty);
        }

        [Test]
        public void CollectMessages_NonValidatable_ReturnsNone()
        {
            Assert.That(SpellBookEditor.CollectMessages(new Object[] { Make<PlainHolder>(), null }), Is.Empty);
        }

        [Test]
        public void CollectMessages_ExceptionInValidate_BecomesError()
        {
            ValidatableHolder holder = Make<ValidatableHolder>();
            holder.throws = true;

            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(new Object[] { holder });

            Assert.That(messages, Has.Count.EqualTo(1));
            Assert.That(messages[0].Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(messages[0].Message, Does.Contain(nameof(InvalidOperationException)).And.Contain("falhou"));
            Assert.That(messages[0].Context, Is.SameAs(holder));
        }

        [Test]
        public void FormatMessage_MultipleTargets_PrefixesObjectName()
        {
            ValidatableHolder holder = Make<ValidatableHolder>();
            holder.name = "Bola de Fogo";

            string text = SpellBookEditor.FormatMessage(holder, ValidatableHolder.InvalidMessage, true);

            Assert.That(text, Is.EqualTo("Bola de Fogo: " + ValidatableHolder.InvalidMessage));
        }

        [Test]
        public void FormatMessage_SingleTarget_KeepsMessage()
        {
            ValidatableHolder holder = Make<ValidatableHolder>();
            holder.name = "Bola de Fogo";

            string text = SpellBookEditor.FormatMessage(holder, ValidatableHolder.InvalidMessage, false);

            Assert.That(text, Is.EqualTo(ValidatableHolder.InvalidMessage));
        }

        [Test]
        public void FormatMessage_MultipleTargetsWithoutContext_KeepsMessage()
        {
            string text = SpellBookEditor.FormatMessage(null, ValidatableHolder.InvalidMessage, true);

            Assert.That(text, Is.EqualTo(ValidatableHolder.InvalidMessage));
        }

        [Test]
        public void CreateInspectorGUI_ValidationContainerFirst_WithOneHelpBoxPerMessage()
        {
            ValidatableHolder holder = Make<ValidatableHolder>();
            holder.valid = false;
            holder.warn = true;

            VisualElement root = BuildInspector(holder, null);

            VisualElement validation = root.Q(className: ValidationClass);
            List<HelpBox> boxes = validation.Query<HelpBox>().ToList();
            Assert.That(root.IndexOf(validation), Is.EqualTo(1), "Só o contêiner de avisos do topo vem antes.");
            Assert.That(validation.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(boxes.Select(b => b.messageType),
                Is.EqualTo(new[] { HelpBoxMessageType.Error, HelpBoxMessageType.Warning }));
            Assert.That(boxes[0].text, Is.EqualTo(ValidatableHolder.InvalidMessage));
            Assert.That(validation.Query<Button>().ToList().Select(b => b.text), Does.Contain("Revalidar"));
        }

        [Test]
        public void CreateInspectorGUI_NonValidatable_ContainerHidden()
        {
            VisualElement root = BuildInspector(Make<PlainHolder>(), typeof(SpellBookEditor));

            VisualElement validation = root.Q(className: ValidationClass);
            Assert.That(validation.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(validation.Query<HelpBox>().ToList(), Is.Empty);
        }

        [Test]
        public void CreateInspectorGUI_AfterDefaultExtensionIsLast()
        {
            VisualElement root = BuildInspector(Make<ExtendedHolder>(), null);

            Assert.That(root[root.childCount - 1].name, Is.EqualTo(ExtendedHolderEditor.AfterName));
            Assert.That(root.IndexOf(root.Q(ExtendedHolderEditor.BeforeName)),
                Is.EqualTo(2), "BuildBeforeDefault vem logo depois dos dois contêineres.");
        }

        private T Make<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        private VisualElement BuildInspector(Object target, Type editorType)
        {
            UnityEditor.Editor editor = editorType == null
                ? UnityEditor.Editor.CreateEditor(target)
                : UnityEditor.Editor.CreateEditor(target, editorType);
            _created.Insert(0, editor);
            Assert.That(editor, Is.InstanceOf<SpellBookEditor>());
            return editor.CreateInspectorGUI();
        }
    }

    internal class ValidatableHolder : ScriptableObject, IValidatable
    {
        internal const string InvalidMessage = "Valor inválido.";

        public bool valid = true;
        public bool warn;
        public bool throws;

        public void Validate(ValidationResult result)
        {
            if (throws)
            {
                throw new InvalidOperationException("falhou");
            }

            if (!valid)
            {
                result.AddError(InvalidMessage);
            }

            if (warn)
            {
                result.AddWarning("Aviso de teste.");
            }
        }
    }

    [CustomEditor(typeof(ValidatableHolder))]
    internal sealed class ValidatableHolderEditor : SpellBookEditor { }

    internal class PlainHolder : ScriptableObject
    {
        public int number;
    }

    internal class ExtendedHolder : ScriptableObject
    {
        public int number;
    }

    [CustomEditor(typeof(ExtendedHolder))]
    internal sealed class ExtendedHolderEditor : SpellBookEditor
    {
        internal const string BeforeName = "before-default";
        internal const string AfterName = "after-default";

        protected override void BuildBeforeDefault(VisualElement root)
        {
            root.Add(new Label("antes") { name = BeforeName });
        }

        protected override void BuildAfterDefault(VisualElement root)
        {
            root.Add(new Button { name = AfterName, text = "Ação" });
        }
    }
}
