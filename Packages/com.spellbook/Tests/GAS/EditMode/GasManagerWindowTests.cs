using NUnit.Framework;
using SpellBook.GAS.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Fumaça da janela do gerenciador do GAS (ME8b, CA10 e CA15): ela abre em UI Toolkit com as quatro faixas do DS
    /// (seção 3) e, sem item selecionado, o botão principal fica desligado com o motivo no tooltip. O batchmode da
    /// revisão roda sem placa de vídeo, então a janela não é mostrada: o teste monta a UI pelo <c>CreateGUI</c>.
    /// </summary>
    public class GasManagerWindowTests
    {
        private GasManagerWindow _window;

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
        }

        [Test]
        public void Open_BuildsToolbarListPanelAndFooter()
        {
            _window = ScriptableObject.CreateInstance<GasManagerWindow>();
            _window.CreateGUI();

            VisualElement root = _window.rootVisualElement;
            Assert.That(root.Q<Toolbar>(), Is.Not.Null, "sem a barra de filtros");
            Assert.That(root.Q<ToolbarSearchField>(), Is.Not.Null, "sem a busca");
            Assert.That(root.Q<ListView>(), Is.Not.Null, "sem a lista");
            Assert.That(root.Q<Label>("gas-manager__no-selection")?.text,
                Is.EqualTo("Selecione um item para ver o que ele usa e quem o usa."));
            Button open = root.Query<Button>().Where(b => b.text == "Abrir no Inspector").First();
            Assert.That(open, Is.Not.Null, "sem o botão Abrir no Inspector");
            Assert.That(open.enabledSelf, Is.False);
            Assert.That(open.tooltip, Is.EqualTo("Selecione um item na lista."));
        }

        [Test]
        public void Footer_DuplicateAndRename_DisabledWithReason()
        {
            _window = ScriptableObject.CreateInstance<GasManagerWindow>();
            _window.CreateGUI();

            foreach (string text in new[] { "Duplicar", "Renomear" })
            {
                Button button = _window.rootVisualElement.Query<Button>().Where(b => b.text == text).First();
                Assert.That(button, Is.Not.Null, $"sem o botão {text}");
                Assert.That(button.enabledSelf, Is.False);
                Assert.That(button.tooltip, Is.EqualTo("Selecione um item na lista."));
            }
        }
    }
}
