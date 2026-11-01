using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Consultas de tag do ASC e as condições do GAS sobre o <see cref="ConditionContext"/> (AV2).</summary>
    public class GasConditionsTests
    {
        private GasFactory _gas;
        private AbilitySystemComponent _asc;
        private GameplayTag _state;
        private GameplayTag _stunned;
        private GameplayTag _fire;
        private GameplayTag _ice;
        private GameplayTag _immune;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _asc = _gas.CreateAsc();
            _state = _gas.CreateTag("State");
            _stunned = _gas.CreateTag("State.Stunned", _state);
            _immune = _gas.CreateTag("State.Immune", _state);
            _fire = _gas.CreateTag("Element.Fire");
            _ice = _gas.CreateTag("Element.Ice");
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void HasMatching_Query_AllAnyNone()
        {
            GameplayTagQuery query = Query(all: new[] { _state }, any: new[] { _fire, _ice }, none: new[] { _immune });
            _asc.AddTag(_stunned);
            _asc.AddTag(_ice);

            bool matchesWithChildAndAny = _asc.HasMatching(query);
            _asc.AddTag(_immune);
            bool matchesWithForbidden = _asc.HasMatching(query);
            _asc.RemoveTag(_immune);
            _asc.RemoveTag(_ice);
            bool matchesWithoutAny = _asc.HasMatching(query);

            Assert.That(matchesWithChildAndAny, Is.True);
            Assert.That(matchesWithForbidden, Is.False);
            Assert.That(matchesWithoutAny, Is.False);
            Assert.That(_asc.HasMatching(new GameplayTagQuery()), Is.True, "consulta vazia casa com qualquer conjunto");
            Assert.That(_asc.HasMatching(null), Is.False, "consulta nula não casa");
        }

        [Test]
        public void HasAllHasAny_Container_InvalidTagFalse()
        {
            _asc.AddTag(_stunned);
            _asc.AddTag(_fire);
            var both = new GameplayTagContainer(_state, _fire);
            var missingOne = new GameplayTagContainer(_fire, _ice);
            var invalid = new[] { default(GameplayTag) };

            Assert.That(_asc.HasAll(both), Is.True, "a tag ativa filha satisfaz o pai");
            Assert.That(_asc.HasAll(missingOne), Is.False);
            Assert.That(_asc.HasAny(missingOne), Is.True);
            Assert.That(_asc.HasAny(new GameplayTagContainer(_ice, _immune)), Is.False);
            Assert.That(_asc.HasAll(invalid), Is.False);
            Assert.That(_asc.HasAny(invalid), Is.False);
            Assert.That(_asc.HasAll(new GameplayTagContainer()), Is.True);
            Assert.That(_asc.HasAny(new GameplayTagContainer()), Is.False);
        }

        [Test]
        public void TagQuery_PassAndFail_UsesAllAnyNone()
        {
            var condition = new TagQueryCondition();
            condition.Query.All.Add(_state);
            condition.Query.Any.Add(_fire);
            condition.Query.None.Add(_immune);
            _asc.AddTag(_stunned);
            _asc.AddTag(_fire);

            bool passes = condition.Evaluate(Context(_asc));
            _asc.AddTag(_immune);
            bool failsOnNone = condition.Evaluate(Context(_asc));
            _asc.RemoveTag(_immune);
            _asc.RemoveTag(_fire);
            bool failsOnAny = condition.Evaluate(Context(_asc));

            Assert.That(passes, Is.True);
            Assert.That(failsOnNone, Is.False);
            Assert.That(failsOnAny, Is.False);
            Assert.That(condition.Evaluate(Context(null)), Is.False, "sem dono não casa");
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.tag_query"));
        }

        [Test]
        public void HasTargets_PassAndFail_IgnoresDestroyed()
        {
            var condition = new HasTargetsCondition();
            AbilitySystemComponent target = _gas.CreateAsc();
            AbilitySystemComponent destroyed = _gas.CreateAsc();
            Object.DestroyImmediate(destroyed.gameObject);

            bool none = condition.Evaluate(Context(_asc));
            bool onlyDestroyed = condition.Evaluate(Context(_asc, destroyed, null));
            bool withTarget = condition.Evaluate(Context(_asc, destroyed, target));

            Assert.That(none, Is.False);
            Assert.That(onlyDestroyed, Is.False);
            Assert.That(withTarget, Is.True);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("gas.activation.has_targets"));
        }

        [Test]
        public void OrTagQueryHasTargets_NotHasTargets_AndRequiresAll()
        {
            TagQueryCondition stunnedQuery = TagQuery(_stunned);
            var or = new OrCondition();
            or.Conditions.Add(stunnedQuery);
            or.Conditions.Add(new HasTargetsCondition());
            var not = new NotCondition { Condition = new HasTargetsCondition() };
            var and = new AndCondition();
            and.Conditions.Add(TagQuery(_fire));
            and.Conditions.Add(not);
            AbilitySystemComponent target = _gas.CreateAsc();

            bool orNeither = or.Evaluate(Context(_asc));
            bool orByTarget = or.Evaluate(Context(_asc, target));
            _asc.AddTag(_stunned);
            bool orByTag = or.Evaluate(Context(_asc));
            bool notWithTarget = not.Evaluate(Context(_asc, target));
            bool notWithoutTarget = not.Evaluate(Context(_asc));
            bool andMissingFire = and.Evaluate(Context(_asc));
            _asc.AddTag(_fire);
            bool andAll = and.Evaluate(Context(_asc));
            bool andNotFails = and.Evaluate(Context(_asc, target));

            Assert.That(orNeither, Is.False);
            Assert.That(orByTarget, Is.True);
            Assert.That(orByTag, Is.True);
            Assert.That(notWithTarget, Is.False);
            Assert.That(notWithoutTarget, Is.True);
            Assert.That(andMissingFire, Is.False);
            Assert.That(andAll, Is.True);
            Assert.That(andNotFails, Is.False);
            Assert.That(new AndCondition(), Is.InstanceOf<AndConditionBase<ConditionContext>>());
        }

        [Test]
        public void Conditions_DoNotChangeAsc()
        {
            _asc.AddTag(_stunned);
            _asc.AddTag(_stunned);
            _asc.AddTag(_fire);
            var changes = new List<GameplayTag>();
            _asc.OnTagChanged += (tag, added) => changes.Add(tag);
            var or = new OrCondition();
            or.Conditions.Add(TagQuery(_stunned));
            or.Conditions.Add(new HasTargetsCondition());
            var condition = new AndCondition();
            condition.Conditions.Add(or);
            condition.Conditions.Add(new NotCondition { Condition = TagQuery(_immune) });
            AbilitySystemComponent target = _gas.CreateAsc();
            int tagsBefore = _asc.TagCounts.Count;
            int effectsBefore = _asc.ActiveEffects.Count;
            int abilitiesBefore = _asc.Abilities.Count;

            bool result = condition.Evaluate(Context(_asc, target));

            Assert.That(result, Is.True);
            Assert.That(changes, Is.Empty);
            Assert.That(_asc.TagCounts.Count, Is.EqualTo(tagsBefore));
            Assert.That(_asc.GetTagCount(_stunned), Is.EqualTo(2));
            Assert.That(_asc.GetTagCount(_fire), Is.EqualTo(1));
            Assert.That(_asc.ActiveEffects.Count, Is.EqualTo(effectsBefore));
            Assert.That(_asc.Abilities.Count, Is.EqualTo(abilitiesBefore));
            Assert.That(target.TagCounts.Count, Is.Zero);
        }

        [Test]
        public void HasMatching_DoesNotAllocate()
        {
            GameplayTagQuery query = Query(all: new[] { _state }, any: new[] { _fire, _ice }, none: new[] { _immune });
            var container = new GameplayTagContainer(_state, _fire);
            TagQueryCondition condition = TagQuery(_stunned);
            _asc.AddTag(_stunned);
            _asc.AddTag(_fire);
            ConditionContext context = Context(_asc);
            int hits = 0;
            if (_asc.HasMatching(query) && _asc.HasAll(container) && _asc.HasAny(container) && condition.Evaluate(context)) hits++;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (_asc.HasMatching(query)) hits++;
                    if (_asc.HasAll(container)) hits++;
                    if (_asc.HasAny(container)) hits++;
                    if (condition.Evaluate(context)) hits++;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(hits, Is.EqualTo(40001));
        }

        private static GameplayTagQuery Query(GameplayTag[] all = null, GameplayTag[] any = null, GameplayTag[] none = null)
        {
            var query = new GameplayTagQuery();
            foreach (GameplayTag tag in all ?? new GameplayTag[0]) query.All.Add(tag);
            foreach (GameplayTag tag in any ?? new GameplayTag[0]) query.Any.Add(tag);
            foreach (GameplayTag tag in none ?? new GameplayTag[0]) query.None.Add(tag);
            return query;
        }

        private static TagQueryCondition TagQuery(GameplayTag required)
        {
            var condition = new TagQueryCondition();
            condition.Query.All.Add(required);
            return condition;
        }

        private static ConditionContext Context(AbilitySystemComponent owner, params AbilitySystemComponent[] targets)
        {
            return new ConditionContext(owner, null, 1, null, AbilityTarget.None, targets);
        }
    }
}
