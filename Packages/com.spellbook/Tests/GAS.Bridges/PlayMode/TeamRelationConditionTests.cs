#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Teams;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>Busca do membro do ASC e <see cref="TeamRelationCondition"/> (Teams, TM5; CA11, CA12, CA14 e CA16).</summary>
    public class TeamRelationConditionTests
    {
        private const string RelationKey = "teams.condition.relation";

        private GasFactory _gas;
        private TeamsFactory _teams;
        private TeamDefinition _red;
        private TeamDefinition _blue;
        private TeamRelationMatrix _matrix;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _teams = new TeamsFactory();
            _red = _teams.CreateTeam("Red");
            _blue = _teams.CreateTeam("Blue");
            _matrix = _teams.CreateMatrix(TeamRelation.Neutral, _red, _blue);
            _teams.SetCell(_matrix, _red, _blue, TeamRelation.Hostile);
            _teams.SetCell(_matrix, _blue, _red, TeamRelation.Hostile);
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _teams.Dispose();
        }

        [Test]
        public void Member_OnSameObjectAndParent_Found_NoneIsNoTeam()
        {
            AbilitySystemComponent onSelf = Unit(_red);
            TeamMember self = onSelf.GetComponent<TeamMember>();
            TeamMember parent = _teams.CreateMember(_blue, _matrix, "Parent");
            AbilitySystemComponent child = _gas.CreateAsc();
            child.transform.SetParent(parent.transform);
            AbilitySystemComponent alone = _gas.CreateAsc();

            Assert.That(AscTeams.GetMember(onSelf), Is.SameAs(self), "membro no próprio objeto do ASC");
            Assert.That(AscTeams.GetMember(child), Is.SameAs(parent), "membro num pai do ASC");
            Assert.That(AscTeams.GetMember(alone), Is.Null, "sem membro na hierarquia");
            Assert.That(AscTeams.GetMember(null), Is.Null, "sem ASC");
        }

        [Test]
        public void Condition_PassesAndFailsByMask_ReasonKey()
        {
            AbilitySystemComponent owner = Unit(_red);
            AbilitySystemComponent enemy = Unit(_blue);
            AbilitySystemComponent ally = Unit(_red);
            var condition = new TeamRelationCondition();
            GameplayAbility ability = Grant(owner, condition);

            AbilityActivationResult onEnemy = owner.GetActivationResult(ability, AbilityTarget.AtUnit(enemy), out _);
            AbilityActivationResult onAlly = owner.GetActivationResult(ability, AbilityTarget.AtUnit(ally),
                out ActivationFailure allyFailure);
            condition.Mask = TeamRelationMask.Allied | TeamRelationMask.Self;
            AbilityActivationResult alliesOnSelf = owner.GetActivationResult(ability, AbilityTarget.AtUnit(owner), out _);
            AbilityActivationResult alliesOnAlly = owner.GetActivationResult(ability, AbilityTarget.AtUnit(ally), out _);
            AbilityActivationResult alliesOnEnemy = owner.GetActivationResult(ability, AbilityTarget.AtUnit(enemy), out _);

            Assert.That(condition.DefaultFailureKey, Is.EqualTo(RelationKey));
            Assert.That(onEnemy, Is.EqualTo(AbilityActivationResult.Success), "Hostil passa no inimigo");
            Assert.That(onAlly, Is.EqualTo(AbilityActivationResult.BlockedByCondition), "Hostil falha no aliado");
            Assert.That(allyFailure.ReasonKey, Is.EqualTo(RelationKey));
            Assert.That(alliesOnSelf, Is.EqualTo(AbilityActivationResult.Success), "Self casa o próprio dono");
            Assert.That(alliesOnAlly, Is.EqualTo(AbilityActivationResult.Success), "Aliado casa o mesmo time");
            Assert.That(alliesOnEnemy, Is.EqualTo(AbilityActivationResult.BlockedByCondition), "Aliado falha no inimigo");
        }

        [Test]
        public void Condition_NoTargetUnit_False_NoException_RelationKey()
        {
            AbilitySystemComponent owner = Unit(_red);
            var condition = new TeamRelationCondition { Mask = ~TeamRelationMask.None };
            GameplayAbility ability = Grant(owner, condition);
            bool withoutTarget = true;
            bool onPoint = true;

            Assert.That(() => withoutTarget = condition.Evaluate(Context(owner, AbilityTarget.None)), Throws.Nothing);
            Assert.That(() => onPoint = condition.Evaluate(Context(owner, AbilityTarget.AtPoint(Vector3.one))), Throws.Nothing);
            owner.GetActivationResult(ability, AbilityTarget.AtPoint(Vector3.one), out ActivationFailure failure);

            Assert.That(withoutTarget, Is.False, "sem alvo, mesmo com todas as relações na máscara");
            Assert.That(onPoint, Is.False, "alvo só de ponto, sem unidade");
            Assert.That(failure.Result, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(failure.ReasonKey, Is.EqualTo(RelationKey), "a única chave da condição");
        }

        [Test]
        public void Rules_HasTargetPointBeforeTeam_NoTargetFailsWithNoTargetKey()
        {
            AbilitySystemComponent owner = Unit(_red);
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            ability.ActivationConditions.Add(new ActivationRule(new TeamRelationCondition()));
            owner.GiveAbility(ability);

            owner.GetActivationResult(ability, AbilityTarget.None, out ActivationFailure noTarget);
            owner.GetActivationResult(ability, AbilityTarget.AtPoint(Vector3.one), out ActivationFailure pointOnly);

            Assert.That(noTarget.ReasonKey, Is.EqualTo("gas.activation.no_target"), "sem alvo, a primeira regra decide");
            Assert.That(noTarget.ConditionIndex, Is.EqualTo(0));
            Assert.That(pointOnly.ReasonKey, Is.EqualTo(RelationKey), "só ponto passa a primeira e falha a de time");
            Assert.That(pointOnly.ConditionIndex, Is.EqualTo(1));
        }

        [Test]
        public void NoTeamOnBothSides_UsesNoTeamRelation()
        {
            _teams.SetNoTeamRelation(_matrix, TeamRelation.Hostile);
            AbilitySystemComponent owner = Unit(null);
            AbilitySystemComponent target = Unit(null);
            AbilitySystemComponent bareOwner = _gas.CreateAsc();
            AbilitySystemComponent bareTarget = _gas.CreateAsc();
            var hostile = new TeamRelationCondition { Mask = TeamRelationMask.Hostile };
            var neutral = new TeamRelationCondition { Mask = TeamRelationMask.Neutral };

            bool hostileWithMatrix = hostile.Evaluate(Context(owner, AbilityTarget.AtUnit(target)));
            bool neutralWithMatrix = neutral.Evaluate(Context(owner, AbilityTarget.AtUnit(target)));
            bool neutralWithoutMembers = neutral.Evaluate(Context(bareOwner, AbilityTarget.AtUnit(bareTarget)));

            Assert.That(hostileWithMatrix, Is.True, "sem time nos dois lados vale a relação de sem time da matriz");
            Assert.That(neutralWithMatrix, Is.False);
            Assert.That(neutralWithoutMembers, Is.True, "sem membro e sem matriz, Neutro");
        }

        [Test]
        public void Validate_EmptyMask_Warns()
        {
            var empty = new TeamRelationCondition { Mask = TeamRelationMask.None };
            var hostile = new TeamRelationCondition();
            var emptyResult = new ValidationResult(null);
            var hostileResult = new ValidationResult(null);

            empty.Validate(emptyResult, "rules.condition");
            hostile.Validate(hostileResult, "rules.condition");

            Assert.That(emptyResult.Messages.Select(m => (m.Severity, m.PropertyPath)),
                Is.EqualTo(new[] { (ValidationSeverity.Warning, "rules.condition.mask") }));
            Assert.That(hostileResult.Messages, Is.Empty);
        }

        [Test]
        public void Lookup_10000_Condition_10000_DoNotAllocate()
        {
            TeamMember root = _teams.CreateMember(_red, _matrix, "Root");
            var middle = new GameObject("Middle"); // destruído com o Root, que é da factory
            middle.transform.SetParent(root.transform);
            AbilitySystemComponent owner = _gas.CreateAsc();
            owner.transform.SetParent(middle.transform);
            AbilitySystemComponent enemy = Unit(_blue);
            AbilitySystemComponent alone = _gas.CreateAsc();
            var condition = new TeamRelationCondition();
            ConditionContext onEnemy = Context(owner, AbilityTarget.AtUnit(enemy));
            ConditionContext onAlone = Context(owner, AbilityTarget.AtUnit(alone));
            int passed = 0;
            AscTeams.GetMember(owner);
            AscTeams.GetMember(alone);
            condition.Evaluate(onEnemy);
            condition.Evaluate(onAlone);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (AscTeams.GetMember(owner) != null) passed++;
                    if (AscTeams.GetMember(alone) == null) passed++;
                }
            }, Is.Not.AllocatingGCMemory(), "busca do membro, achando e sem achar");
            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (condition.Evaluate(onEnemy)) passed++;
                    if (!condition.Evaluate(onAlone)) passed++;
                }
            }, Is.Not.AllocatingGCMemory(), "condição");
            Assert.That(passed, Is.EqualTo(40000), "a busca e a condição deram o resultado esperado em toda volta");
        }

        private AbilitySystemComponent Unit(TeamDefinition team)
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            _teams.AddMember(asc.gameObject, team, _matrix);
            return asc;
        }

        private GameplayAbility Grant(AbilitySystemComponent owner, TeamRelationCondition condition)
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(condition));
            owner.GiveAbility(ability);
            return ability;
        }

        private static ConditionContext Context(AbilitySystemComponent owner, AbilityTarget target)
        {
            return new ConditionContext(owner, null, 1, null, target, null);
        }
    }
}
#endif
