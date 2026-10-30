using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>
    /// Tipo ausente no topo do <see cref="SpellBookEditor"/> (Spec, seção 4.10, D6). Confere só a estrutura, sem bind
    /// (risco 33). O botão não é clicado, porque abre o diálogo de confirmação.
    /// </summary>
    public class SpellBookEditorMissingTypeTests
    {
        private UnityEditor.Editor _editor;

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            MissingTypeAsset.Delete();
        }

        [Test]
        public void CreateInspectorGUI_MissingType_HelpBoxWithClearButtonInTopNotices()
        {
            PickHolder holder = MissingTypeAsset.Create();

            VisualElement root = BuildInspector(holder);

            VisualElement notices = root.Q(className: SpellBookEditor.TopNoticesClass);
            List<HelpBox> boxes = notices.Query<HelpBox>().ToList();
            Assert.That(notices.style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(boxes, Has.Count.EqualTo(1));
            Assert.That(boxes[0].messageType, Is.EqualTo(HelpBoxMessageType.Error));
            Assert.That(boxes[0].text, Does.Contain(MissingTypeAsset.Assembly)
                .And.Contain(MissingTypeAsset.Namespace + "." + MissingTypeAsset.GhostClass));
            Assert.That(boxes[0].Q<Button>().text, Is.EqualTo("Limpar referência"));
            Assert.That(root.IndexOf(notices),
                Is.LessThan(root.IndexOf(root.Q(className: SpellBookEditor.ValidationClass))));
        }

        [Test]
        public void CreateInspectorGUI_NoMissingType_TopNoticesHidden()
        {
            PickHolder holder = ScriptableObject.CreateInstance<PickHolder>();
            holder.single = new PickConcrete { Value = 1 };

            try
            {
                VisualElement root = BuildInspector(holder);

                VisualElement notices = root.Q(className: SpellBookEditor.TopNoticesClass);
                Assert.That(notices.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(notices.Query<HelpBox>().ToList(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        private VisualElement BuildInspector(Object target)
        {
            _editor = UnityEditor.Editor.CreateEditor(target, typeof(MissingTypeTestEditor));
            return _editor.CreateInspectorGUI();
        }
    }

    /// <summary>Editor de teste que só herda de <see cref="SpellBookEditor"/>, sem <c>[CustomEditor]</c>.</summary>
    internal sealed class MissingTypeTestEditor : SpellBookEditor { }
}
