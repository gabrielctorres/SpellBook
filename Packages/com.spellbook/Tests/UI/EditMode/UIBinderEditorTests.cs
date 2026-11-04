using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Editor;
using SpellBook.Core.Text;
using SpellBook.UI.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>Inspector do binder: seletor de fonte, resolvedor, view, resumo e prévia redesenhada (R16; CA14).</summary>
    public class UIBinderEditorTests
    {
        private readonly List<Object> _created = new List<Object>();
        private UnityEditor.Editor _editor;

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Editor_ShowsPickerResolverViewAndSummary()
        {
            TextBinder binder = Binder(new RoleSource(), out _);
            var anchor = Track(ScriptableObject.CreateInstance<TransformAnchor>());
            anchor.name = "Jogador";
            binder.SetOwner(ActorResolver.ForAnchor(anchor));

            VisualElement root = Inspector(binder);

            Assert.That(_editor, Is.InstanceOf<UIBinderEditor>());
            Label summary = root.Q<Label>(className: UIBinderEditor.SummaryClass);
            Assert.That(summary, Is.Not.Null, "falta a linha de resumo");
            Assert.That(summary.text, Is.EqualTo("Role Source · dono: âncora Jogador"));
            List<VisualElement> order = root.Query<VisualElement>().ToList();
            VisualElement validation = root.Q(className: SpellBookEditor.ValidationClass);
            string[] fields = root.Query<PropertyField>().ToList().Select(f => f.bindingPath)
                .Where(p => p == "source" || p == "owner" || p == "view").ToArray();
            Assert.That(fields, Is.EqualTo(new[] { "owner", "source", "view" }), "resolvedor, fonte e view no Inspector");
            Assert.That(order.IndexOf(validation), Is.LessThan(order.IndexOf(summary)), "a validação fica no topo");
            Assert.That(order.IndexOf(summary), Is.LessThan(order.IndexOf(root.Q<PropertyField>("PropertyField:owner"))),
                "o resumo vem antes dos campos");
        }

        [Test]
        public void Editor_ConfigChange_RedrawsPreview()
        {
            TextBinder binder = Binder(new RoleSource { Role = TextRole.Beneficial }, out RoleView view);
            Inspector(binder);
            Assert.That(view.Last, Is.EqualTo(TextRole.Beneficial));

            _editor.serializedObject.FindProperty("source").managedReferenceValue = new OtherRoleSource();
            _editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            ((UIBinderEditor)_editor).Refresh();

            Assert.That(view.Last, Is.EqualTo(TextRole.Harmful), "a prévia não foi redesenhada com a fonte nova");
            Label summary = _editor.CreateInspectorGUI().Q<Label>(className: UIBinderEditor.SummaryClass);
            Assert.That(summary.text, Is.EqualTo("Other Role Source · dono: no objeto ou pai"));
        }

        private TextBinder Binder(UISource<TextModel> source, out RoleView view)
        {
            var gameObject = Track(new GameObject("Texto"));
            var binder = gameObject.AddComponent<TextBinder>();
            view = gameObject.AddComponent<RoleView>();
            binder.SetSource(source);
            binder.SetView(view);
            return binder;
        }

        private VisualElement Inspector(Object target)
        {
            _editor = UnityEditor.Editor.CreateEditor(target);
            return _editor.CreateInspectorGUI();
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        [Serializable]
        public sealed class RoleSource : UISource<TextModel>
        {
            public TextRole Role;

            public override Type OwnerType => typeof(Transform);

            public override UIBinding<TextModel> CreateBinding() => null;

            public override TextModel CreatePreview(in UIPreviewContext context) => new TextModel(default, Role);
        }

        [Serializable]
        public sealed class OtherRoleSource : UISource<TextModel>
        {
            public override Type OwnerType => typeof(Transform);

            public override UIBinding<TextModel> CreateBinding() => null;

            public override TextModel CreatePreview(in UIPreviewContext context) => new TextModel(default, TextRole.Harmful);
        }

        public sealed class RoleView : MonoBehaviour, IView<TextModel>
        {
            public TextRole Last;

            public void Render(in TextModel model) => Last = model.Role;
        }
    }
}
