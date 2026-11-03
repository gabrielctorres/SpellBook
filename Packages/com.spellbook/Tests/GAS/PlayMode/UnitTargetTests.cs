using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Unidade-alvo, alcance até a borda e sujeito da <c>TagQueryCondition</c> (B-012, EV8; CA28).</summary>
    public class UnitTargetTests
    {
        private GasFactory _gas;
        private AttributeDefinition _mana;
        private readonly List<GameObject> _units = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _mana = _gas.CreateAttribute("Mana");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject unit in _units)
            {
                if (unit != null) Object.DestroyImmediate(unit);
            }
            _units.Clear();
            Physics.SyncTransforms();
            _gas.Dispose();
        }

        [Test]
        public void UnitTarget_InRangeActivates_OutFailsWithReasonNoCost()
        {
            AbilitySystemComponent caster = _gas.CreateAsc((_mana, 50f));
            AbilitySystemComponent near = CreateUnit(new Vector3(3f, 0f, 0f));
            AbilitySystemComponent far = CreateUnit(new Vector3(8f, 0f, 0f));
            var record = new RecordEventTargetsAction();
            GameplayAbility ability = _gas.CreateAbility(record);
            ability.CostEffect = Cost(10f);
            ability.CastRange = ScalableFloat.Constant(5f);
            ability.ActivationConditions.Add(new ActivationRule(new CastRangeCondition()));
            caster.GiveAbility(ability);
            ActivationFailure reported = default;
            caster.OnAbilityFailed += (_, failure) => reported = failure;

            AbilityActivationResult outOfRange = caster.TryActivateAbility(ability, AbilityTarget.AtUnit(far));
            float manaAfterFailure = caster.GetAttributeValue(_mana);
            AbilityActivationResult inRange = caster.TryActivateAbility(ability, AbilityTarget.AtUnit(near));

            Assert.That(outOfRange, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(reported.ReasonKey, Is.EqualTo("gas.activation.cast_range"), "a falha leva o motivo");
            Assert.That(manaAfterFailure, Is.EqualTo(50f), "fora do alcance não cobra o custo");
            Assert.That(inRange, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(caster.GetAttributeValue(_mana), Is.EqualTo(40f));
            Assert.That(record.Seen, Is.EqualTo(new[] { near }), "a unidade entra nos alvos do evento do OnActivate");
            Assert.That(record.Point, Is.EqualTo(near.transform.position), "sem ponto explícito, o ponto é a posição da unidade");
        }

        [Test]
        public void EdgeMeasure_UsesColliderClosestPoint()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent big = CreateUnit(new Vector3(6f, 0f, 0f), 2f);
            AbilitySystemComponent noCollider = CreateUnit(new Vector3(6f, 0f, 0f), 0f);
            var range = new CastRangeCondition();
            GameplayAbility ability = _gas.CreateAbility();
            ability.CastRange = ScalableFloat.Constant(5f);
            ability.ActivationConditions.Add(new ActivationRule(range));
            caster.GiveAbility(ability);

            AbilityActivationResult center = caster.GetActivationResult(ability, AbilityTarget.AtUnit(big), out _);
            range.Measure = RangeMeasure.Edge;
            AbilityActivationResult edge = caster.GetActivationResult(ability, AbilityTarget.AtUnit(big), out _);
            AbilityActivationResult edgeNoCollider = caster.GetActivationResult(ability, AbilityTarget.AtUnit(noCollider), out _);

            Assert.That(center, Is.EqualTo(AbilityActivationResult.BlockedByCondition), "o centro está a 6 m");
            Assert.That(edge, Is.EqualTo(AbilityActivationResult.Success), "a borda do colisor de raio 2 está a 4 m");
            Assert.That(edgeNoCollider, Is.EqualTo(AbilityActivationResult.BlockedByCondition), "sem colisor, mede até o centro");
        }

        [Test]
        public void PointOverload_StillWorks()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            var record = new RecordEventTargetsAction();
            GameplayAbility ability = _gas.CreateAbility(record);
            ability.CastRange = ScalableFloat.Constant(5f);
            ability.ActivationConditions.Add(new ActivationRule(new CastRangeCondition { Measure = RangeMeasure.Edge }));
            caster.GiveAbility(ability);

            AbilityActivationResult far = caster.TryActivateAbility(ability, new Vector3(8f, 0f, 0f));
            AbilityActivationResult near = caster.TryActivateAbility(ability, new Vector3(3f, 0f, 0f));

            Assert.That(far, Is.EqualTo(AbilityActivationResult.BlockedByCondition));
            Assert.That(near, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(record.Seen, Is.Empty, "com ponto, os alvos do evento ficam vazios");
            Assert.That(record.Point, Is.EqualTo(new Vector3(3f, 0f, 0f)));
            Assert.That(AbilityTarget.AtPoint(Vector3.one).Unit, Is.Null);
            Assert.That(AbilityTarget.AtUnit(null).HasPoint, Is.False, "unidade nula é o alvo vazio");
        }

        [Test]
        public void TagQuery_SubjectOwnerDefault_TargetUnitReadsUnit_NoUnitFalse()
        {
            GameplayTag alive = _gas.CreateTag("State.Alive");
            AbilitySystemComponent owner = _gas.CreateAsc();
            AbilitySystemComponent unit = _gas.CreateAsc();
            unit.AddTag(alive);
            var condition = new TagQueryCondition();
            condition.Query.All.Add(alive);

            bool ownerDefault = condition.Evaluate(Context(owner, AbilityTarget.AtUnit(unit)));
            condition.Subject = ConditionSubject.TargetUnit;
            bool onUnit = condition.Evaluate(Context(owner, AbilityTarget.AtUnit(unit)));
            bool noUnit = condition.Evaluate(Context(owner, AbilityTarget.AtPoint(Vector3.zero)));
            owner.AddTag(alive);
            bool noUnitOwnerTagged = condition.Evaluate(Context(owner, AbilityTarget.None));

            Assert.That(new TagQueryCondition().Subject, Is.EqualTo(ConditionSubject.Owner), "o padrão é o dono");
            Assert.That(ownerDefault, Is.False, "com sujeito Owner, lê as tags do dono, que não tem a tag");
            Assert.That(onUnit, Is.True, "com sujeito TargetUnit, lê as tags da unidade");
            Assert.That(noUnit, Is.False, "sem unidade, é falsa");
            Assert.That(noUnitOwnerTagged, Is.False, "sem unidade, é falsa mesmo com o dono marcado");
        }

        private static ConditionContext Context(AbilitySystemComponent owner, AbilityTarget target)
        {
            return new ConditionContext(owner, null, 1, null, target, null);
        }

        private AbilitySystemComponent CreateUnit(Vector3 position, float colliderRadius = 0.25f)
        {
            AbilitySystemComponent unit = _gas.CreateAsc();
            unit.transform.position = position;
            if (colliderRadius > 0f) unit.gameObject.AddComponent<SphereCollider>().radius = colliderRadius;
            _units.Add(unit.gameObject);
            Physics.SyncTransforms();
            return unit;
        }

        private GameplayEffect Cost(float amount)
        {
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) }
            });
            return cost;
        }

        /// <summary>Guarda os alvos e o ponto do evento da execução.</summary>
        [Serializable]
        private sealed class RecordEventTargetsAction : GameplayAction
        {
            public readonly List<AbilitySystemComponent> Seen = new List<AbilitySystemComponent>();
            public Vector3 Point;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                Seen.Clear();
                Seen.AddRange(context.EventTargets);
                Point = context.Point;
                return ActionStatus.Done;
            }
        }
    }
}
