using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Estado do assistente de criação, sem UI (menu de Editor do GAS, ME4; CA1, CA2, CA4 e CA15): os modelos com a
    /// frase, os quatro passos com o motivo de cada um, a lista do que será criado, a criação e o "Desfazer criação".
    /// Usa o modelo Projétil do pacote e grava numa pasta temporária de Assets/.
    /// </summary>
    public class AbilityWizardModelTests
    {
        private const string Out = "Assets/__AbilityWizardModelTests";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "__AbilityWizardModelTests");

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            AssetDatabase.DeleteAsset(Out);
        }

        [Test]
        public void Load_ListsPackageTemplates_BrokenLeftOutWithNote()
        {
            AbilityTemplate broken = CreateTemplate("Modelo quebrado", null);
            var model = new AbilityWizardModel();

            model.Load();

            Assert.That(model.Templates, Has.Member(Projectile()));
            Assert.That(model.Templates, Has.No.Member(broken));
            Assert.That(model.BrokenNote, Does.EndWith(" com asset ausente: veja SpellBook/Validar projeto"));
            Assert.That(model.BrokenNote, Does.StartWith(model.BrokenCount == 1 ? "1 modelo " : $"{model.BrokenCount} modelos "));
            Assert.That(AbilityWizardModel.Describe(Projectile()), Is.EqualTo(Projectile().Description));
        }

        [Test]
        public void Describe_EmptyDescription_SaysProjectTemplate()
        {
            AbilityTemplate template = CreateTemplate("Modelo sem frase", ScriptableObject.CreateInstance<GameplayAbility>());

            Assert.That(AbilityWizardModel.Describe(template), Is.EqualTo("Modelo do projeto"));
        }

        [Test]
        public void Steps_AdvanceOnlyWhenValid_WithReason()
        {
            var model = new AbilityWizardModel();
            Assert.That((model.Step, model.StepLabel, model.StepTitle), Is.EqualTo((1, "Passo 1 de 4", "Modelo")));
            Assert.That(model.Reason, Is.EqualTo("Escolha um modelo."));

            model.SelectTemplate(Projectile());
            model.Next();
            Assert.That((model.Step, model.StepTitle), Is.EqualTo((2, "Nome e pasta")));
            Assert.That(model.Reason, Is.EqualTo("Digite um nome."));
            model.SetName("Ice Bolt");
            Assert.That(model.Reason, Is.EqualTo("Use só letras, números e _."));
            model.SetName("IceBolt");
            model.SetFolder("Packages/com.spellbook");
            Assert.That(model.Reason, Is.EqualTo("Escolha uma pasta dentro de Assets."));
            model.Next();
            Assert.That(model.Step, Is.EqualTo(2), "avançou com a pasta fora de Assets/");

            model.SetFolder(Out);
            Assert.That(model.Reason, Is.Null);
            model.Next();
            Assert.That((model.Step, model.StepLabel, model.StepTitle), Is.EqualTo((3, "Passo 3 de 4", "Revisão")));
            model.Back();
            Assert.That(model.Step, Is.EqualTo(2));
        }

        [Test]
        public void Rows_FinalNamesTagsNewOrExistingAndPrefabReference()
        {
            CreateTag("Ability");
            AbilityWizardModel model = AtReview("IceBolt");

            List<AbilityWizardRow> rows = model.Rows;

            AbilityWizardRow main = rows.Single(r => r.Name == "GA_IceBolt");
            Assert.That((main.Type, main.Marker, main.IsMain), Is.EqualTo(("Habilidade", "+ nova", true)));
            Assert.That(rows.Single(r => r.Name == "GE_IceBolt_Cost").Type, Is.EqualTo("Efeito"));
            Assert.That(rows.Count(r => r.IsMain), Is.EqualTo(1));
            AbilityWizardRow parent = rows.Single(r => r.Name == "Ability");
            Assert.That((parent.Type, parent.Marker), Is.EqualTo(("Tag", "↺ já existe")));
            AbilityWizardRow tag = rows.Single(r => r.Name == "Ability.IceBolt");
            Assert.That((tag.Type, tag.Marker), Is.EqualTo(("Tag (pai: Ability)", "+ nova")));
            AbilityWizardRow prefab = rows.Single(r => r.Name == "Projectile");
            Assert.That((prefab.Type, prefab.Marker), Is.EqualTo(("Prefab do pacote", "🔗 referência")));
            Assert.That(prefab.Tooltip, Does.StartWith("Este prefab é do pacote e é compartilhado."));
            Assert.That(model.CanCreate, Is.True);
        }

        [Test]
        public void Collision_DisablesCreate_SaysWhichAsset()
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameplayAbility>(), Out + "/GA_IceBolt.asset");

            AbilityWizardModel model = AtReview("IceBolt");

            Assert.That(model.Step, Is.EqualTo(3), "a colisão só desliga o Criar, na revisão");
            Assert.That(model.CanCreate, Is.False);
            Assert.That(model.Reason, Is.EqualTo("Já existe um asset GA_IceBolt nesta pasta. Escolha outro nome."));
            Assert.That(model.Rows.Single(r => r.Name == "GA_IceBolt").Marker, Is.EqualTo("✖ colisão"));
        }

        [Test]
        public void Create_SelectsMain_ThenUndoDeletesSetAndReturnsToName()
        {
            AbilityWizardModel model = AtReview("IceBolt");
            int tags = model.Rows.Count(r => r.Type.StartsWith("Tag") && r.Marker == "+ nova");

            model.Create();

            Assert.That(model.Step, Is.EqualTo(4));
            Assert.That(Selection.activeObject.name, Is.EqualTo("GA_IceBolt"));
            Assert.That(model.ResultText, Is.EqualTo($"Conjunto criado: 4 assets e {tags} tags"));
            Assert.That(model.UndoConfirm, Is.EqualTo($"Apagar os 4 assets e {tags} tags criados?"));
            List<string> paths = model.Created.Paths.ToList();
            Assert.That(paths.All(p => AssetDatabase.LoadMainAssetAtPath(p) != null));

            model.UndoCreation();

            Assert.That(paths.Where(p => AssetDatabase.LoadMainAssetAtPath(p) != null), Is.Empty);
            Assert.That((model.Step, model.Name, model.Created), Is.EqualTo((2, "IceBolt", (CreatedSet)null)));
            Assert.That(model.Reason, Is.Null);
        }

        [Test]
        public void FolderOf_FolderAssetOrNothing()
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameplayAbility>(), Out + "/GA_Some.asset");

            Assert.That(AbilityWizardModel.FolderOf(Out), Is.EqualTo(Out));
            Assert.That(AbilityWizardModel.FolderOf(Out + "/GA_Some.asset"), Is.EqualTo(Out));
            Assert.That(AbilityWizardModel.FolderOf(null), Is.EqualTo("Assets"));
            Assert.That(AbilityWizardModel.FolderOf(string.Empty), Is.EqualTo("Assets"));
        }

        private static AbilityWizardModel AtReview(string name)
        {
            var model = new AbilityWizardModel();
            model.SelectTemplate(Projectile());
            model.Next();
            model.SetName(name);
            model.SetFolder(Out);
            model.Next();
            return model;
        }

        private static AbilityTemplate Projectile() =>
            AssetDatabase.LoadAssetAtPath<AbilityTemplate>("Packages/com.spellbook/Editor/GAS/Templates/Projectile.asset");

        private static void CreateTag(string tagName) =>
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameplayTagDefinition>(), $"{Out}/{tagName}.asset");

        // Modelo do projeto com um asset modelo; sem fonte, a entrada fica com a referência vazia e o modelo, quebrado.
        private static AbilityTemplate CreateTemplate(string templateName, GameplayAbility source)
        {
            if (source != null) AssetDatabase.CreateAsset(source, $"{Out}/GA_{templateName}.asset");
            var template = ScriptableObject.CreateInstance<AbilityTemplate>();
            AssetDatabase.CreateAsset(template, $"{Out}/{templateName}.asset");
            var serialized = new SerializedObject(template);
            SerializedProperty assets = serialized.FindProperty("assets");
            assets.InsertArrayElementAtIndex(0);
            assets.GetArrayElementAtIndex(0).FindPropertyRelative("source").objectReferenceValue = source;
            assets.GetArrayElementAtIndex(0).FindPropertyRelative("nameRule").stringValue = "GA_{Name}";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return template;
        }
    }
}
