using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Fumaça da janela do assistente (menu de Editor do GAS, ME4; CA1, CA6 e CA15): ela abre em UI Toolkit no passo 1,
    /// com os modelos e o "Avançar" desligado com o motivo, e os menus do assistente substituem o atalho de pastas. O
    /// batchmode roda sem placa de vídeo, então o teste monta a UI pelo <c>CreateGUI</c>, sem mostrar a janela. Sem
    /// janela mostrada não há painel para despachar eventos, então os testes de troca mudam o modelo pelo
    /// <c>Change</c>, que é a porta de todos os controles, e a lista pelo <c>SetSelection</c>.
    /// </summary>
    public class AbilityWizardWindowTests
    {
        private const string Out = "Assets/__AbilityWizardWindowTests";
        private const string Name = "WizardWindowTest";
        private AbilityWizardWindow _window;

        [TearDown]
        public void TearDown()
        {
            if (_window != null) Object.DestroyImmediate(_window);
            AssetDatabase.DeleteAsset(Out);
        }

        [Test]
        public void Open_StepOne_ListsTemplates_AdvanceDisabledWithReason()
        {
            _window = ScriptableObject.CreateInstance<AbilityWizardWindow>();
            _window.CreateGUI();

            VisualElement root = _window.rootVisualElement;
            Assert.That(root.Q<Label>("ability-wizard__step")?.text, Is.EqualTo("Passo 1 de 4"));
            var list = root.Q<ListView>();
            Assert.That(list, Is.Not.Null, "sem a lista de modelos");
            Assert.That(list.itemsSource.Cast<object>().Any(t => ((Object)t).name == "Projectile"), "o Projétil não está na lista");
            Button next = root.Query<Button>().Where(b => b.text == "Avançar").First();
            Assert.That(next, Is.Not.Null, "sem o botão Avançar");
            Assert.That(next.enabledSelf, Is.False);
            Assert.That(next.tooltip, Is.EqualTo("Escolha um modelo."));
        }

        [Test]
        public void StepOne_SelectTemplate_HeadingShowsNameAndDescription()
        {
            _window = ScriptableObject.CreateInstance<AbilityWizardWindow>();
            _window.CreateGUI();
            var list = _window.rootVisualElement.Q<ListView>();

            list.SetSelection(list.itemsSource.IndexOf(Template("Projectile")));

            VisualElement root = _window.rootVisualElement;
            Assert.That(root.Q<Label>("ability-wizard__heading")?.text, Is.EqualTo(Template("Projectile").DisplayName));
            Assert.That(root.Query<Label>(className: "ability-wizard__secondary").ToList().Select(l => l.text),
                Does.Contain(AbilityWizardModel.Describe(Template("Projectile"))));
        }

        [Test]
        public void StepThree_TemplateChanges_ReviewRowsFollowPlan_ListLocked()
        {
            _window = OpenAtReview("Assets");
            var list = _window.rootVisualElement.Q<ListView>();
            List<string> before = RowNames();

            list.SetSelection(list.itemsSource.IndexOf(Template("Area")));

            Assert.That(list.enabledSelf, Is.False, "a lista de modelos fica travada fora do passo 1");
            Assert.That(RowNames(), Is.EqualTo(_window.Model.Rows.Select(r => r.Name).ToList()));
            Assert.That(RowNames(), Is.Not.EqualTo(before));
        }

        [Test]
        public void StepThree_FolderChanges_ReviewMarksCollision()
        {
            AssetDatabase.CreateFolder("Assets", "__AbilityWizardWindowTests");
            AssetDatabase.CopyAsset("Packages/com.spellbook/Editor/GAS/Templates/GA_Template_Projectile.asset", $"{Out}/GA_{Name}.asset");
            _window = OpenAtReview("Assets");

            _window.UseFolder(Out);

            VisualElement row = _window.rootVisualElement.Query(className: "ability-wizard__row").First();
            Assert.That(((Label)row[3]).text, Is.EqualTo("✖ colisão"));
            Button create = _window.rootVisualElement.Query<Button>().Where(b => b.text == "Criar").First();
            Assert.That(create.enabledSelf, Is.False);
        }

        [Test]
        public void Menus_WizardEntriesExist_FolderCreatorGone()
        {
            var menus = TypeCache.GetMethodsWithAttribute<MenuItem>()
                .SelectMany(m => m.GetCustomAttributes<MenuItem>()).Select(a => a.menuItem).ToList();

            Assert.That(menus, Does.Contain("SpellBook/Criar habilidade ou efeito…"));
            Assert.That(menus, Does.Contain("Assets/Create/SpellBook/GAS/Ability from Template…"));
            Assert.That(menus, Does.Not.Contain("Tools/Create Default Project Folders"));
        }

        private static AbilityWizardWindow OpenAtReview(string folder)
        {
            var window = ScriptableObject.CreateInstance<AbilityWizardWindow>();
            window.CreateGUI();
            window.Change(() =>
            {
                window.Model.SelectTemplate(Template("Projectile"));
                window.Model.SetName(Name);
                window.Model.SetFolder(folder);
                window.Model.Next();
                window.Model.Next();
            });
            Assert.That(window.Model.Step, Is.EqualTo(3), "o teste não chegou à revisão");
            return window;
        }

        private List<string> RowNames() => _window.rootVisualElement.Query(className: "ability-wizard__row").ToList()
            .Select(row => ((Label)row[1]).text).ToList();

        private static AbilityTemplate Template(string name) =>
            AssetDatabase.LoadAssetAtPath<AbilityTemplate>($"Packages/com.spellbook/Editor/GAS/Templates/{name}.asset");
    }
}
