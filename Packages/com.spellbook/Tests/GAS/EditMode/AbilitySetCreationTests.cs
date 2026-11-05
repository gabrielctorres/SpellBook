using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Motor de criação do conjunto (menu de Editor do GAS, ME2; CA2, CA3 e CA4): plano de nomes, cópia ligada,
    /// tags, falha sem sobras e apagar o conjunto. Os modelos e as cópias ficam numa pasta temporária de Assets/.
    /// </summary>
    public class AbilitySetCreationTests
    {
        private const string Root = "Assets/__AbilitySetCreationTests";
        private const string Models = Root + "/Models";
        private const string Out = Root + "/Out";

        private GameplayTagDefinition _abilityTagModel;
        private GameplayTagDefinition _cooldownTagModel;
        private GameplayEffect _cost;
        private GameplayEffect _cooldown;
        private GameplayEffect _damage;
        private GameplayAbility _ability;
        private GameObject _prefab;
        private AbilityTemplate _template;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "__AbilitySetCreationTests");
            AssetDatabase.CreateFolder(Root, "Models");
            AssetDatabase.CreateFolder(Root, "Out");

            _abilityTagModel = Save(ScriptableObject.CreateInstance<GameplayTagDefinition>(), "TAG_AbilityModel");
            _cooldownTagModel = Save(ScriptableObject.CreateInstance<GameplayTagDefinition>(), "TAG_CooldownModel");
            _cost = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Model_Cost");
            _cooldown = ScriptableObject.CreateInstance<GameplayEffect>();
            _cooldown.GrantedTags = new List<GameplayTag> { new GameplayTag(_cooldownTagModel) };
            Save(_cooldown, "GE_Model_Cooldown");
            _damage = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Model_Damage");

            var go = new GameObject("Projectile");
            _prefab = PrefabUtility.SaveAsPrefabAsset(go, Models + "/Projectile.prefab");
            Object.DestroyImmediate(go);

            _ability = ScriptableObject.CreateInstance<GameplayAbility>();
            _ability.AbilityTag = new GameplayTag(_abilityTagModel);
            _ability.CostEffect = _cost;
            _ability.CooldownEffect = _cooldown;
            var damage = new ApplyEffectAction { Effects = new List<GameplayEffect> { _damage } };
            _ability.OnActivate.Add(new SpawnProjectileAction { ProjectilePrefab = _prefab, OnHitActions = new List<GameplayAction> { damage } });
            Save(_ability, "GA_Model");

            _template = Save(ScriptableObject.CreateInstance<AbilityTemplate>(), "Template");
            var serialized = new SerializedObject(_template);
            AddEntry(serialized, "assets", "source", _ability, "nameRule", "GA_{Name}");
            AddEntry(serialized, "assets", "source", _cost, "nameRule", "GE_{Name}_Cost");
            AddEntry(serialized, "assets", "source", _cooldown, "nameRule", "GE_{Name}_Cooldown");
            AddEntry(serialized, "assets", "source", _damage, "nameRule", "GE_{Name}_Damage");
            AddEntry(serialized, "tags", "source", _abilityTagModel, "rule", "SetTestAbility.{Name}");
            AddEntry(serialized, "tags", "source", _cooldownTagModel, "rule", "SetTestCooldown.{Name}");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void CheckName_EmptyOrInvalid_SaysWhy()
        {
            Assert.That(AbilitySetPlan.CheckName(""), Is.EqualTo("Digite um nome."));
            Assert.That(AbilitySetPlan.CheckName("  "), Is.EqualTo("Digite um nome."));
            Assert.That(AbilitySetPlan.CheckName("Ice Bolt"), Is.EqualTo("Use só letras, números e _."));
            Assert.That(AbilitySetPlan.CheckName("Ice.Bolt"), Is.EqualTo("Use só letras, números e _."));
            Assert.That(AbilitySetPlan.CheckName("Ice_Bolt2"), Is.Null);

            AbilitySetPlan plan = AbilitySetPlan.Build(_template, "Ice Bolt", Out);

            Assert.That(plan.CanCreate, Is.False);
            Assert.That(plan.Problem, Is.EqualTo("Use só letras, números e _."));
        }

        [Test]
        public void Build_FinalNamesMainFirstAndPrefabAsReference()
        {
            AbilitySetPlan plan = AbilitySetPlan.Build(_template, "IceBolt", Out);

            Assert.That(plan.Assets.Select(a => a.Name),
                Is.EqualTo(new[] { "GA_IceBolt", "GE_IceBolt_Cost", "GE_IceBolt_Cooldown", "GE_IceBolt_Damage" }));
            Assert.That(plan.Assets.Select(a => a.Path), Has.All.StartWith(Out + "/").And.All.EndWith(".asset"));
            Assert.That(plan.Assets.Select(a => a.IsMain), Is.EqualTo(new[] { true, false, false, false }));
            Assert.That(plan.Assets.Select(a => a.Status), Has.All.EqualTo(PlannedStatus.New));
            Assert.That(plan.References, Is.EqualTo(new Object[] { _prefab }));
            Assert.That(plan.CanCreate, Is.True);
        }

        [Test]
        public void Build_ExistingAsset_CollisionDisablesCreate()
        {
            Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_IceBolt_Cost", Out);

            AbilitySetPlan plan = AbilitySetPlan.Build(_template, "IceBolt", Out);

            Assert.That(plan.Assets[1].Status, Is.EqualTo(PlannedStatus.Collision));
            Assert.That(plan.Assets[0].Status, Is.EqualTo(PlannedStatus.New));
            Assert.That(plan.CanCreate, Is.False);
            Assert.That(plan.Problem, Is.EqualTo("Já existe um asset GE_IceBolt_Cost nesta pasta. Escolha outro nome."));
        }

        [Test]
        public void Build_FolderOutsideAssets_DisablesCreate()
        {
            AbilitySetPlan missing = AbilitySetPlan.Build(_template, "IceBolt", Root + "/NaoExiste");
            AbilitySetPlan package = AbilitySetPlan.Build(_template, "IceBolt", "Packages/com.spellbook");

            Assert.That(missing.Problem, Is.EqualTo("Escolha uma pasta dentro de Assets."));
            Assert.That(package.Problem, Is.EqualTo("Escolha uma pasta dentro de Assets."));
            Assert.That(package.CanCreate, Is.False);
        }

        [Test]
        public void Build_TagsWithParent_NewOrExisting()
        {
            ExistingCooldownParent();

            AbilitySetPlan plan = AbilitySetPlan.Build(_template, "IceBolt", Out);

            Assert.That(plan.Tags.Select(t => $"{t.Name}|{t.ParentName}|{t.Status}"), Is.EqualTo(new[]
            {
                "SetTestAbility||New",
                "SetTestAbility.IceBolt|SetTestAbility|New",
                "SetTestCooldown||Exists",
                "SetTestCooldown.IceBolt|SetTestCooldown|New",
            }));
            Assert.That(plan.Tags[1].Source, Is.SameAs(_abilityTagModel));
            Assert.That(plan.Tags[1].Path, Is.EqualTo(Out + "/SetTestAbility.IceBolt.asset"));
            Assert.That(plan.Tags[2].Existing, Is.Not.Null);
        }

        [Test]
        public void Create_CopiesPointToCopies_InsideSerializeReference()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "IceBolt", Out));

            var ability = Load<GameplayAbility>("GA_IceBolt");
            Assert.That(set.Main, Is.SameAs(ability));
            Assert.That(ability.CostEffect, Is.SameAs(Load<GameplayEffect>("GE_IceBolt_Cost")));
            Assert.That(ability.CooldownEffect, Is.SameAs(Load<GameplayEffect>("GE_IceBolt_Cooldown")));
            var spawn = (SpawnProjectileAction)ability.OnActivate.Single();
            var damage = (ApplyEffectAction)spawn.OnHitActions.Single();
            Assert.That(damage.Effects.Single(), Is.SameAs(Load<GameplayEffect>("GE_IceBolt_Damage")));
        }

        [Test]
        public void Create_TagSourceSwapped_ParentCreated_ExistingReused()
        {
            GameplayTagDefinition existing = ExistingCooldownParent();

            AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "IceBolt", Out));

            var abilityTag = Load<GameplayTagDefinition>("SetTestAbility.IceBolt");
            var cooldownTag = Load<GameplayTagDefinition>("SetTestCooldown.IceBolt");
            Assert.That(abilityTag.Parent, Is.SameAs(Load<GameplayTagDefinition>("SetTestAbility")));
            Assert.That(cooldownTag.Parent, Is.SameAs(existing));
            Assert.That(Load<GameplayAbility>("GA_IceBolt").AbilityTag.GetDefinition(), Is.SameAs(abilityTag));
            Assert.That(Load<GameplayEffect>("GE_IceBolt_Cooldown").GrantedTags.Single().GetDefinition(), Is.SameAs(cooldownTag));
            Assert.That(File.Exists(Out + "/SetTestCooldown.asset"), Is.False);
        }

        [Test]
        public void Create_KeepsPrefab_TemplatesUnchanged_SetsIndependent()
        {
            AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "IceBolt", Out));
            AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "Fireball", Out));

            var ice = Load<GameplayAbility>("GA_IceBolt");
            var fire = Load<GameplayAbility>("GA_Fireball");
            Assert.That(((SpawnProjectileAction)ice.OnActivate.Single()).ProjectilePrefab, Is.SameAs(_prefab));
            Assert.That(AssetDatabase.FindAssets("t:Prefab", new[] { Out }), Is.Empty);
            Assert.That(fire.CostEffect, Is.Not.SameAs(ice.CostEffect));
            Assert.That(fire.AbilityTag, Is.Not.EqualTo(ice.AbilityTag));
            Assert.That(_ability.CostEffect, Is.SameAs(_cost));
            Assert.That(_ability.AbilityTag.GetDefinition(), Is.SameAs(_abilityTagModel));
            Assert.That(_cooldown.GrantedTags.Single().GetDefinition(), Is.SameAs(_cooldownTagModel));
            var damage = (ApplyEffectAction)((SpawnProjectileAction)_ability.OnActivate.Single()).OnHitActions.Single();
            Assert.That(damage.Effects.Single(), Is.SameAs(_damage));
        }

        [Test]
        public void Create_SelectsMain_CopyValidatesWithoutErrors()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "IceBolt", Out));
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(set.Paths.Select(AssetDatabase.LoadMainAssetAtPath).ToArray(), messages);

            Assert.That(Selection.activeObject, Is.SameAs(set.Main));
            Assert.That(messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message), Is.Empty);
        }

        [Test]
        public void Create_StepFails_NothingLeft()
        {
            AbilitySetPlan plan = AbilitySetPlan.Build(_template, "IceBolt", Out);
            GameplayEffect blocker = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_IceBolt_Damage", Out);

            Assert.That(() => AbilitySetCreator.Create(plan), Throws.InvalidOperationException
                .With.Message.EqualTo("Já existe um asset GE_IceBolt_Damage nesta pasta. Escolha outro nome."));

            string[] left = AssetDatabase.FindAssets("", new[] { Out }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(left, Is.EqualTo(new[] { AssetDatabase.GetAssetPath(blocker) }));
        }

        [Test]
        public void Create_NoAssets_ProblemAndNothingLeft()
        {
            var tags = new List<(GameplayTagDefinition, string)> { (null, "SetTestAbility.{Name}") };
            AbilitySetPlan plan = AbilitySetPlan.Build("IceBolt", Out, new List<(ScriptableObject, string)>(), tags);

            Assert.That(plan.CanCreate, Is.False);
            Assert.That(plan.Problem, Is.EqualTo("O conjunto precisa de pelo menos um asset."));
            Assert.That(() => AbilitySetCreator.Create(plan), Throws.InvalidOperationException);
            Assert.That(AssetDatabase.FindAssets("", new[] { Out }), Is.Empty);
        }

        [Test]
        public void Delete_RemovesCreatedKeepsReusedTags()
        {
            GameplayTagDefinition existing = ExistingCooldownParent();
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(_template, "IceBolt", Out));
            Assert.That(set.Paths, Has.Count.EqualTo(7));

            AbilitySetCreator.Delete(set);

            Assert.That(AssetDatabase.FindAssets("", new[] { Out }), Is.Empty);
            Assert.That(existing != null, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(_ability), Is.EqualTo(Models + "/GA_Model.asset"));
        }

        // Q8: a Undo da Unity não apaga o arquivo que o AssetDatabase criou, então o Ctrl+Z não desfaz a criação e o
        // "Desfazer criação" do ME4 usa o Delete.
        [Test]
        public void Undo_DoesNotCoverAssetDatabaseFiles()
        {
            var tag = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(tag, "Criar");
            AssetDatabase.CreateAsset(tag, Out + "/UndoProbe.asset");

            Undo.PerformUndo();

            Assert.That(File.Exists(Out + "/UndoProbe.asset"), Is.True);
        }

        private static T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Out + "/" + name + ".asset");

        // A tag pai SetTestCooldown já existe no projeto, fora da pasta escolhida.
        private static GameplayTagDefinition ExistingCooldownParent()
        {
            AssetDatabase.CreateFolder(Root, "Tags");
            return Save(ScriptableObject.CreateInstance<GameplayTagDefinition>(), "SetTestCooldown", Root + "/Tags");
        }

        private static T Save<T>(T asset, string name, string folder = Models) where T : Object
        {
            AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset");
            return asset;
        }

        private static void AddEntry(SerializedObject serialized, string list, string objectField, Object value,
            string textField, string text)
        {
            SerializedProperty items = serialized.FindProperty(list);
            items.InsertArrayElementAtIndex(items.arraySize);
            SerializedProperty entry = items.GetArrayElementAtIndex(items.arraySize - 1);
            entry.FindPropertyRelative(objectField).objectReferenceValue = value;
            entry.FindPropertyRelative(textField).stringValue = text;
        }
    }
}
