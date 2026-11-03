#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// <c>ConditionalAction</c> avalia uma <c>Condition&lt;ConditionContext&gt;</c> sobre a execução em curso, e o enum
    /// <c>ConditionType</c> sai (B-012, EV7; CA20, R18).
    /// </summary>
    public class ConditionalActionTests
    {
        private static readonly object Owner = new object();

        private GasFactory _gas;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        // Ou(E(fogo, tem alvos), Não(atordoado))
        [TestCase(true, true, true, "then")]
        [TestCase(true, false, true, "else")]
        [TestCase(false, true, true, "else")]
        [TestCase(false, false, false, "then")]
        public void Conditional_AndOrNot_ThenElse(bool fire, bool withTarget, bool stunned, string expected)
        {
            GameplayTag fireTag = _gas.CreateTag("Element.Fire");
            GameplayTag stunnedTag = _gas.CreateTag("State.Stunned");
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent target = _gas.CreateAsc();
            if (fire) caster.AddTag(fireTag);
            if (stunned) caster.AddTag(stunnedTag);
            var and = new AndCondition();
            and.Conditions.Add(TagQuery(fireTag));
            and.Conditions.Add(new HasTargetsCondition());
            var or = new OrCondition();
            or.Conditions.Add(and);
            or.Conditions.Add(new NotCondition { Condition = TagQuery(stunnedTag) });
            AbilitySystemComponent[] targets = withTarget ? new[] { target } : Array.Empty<AbilitySystemComponent>();

            caster.RunActions(new GameplayAction[] { Conditional(or) },
                GasFactory.CreateActionContext(caster, Vector3.zero, targets), Owner);

            Assert.That(_log, Is.EqualTo(new[] { expected }),
                $"fogo {fire}, alvo {withTarget}, atordoado {stunned}");
        }

        [Test]
        public void EnumGone()
        {
            string runtime = Path.Combine(PackageRoot(), "Runtime");
            var pattern = new Regex(@"\bConditionType\b");
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                if (pattern.IsMatch(File.ReadAllText(file))) violations.Add(file);
            }

            Assert.That(typeof(ConditionalAction).GetNestedType("ConditionType"), Is.Null);
            Assert.That(violations, Is.Empty, "o enum ConditionType ainda aparece no Runtime");
        }

        [Test]
        public void Conditional_ThresholdUsesExecutionLevel()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            var threshold = new AttributeThresholdCondition
            {
                Attribute = mana,
                ComparisonType = AttributeThresholdCondition.Comparison.GreaterOrEqual
            };
            threshold.Value.Type = ModifierValueSource.SourceType.Value;
            threshold.Value.Value = ScalableFloat.FromTable("minMana");
            GameplayAbility ability = _gas.CreateAbility(Conditional(threshold));
            ability.Values.Add("minMana", ScalableFloat.FromArray(10f, 40f));
            SetMaxLevel(ability, 2);
            AbilitySystemComponent caster = _gas.CreateAsc((mana, 30f));
            caster.GiveAbility(ability, 1);

            caster.TryActivateAbility(ability, Vector3.zero);
            caster.SetAbilityLevel(ability, 2);
            caster.TryActivateAbility(ability, Vector3.zero);

            Assert.That(_log, Is.EqualTo(new[] { "then", "else" }), "nível 1 exige 10 de Mana e nível 2 exige 40");
        }

        [Test]
        public void Conditional_ContextCarriesExecution()
        {
            AbilitySystemComponent caster = _gas.CreateAsc();
            AbilitySystemComponent target = _gas.CreateAsc();
            var probe = new ProbeCondition();

            caster.RunActions(new GameplayAction[] { Conditional(probe) },
                GasFactory.CreateActionContext(caster, new Vector3(1f, 2f, 3f), target), Owner);

            Assert.That(probe.Owner, Is.SameAs(caster));
            Assert.That(probe.ActionInstigator, Is.SameAs(caster), "Action não é o contexto da execução");
            Assert.That(probe.Targets, Is.EqualTo(new[] { target }));
            Assert.That(probe.Point, Is.EqualTo((true, new Vector3(1f, 2f, 3f))));
        }

        private ConditionalAction Conditional(Condition<ConditionContext> condition)
        {
            return new ConditionalAction
            {
                Condition = condition,
                Then = new List<GameplayAction> { new RecordAction("then", _log) },
                Else = new List<GameplayAction> { new RecordAction("else", _log) },
            };
        }

        private static TagQueryCondition TagQuery(GameplayTag required)
        {
            var condition = new TagQueryCondition();
            condition.Query.All.Add(required);
            return condition;
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(ConditionalActionTests).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
            }

            return info.resolvedPath;
        }

        [Serializable]
        private sealed class ProbeCondition : Condition<ConditionContext>
        {
            public AbilitySystemComponent Owner;
            public AbilitySystemComponent ActionInstigator;
            public AbilitySystemComponent[] Targets;
            public (bool, Vector3) Point;

            // Copia o que leu: a sonda só existe no teste e pode alocar
            public override bool Evaluate(in ConditionContext context)
            {
                Owner = context.Owner;
                ActionInstigator = context.Action != null ? context.Action.Instigator : null;
                Targets = new List<AbilitySystemComponent>(context.Targets).ToArray();
                Point = (context.Target.HasPoint, context.Target.Point);
                return true;
            }
        }

        [Serializable]
        private sealed class RecordAction : GameplayAction
        {
            private readonly string _entry;
            private readonly List<string> _log;

            public RecordAction(string entry, List<string> log)
            {
                _entry = entry;
                _log = log;
            }

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                _log.Add(_entry);
                return ActionStatus.Done;
            }
        }
    }
}
#endif
