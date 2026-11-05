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
using SpellBook.GAS.Thinkers;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Modelos do pacote (menu de Editor do GAS, ME3 e ME5; CA1 e CA3): cada modelo aparece no catálogo e cria um conjunto
    /// sem erro no validador, sem mudar o modelo. A Aura é a exceção: vem sem thinker, e o validador acusa só a ação de
    /// criar thinker sem prefab (Q6). As cópias ficam numa pasta temporária de Assets/.
    /// </summary>
    public class PackageTemplatesTests
    {
        private const string Templates = "Packages/com.spellbook/Editor/GAS/Templates";
        private const string ProjectilePrefab = "Packages/com.spellbook/Runtime/GAS/Prefabs/Projectile.prefab";
        private const string Out = "Assets/__PackageTemplatesTests";
        private const string ThinkerWithoutPrefab = "Create Thinker sem prefab: escolha o thinker criado.";

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
        [TestCase("Aura", "Aura")]
        [TestCase("Blank", "Em branco")]
        [TestCase("EffectOnly", "Só efeito")]
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
            Object aura = AssetDatabase.LoadMainAssetAtPath($"{Templates}/GA_Template_Aura.asset");

            Assert.That(targets, Has.None.Null);
            Assert.That(targets, Does.Contain(aura));
            Assert.That(Errors(targets.Where(t => t != aura)), Is.Empty);
            Assert.That(Errors(new[] { aura }), Is.EqualTo(new[] { ThinkerWithoutPrefab }));
        }

        [TestCase("Projectile", "GA_IceBolt", null)]
        [TestCase("Area", "GA_IceBolt", null)]
        [TestCase("Buff", "GA_IceBolt", null)]
        [TestCase("Aura", "GA_IceBolt", ThinkerWithoutPrefab)]
        [TestCase("Blank", "GA_IceBolt", null)]
        [TestCase("EffectOnly", "GE_IceBolt", null)]
        public void Template_CreatesSetWithoutErrors_TemplateUnchanged(string file, string main, string expectedError)
        {
            AbilityTemplate template = Load(file);
            var models = new List<Object> { template };
            models.AddRange(template.Assets.Select(e => (Object)e.Source));
            models.AddRange(template.Tags.Select(t => (Object)t.Source));
            string[] before = models.Select(m => EditorJsonUtility.ToJson(m)).ToArray();

            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(template, "IceBolt", Out));

            string[] expected = expectedError == null ? new string[0] : new[] { expectedError };
            Assert.That(Errors(set.Paths.Select(AssetDatabase.LoadMainAssetAtPath)), Is.EqualTo(expected));
            Assert.That(set.Main.name, Is.EqualTo(main));
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

        [Test]
        public void Aura_EmptyThinkerActionAndOwnEffect()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("Aura"), "Frost", Out));

            var ability = (GameplayAbility)set.Main;
            var create = (CreateThinkerAction)ability.OnActivate.Single();
            var aura = AssetDatabase.LoadAssetAtPath<GameplayEffect>($"{Out}/GE_Frost_Aura.asset");
            Assert.That(create.Prefab, Is.Null);
            Assert.That(aura, Is.Not.Null);
            Assert.That(aura.DurationPolicy, Is.EqualTo(EffectDurationPolicy.Infinite));
            Assert.That(ability.AbilityTag.Name, Is.EqualTo("Ability.Frost"));
        }

        [Test]
        public void Blank_NoActions_CostCooldownAndTags()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("Blank"), "Leap", Out));

            var ability = (GameplayAbility)set.Main;
            Assert.That(ability.OnActivate, Is.Empty);
            Assert.That(ability.CostEffect.name, Is.EqualTo("GE_Leap_Cost"));
            Assert.That(ability.CooldownEffect.name, Is.EqualTo("GE_Leap_Cooldown"));
            Assert.That(ability.AbilityTag.Name, Is.EqualTo("Ability.Leap"));
            Assert.That(ability.CooldownEffect.GrantedTags.Single().Name, Is.EqualTo("Cooldown.Leap"));
        }

        [Test]
        public void EffectOnly_SingleInfiniteEffect_NoTags()
        {
            CreatedSet set = AbilitySetCreator.Create(AbilitySetPlan.Build(Load("EffectOnly"), "Might", Out));

            var effect = (GameplayEffect)set.Main;
            Assert.That(set.Paths, Is.EqualTo(new[] { $"{Out}/GE_Might.asset" }));
            Assert.That(effect.DurationPolicy, Is.EqualTo(EffectDurationPolicy.Infinite));
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
