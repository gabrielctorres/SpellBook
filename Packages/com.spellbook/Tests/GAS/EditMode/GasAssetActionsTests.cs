using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Duplicar e Renomear do gerenciador do GAS (ME9; CA11, CA12 e CA13): a cópia da habilidade tem custo, recarga e
    /// tags próprios e compartilha o resto; renomear leva junto os derivados marcados sem quebrar referência; renomear
    /// uma tag leva as filhas. Os assets ficam numa pasta temporária de Assets/.
    /// </summary>
    public class GasAssetActionsTests
    {
        private const string Root = "Assets/__GasAssetActionsTests";

        private GameplayTagDefinition _abilityRoot;
        private GameplayTagDefinition _abilityTag;
        private GameplayTagDefinition _cooldownTag;
        private GameplayEffect _cost;
        private GameplayEffect _cooldown;
        private GameplayEffect _damage;
        private GameplayEffect _burn;
        private GameplayAbility _ability;
        private GasActionDialog _dialog;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "__GasAssetActionsTests");
            _abilityRoot = Tag("ActTestAbility", null);
            _abilityTag = Tag("ActTestAbility.Fireball", _abilityRoot);
            _cooldownTag = Tag("ActTestCooldown.Fireball", Tag("ActTestCooldown", null));
            _cost = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Fireball_Cost");
            _cooldown = ScriptableObject.CreateInstance<GameplayEffect>();
            _cooldown.GrantedTags = new List<GameplayTag> { new GameplayTag(_cooldownTag) };
            Save(_cooldown, "GE_Fireball_Cooldown");
            _damage = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Fireball_Damage");
            _burn = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Burn");

            _ability = ScriptableObject.CreateInstance<GameplayAbility>();
            _ability.AbilityTag = new GameplayTag(_abilityTag);
            _ability.CostEffect = _cost;
            _ability.CooldownEffect = _cooldown;
            _ability.OnActivate.Add(new ApplyEffectAction { Effects = new List<GameplayEffect> { _damage, _burn } });
            Save(_ability, "GA_Fireball");
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (_dialog != null) Object.DestroyImmediate(_dialog);
            Selection.activeObject = null;
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
        }

        [Test]
        public void Duplicate_Ability_OwnCostCooldownAndTags_SharesOtherEffects()
        {
            Assert.That(GasDuplicate.Suggest(_ability), Is.EqualTo("Fireball_2"));
            AbilitySetPlan plan = GasDuplicate.Plan(_ability, "Fireball_2");
            Assert.That(GasDuplicate.Problem(plan), Is.Null);

            var copy = (GameplayAbility)GasDuplicate.Create(plan);

            Assert.That(AssetDatabase.GetAssetPath(copy), Is.EqualTo(Root + "/GA_Fireball_2.asset"));
            Assert.That(copy.CostEffect, Is.Not.EqualTo(_cost));
            Assert.That(copy.CostEffect.name, Is.EqualTo("GE_Fireball_2_Cost"));
            Assert.That(copy.CooldownEffect.name, Is.EqualTo("GE_Fireball_2_Cooldown"));
            Assert.That(copy.CooldownEffect.GrantedTags[0].Name, Is.EqualTo("ActTestCooldown.Fireball_2"));
            Assert.That(copy.AbilityTag.Name, Is.EqualTo("ActTestAbility.Fireball_2"));
            Assert.That(copy.AbilityTag.GetDefinition().Parent, Is.EqualTo(_abilityRoot));
            Assert.That(((ApplyEffectAction)copy.OnActivate[0]).Effects, Is.EqualTo(new[] { _damage, _burn }));
            Assert.That(GasDuplicate.Shared(_ability), Is.EqualTo(new[] { _damage, _burn }));
            Assert.That(_ability.CostEffect, Is.EqualTo(_cost), "a original mudou");
            Assert.That(_cooldown.GrantedTags[0].GetDefinition(), Is.EqualTo(_cooldownTag), "a recarga original mudou");
        }

        [Test]
        public void Duplicate_Effect_CopiesOnlyEffect()
        {
            Assert.That(GasDuplicate.Suggest(_damage), Is.EqualTo("GE_Fireball_Damage_2"));
            AbilitySetPlan plan = GasDuplicate.Plan(_damage, "GE_Fireball_Damage_2");

            Assert.That(plan.Assets.Select(a => a.Name), Is.EqualTo(new[] { "GE_Fireball_Damage_2" }));
            Assert.That(plan.Tags, Is.Empty);
            Object copy = GasDuplicate.Create(plan);
            Assert.That(copy, Is.InstanceOf<GameplayEffect>());
            Assert.That(AssetDatabase.GetAssetPath(copy), Is.EqualTo(Root + "/GE_Fireball_Damage_2.asset"));
            Assert.That(GasDuplicate.CanDuplicate(_ability) && GasDuplicate.CanDuplicate(_damage), Is.True);
            Assert.That(GasDuplicate.CanDuplicate(_abilityTag), Is.False);
        }

        [Test]
        public void Duplicate_Suggest_SkipsTakenNamesAndTags()
        {
            Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Fireball_2_Cost");
            Assert.That(GasDuplicate.Suggest(_ability), Is.EqualTo("Fireball_3"));

            Tag("ActTestAbility.Fireball_3", _abilityRoot);

            Assert.That(GasDuplicate.Problem(GasDuplicate.Plan(_ability, "Fireball_3")),
                Is.EqualTo("Já existe a tag ActTestAbility.Fireball_3. Escolha outro nome."));
            Assert.That(GasDuplicate.Suggest(_ability), Is.EqualTo("Fireball_4"));
        }

        [Test]
        public void Duplicate_Suggest_DefaultNameWithSpace_StopsWithProblem()
        {
            GameplayEffect effect = Save(ScriptableObject.CreateInstance<GameplayEffect>(), "New Effect");

            string name = GasDuplicate.Suggest(effect);

            Assert.That(name, Is.EqualTo("New Effect_2"));
            Assert.That(GasDuplicate.Problem(GasDuplicate.Plan(effect, name)), Is.EqualTo("Use só letras, números e _."));
        }

        [Test]
        public void Duplicate_Suggest_OutsideAssets_StopsWithProblem()
        {
            var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>("Packages/com.spellbook/Editor/GAS/Templates/GE_Template_Damage.asset");

            string name = GasDuplicate.Suggest(effect);

            Assert.That(name, Is.EqualTo("GE_Template_Damage_2"));
            Assert.That(GasDuplicate.Problem(GasDuplicate.Plan(effect, name)), Is.EqualTo("Escolha uma pasta dentro de Assets."));
        }

        [Test]
        public void Rename_Ability_DerivedChecked_KeepsReferences()
        {
            GasRenamePlan plan = GasRenamePlan.Build(_ability, "Firebolt");

            Assert.That(plan.NewName, Is.EqualTo("GA_Firebolt"));
            Assert.That(plan.Items.Select(i => $"{i.Asset.name} → {i.NewName}"), Is.EquivalentTo(new[]
            {
                "GE_Fireball_Cost → GE_Firebolt_Cost", "GE_Fireball_Cooldown → GE_Firebolt_Cooldown",
                "GE_Fireball_Damage → GE_Firebolt_Damage", "ActTestAbility.Fireball → ActTestAbility.Firebolt",
                "ActTestCooldown.Fireball → ActTestCooldown.Firebolt",
            }));
            Assert.That(plan.Items.Select(i => i.Checked), Has.All.True);
            Assert.That(plan.Count, Is.EqualTo(6));
            Assert.That(plan.Problem, Is.Null);

            plan.Apply();

            Assert.That(AssetDatabase.GetAssetPath(_ability), Is.EqualTo(Root + "/GA_Firebolt.asset"));
            Assert.That(_ability.CostEffect, Is.EqualTo(_cost));
            Assert.That(AssetDatabase.GetAssetPath(_cost), Is.EqualTo(Root + "/GE_Firebolt_Cost.asset"));
            Assert.That(_ability.AbilityTag.Name, Is.EqualTo("ActTestAbility.Firebolt"));
            Assert.That(_cooldown.GrantedTags[0].Name, Is.EqualTo("ActTestCooldown.Firebolt"));
            Assert.That(((ApplyEffectAction)_ability.OnActivate[0]).Effects[0].name, Is.EqualTo("GE_Firebolt_Damage"));
            Assert.That(_burn.name, Is.EqualTo("GE_Burn"));
        }

        [Test]
        public void Rename_Unchecked_KeepsName()
        {
            GasRenamePlan plan = GasRenamePlan.Build(_ability, "Firebolt");
            int damage = plan.Items.ToList().FindIndex(i => i.Asset == _damage);

            plan.SetChecked(damage, false);
            plan.Apply();

            Assert.That(plan.Count, Is.EqualTo(5));
            Assert.That(AssetDatabase.GetAssetPath(_damage), Is.EqualTo(Root + "/GE_Fireball_Damage.asset"));
            Assert.That(_cost.name, Is.EqualTo("GE_Firebolt_Cost"));
        }

        [Test]
        public void Rename_ExistingOrSameName_Refused()
        {
            Assert.That(GasRenamePlan.Build(_ability, "Fireball").Problem, Is.EqualTo("Digite um nome diferente do atual."));
            Assert.That(GasRenamePlan.Build(_ability, "Fire bolt").Problem, Is.EqualTo("Use só letras, números e _."));

            Save(ScriptableObject.CreateInstance<GameplayEffect>(), "GE_Firebolt_Damage");
            Tag("ActTestCooldown.Firebolt", null);
            GasRenamePlan plan = GasRenamePlan.Build(_ability, "Firebolt");
            Assert.That(plan.Problem, Is.EqualTo("Já existe um asset GE_Firebolt_Damage nesta pasta. Escolha outro nome."));

            plan.SetChecked(plan.Items.ToList().FindIndex(i => i.Asset == _damage), false);
            Assert.That(plan.Problem, Is.EqualTo("Já existe a tag ActTestCooldown.Firebolt. Escolha outro nome."));

            plan.SetChecked(plan.Items.ToList().FindIndex(i => i.Asset == _cooldownTag), false);
            Assert.That(plan.Problem, Is.Null);
        }

        [Test]
        public void Rename_TagWithChildren_RenamesCheckedChildren()
        {
            GameplayTagDefinition charged = Tag("ActTestAbility.Fireball.Charged", _abilityTag);
            Assert.That(GasRenamePlan.Build(_abilityTag, "Heavy").NewName, Is.EqualTo("ActTestAbility.Heavy"));

            GasRenamePlan plan = GasRenamePlan.Build(_abilityRoot, "ActTestSkill");

            Assert.That(plan.NewName, Is.EqualTo("ActTestSkill"));
            Assert.That(plan.Items.Select(i => i.NewName),
                Is.EquivalentTo(new[] { "ActTestSkill.Fireball", "ActTestSkill.Fireball.Charged" }));
            plan.Apply();
            Assert.That(_abilityRoot.name, Is.EqualTo("ActTestSkill"));
            Assert.That(charged.name, Is.EqualTo("ActTestSkill.Fireball.Charged"));
            Assert.That(_abilityTag.Parent, Is.EqualTo(_abilityRoot));
            Assert.That(_ability.AbilityTag.Name, Is.EqualTo("ActTestSkill.Fireball"));
        }

        [Test]
        public void Rename_CaseOnly_Accepted()
        {
            GasRenamePlan plan = GasRenamePlan.Build(_ability, "FireBall");
            Assert.That(plan.Problem, Is.Null);

            plan.Apply();

            Assert.That(AssetDatabase.GetAssetPath(_ability), Is.EqualTo(Root + "/GA_FireBall.asset"));
            Assert.That(AssetDatabase.GetAssetPath(_cost), Is.EqualTo(Root + "/GE_FireBall_Cost.asset"));
            Assert.That(_ability.AbilityTag.Name, Is.EqualTo("ActTestAbility.FireBall"));
        }

        [Test]
        public void Dialog_Rename_TogglesCountAndReason()
        {
            _dialog = ScriptableObject.CreateInstance<GasActionDialog>();
            _dialog.Build(_ability, true);
            VisualElement root = _dialog.rootVisualElement;
            Assert.That(root.Q<TextField>().value, Is.EqualTo("Fireball"));

            _dialog.SetName("Firebolt");
            List<Toggle> toggles = root.Query<Toggle>().ToList();
            Button main = root.Q<Button>("gas-dialog__confirm");
            Assert.That(toggles.Select(t => t.label), Has.Member("GE_Fireball_Cost → GE_Firebolt_Cost"));
            Assert.That(toggles.Select(t => t.value), Is.EqualTo(Enumerable.Repeat(true, 5)));
            Assert.That(main.text, Is.EqualTo("Renomear 6"));
            Assert.That(main.enabledSelf, Is.True);

            _dialog.SetChecked(0, false);
            Assert.That(main.text, Is.EqualTo("Renomear 5"));

            _dialog.SetName("Fireball");
            Assert.That(main.enabledSelf, Is.False);
            Assert.That(root.Q<Label>("gas-dialog__reason").text, Is.EqualTo("Digite um nome diferente do atual."));
            Assert.That(main.tooltip, Is.EqualTo("Digite um nome diferente do atual."));
        }

        [Test]
        public void Dialog_Rename_KeepsUncheckedAfterNameEdit()
        {
            _dialog = ScriptableObject.CreateInstance<GasActionDialog>();
            _dialog.Build(_ability, true);
            VisualElement root = _dialog.rootVisualElement;
            _dialog.SetName("Firebolt");
            int damage = root.Query<Toggle>().ToList().FindIndex(t => t.label == "GE_Fireball_Damage → GE_Firebolt_Damage");
            _dialog.SetChecked(damage, false);

            _dialog.SetName("FireBolt");

            Toggle toggle = root.Query<Toggle>().ToList().Single(t => t.label == "GE_Fireball_Damage → GE_FireBolt_Damage");
            Assert.That(toggle.value, Is.False);
            Assert.That(root.Q<Button>("gas-dialog__confirm").text, Is.EqualTo("Renomear 5"));
        }

        [Test]
        public void Dialog_Duplicate_CopiedAndSharedGroups()
        {
            _dialog = ScriptableObject.CreateInstance<GasActionDialog>();
            _dialog.Build(_ability, false);
            VisualElement root = _dialog.rootVisualElement;

            Assert.That(root.Q<TextField>().value, Is.EqualTo("Fireball_2"));
            List<string> labels = root.Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(labels, Is.SupersetOf(new[]
            {
                "Copiado", "GA_Fireball_2", "GE_Fireball_2_Cost", "GE_Fireball_2_Cooldown", "ActTestAbility.Fireball_2",
                "ActTestCooldown.Fireball_2", "Compartilhado", "GE_Fireball_Damage", "GE_Burn",
            }));
            Assert.That(labels.IndexOf("Compartilhado"), Is.GreaterThan(labels.IndexOf("ActTestCooldown.Fireball_2")));
            Button main = root.Q<Button>("gas-dialog__confirm");
            Assert.That(main.text, Is.EqualTo("Duplicar"));
            Assert.That(main.enabledSelf, Is.True);
        }

        private static GameplayTagDefinition Tag(string name, GameplayTagDefinition parent)
        {
            var tag = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("parent").objectReferenceValue = parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Save(tag, name);
        }

        private static T Save<T>(T asset, string name) where T : Object
        {
            asset.name = name;
            AssetDatabase.CreateAsset(asset, $"{Root}/{name}.asset");
            return asset;
        }
    }
}
