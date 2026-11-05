using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Targeting;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Modelos do pacote (menu de Editor do GAS, ME3; CA1 e CA3): cada modelo aparece no catálogo e cria um conjunto sem
    /// erro no validador, sem mudar o modelo. As cópias ficam numa pasta temporária de Assets/.
    /// </summary>
    public class PackageTemplatesTests
    {
        private const string Templates = "Packages/com.spellbook/Editor/GAS/Templates";
        private const string ProjectilePrefab = "Packages/com.spellbook/Runtime/GAS/Prefabs/Projectile.prefab";
        private const string Out = "Assets/__PackageTemplatesTests";

        [SetUp]
        public void SetUp() => AssetDatabase.CreateFolder("Assets", "__PackageTemplatesTests");

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = null;
            AssetDatabase.DeleteAsset(Out);
        }

        [TestCase("Projectile", "Projétil")]
        [TestCase("Area", "Área")]
        [TestCase("Buff", "Buff")]
        public void Catalog_ListsTemplateAsAvailable(string file, string displayName)
        {
            var available = new List<AbilityTemplate>();
            var broken = new List<AbilityTemplate>();

            AbilityTemplateCatalog.Find(available, broken);

            AbilityTemplate template = Load(file);
            Assert.That(available, Does.Contain(template));
            Assert.That(template.DisplayName, Is.EqualTo(displayName));
            Assert.That(template.Description, Is.Not.Empty);
        }

        [Test]
        public void PackageAssets_ValidateWithoutErrors()
        {
            var targets = AssetDatabase.FindAssets("", new[] { Templates }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadMainAssetAtPath).ToList();
            targets.Add(AssetDatabase.LoadMainAssetAtPath(ProjectilePrefab));

            Assert.That(targets, Has.None.Null);
            Assert.That(Errors(targets), Is.Empty);
        }

        [TestCase("Projectile")]
        [TestCase("Area")]
        [TestCase("Buff")]
        public void Template_CreatesSetWithoutErrors_TemplateUnchanged(string file)
        {
            AbilityTemplate template = Load(file);
            var models = new List<Object> { template };
            models.AddRange(template.Assets.Select(e => (Object)e.Source));
            models.AddRange(template.Tags.Select(t => (Object)t.Source));
            string[] before = models.Select(m => EditorJsonUtility.ToJson(m)).ToArray();

            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(template, "IceBolt", Out));

            Assert.That(Errors(set.Paths.Select(AssetDatabase.LoadMainAssetAtPath)), Is.Empty);
            Assert.That(set.Main.name, Is.EqualTo("GA_IceBolt"));
            Assert.That(models.Select(m => EditorJsonUtility.ToJson(m)), Is.EqualTo(before));
        }

        [Test]
        public void Projectile_CopiesLinked_PrefabFromPackage()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("Projectile"), "IceBolt", Out));

            var ability = (GameplayAbility)set.Main;
            var spawn = (SpawnProjectileAction)ability.OnActivate.Single();
            var hit = (ApplyEffectAction)spawn.OnHitActions.Single();
            Assert.That(ability.CostEffect.name, Is.EqualTo("GE_IceBolt_Cost"));
            Assert.That(ability.CooldownEffect.name, Is.EqualTo("GE_IceBolt_Cooldown"));
            Assert.That(hit.Effects.Single().name, Is.EqualTo("GE_IceBolt_Damage"));
            Assert.That(spawn.ProjectilePrefab, Is.SameAs(AssetDatabase.LoadMainAssetAtPath(ProjectilePrefab)));
            Assert.That(ability.AbilityTag.Name, Is.EqualTo("Ability.IceBolt"));
            Assert.That(ability.AbilityTag.GetDefinition().Parent.name, Is.EqualTo("Ability"));
            Assert.That(ability.CooldownEffect.GrantedTags.Single().Name, Is.EqualTo("Cooldown.IceBolt"));
        }

        [Test]
        public void Area_DamagesAroundCaster()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("Area"), "Nova", Out));

            var ability = (GameplayAbility)set.Main;
            var apply = (ApplyEffectAction)ability.OnActivate.Single();
            Assert.That(apply.Effects.Single().name, Is.EqualTo("GE_Nova_Damage"));
            Assert.That(((AreaSelector)apply.Target).Origin, Is.EqualTo(AreaOrigin.Caster));
            Assert.That(ability.CooldownEffect.GrantedTags.Single().Name, Is.EqualTo("Cooldown.Nova"));
        }

        [Test]
        public void Buff_AppliesTimedBuffToCaster()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("Buff"), "Haste", Out));

            var ability = (GameplayAbility)set.Main;
            var apply = (ApplyEffectAction)ability.OnActivate.Single();
            GameplayEffect buff = apply.Effects.Single();
            Assert.That(buff.name, Is.EqualTo("GE_Haste_Buff"));
            Assert.That(buff.IsBuff, Is.True);
            Assert.That(buff.DurationPolicy, Is.EqualTo(EffectDurationPolicy.HasDuration));
            Assert.That(apply.Target, Is.TypeOf<CasterSelector>());
            Assert.That(ability.CostEffect.name, Is.EqualTo("GE_Haste_Cost"));
        }

        private static AbilityTemplate Load(string file) =>
            AssetDatabase.LoadAssetAtPath<AbilityTemplate>($"{Templates}/{file}.asset");

        private static IEnumerable<string> Errors(IEnumerable<Object> targets)
        {
            var messages = new List<ValidationMessage>();
            ProjectValidator.Validate(targets, messages);
            return messages.Where(m => m.Severity == ValidationSeverity.Error).Select(m => m.Message);
        }
    }
}
