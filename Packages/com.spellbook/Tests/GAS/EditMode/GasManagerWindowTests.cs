using NUnit.Framework;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Fumaça da janela do gerenciador do GAS (ME8b, ME9b e ME10; CA10 e CA15): ela abre em UI Toolkit com as quatro faixas do DS
    /// (seção 3) e, sem item selecionado, o botão principal fica desligado com o motivo no tooltip. O batchmode da
    /// revisão roda sem placa de vídeo, então a janela não é mostrada: o teste monta a UI pelo <c>CreateGUI</c>.
    /// </summary>
    public class GasManagerWindowTests
    {
        private GasManagerWindow _window;
        private GameplayEffect _effect;

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
            if (_effect != null) Object.DestroyImmediate(_effect);
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

        [Test]
        public void Footer_DeleteDisabledWithReason_NewTagOnlyInTagTree()
        {
            _window = ScriptableObject.CreateInstance<GasManagerWindow>();
            _window.CreateGUI();
            VisualElement root = _window.rootVisualElement;

            Button delete = root.Query<Button>().Where(b => b.text == "Excluir").First();
            Assert.That(delete, Is.Not.Null, "sem o botão Excluir");
            Assert.That(delete.enabledSelf, Is.False);
            Assert.That(delete.tooltip, Is.EqualTo("Selecione um item na lista."));
            Button newTag = root.Query<Button>().Where(b => b.text == "Nova tag filha").First();
            Assert.That(newTag, Is.Not.Null, "sem o botão Nova tag filha");
            Assert.That(newTag.style.display.value, Is.EqualTo(DisplayStyle.None), "aparece fora da árvore de tags");

            _window.Change(() => _window.Model.SetKind(GasIndexKind.Tag));

            Assert.That(newTag.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(newTag.enabledSelf, Is.False);
            Assert.That(newTag.tooltip, Is.EqualTo("Selecione um item na lista."));
        }

        [Test]
        public void EmptyProject_ShowsCreateButton()
        {
            _window = ScriptableObject.CreateInstance<GasManagerWindow>();
            _window.CreateGUI();
            Button create = _window.rootVisualElement.Query<Button>().Where(b => b.text == "Criar habilidade ou efeito…").First();
            Assert.That(create, Is.Not.Null, "sem o botão do estado vazio");
            Assert.That(create.style.display.value, Is.EqualTo(DisplayStyle.None), "aparece enquanto o projeto é lido");

            _window.Change(() => _window.Model.SetIndex(GasIndex.Build(new Object[0])));
            Assert.That(create.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            _effect = ScriptableObject.CreateInstance<GameplayEffect>();
            _window.Change(() => _window.Model.SetIndex(GasIndex.Build(new Object[] { _effect })));
            Assert.That(create.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
    }
}
