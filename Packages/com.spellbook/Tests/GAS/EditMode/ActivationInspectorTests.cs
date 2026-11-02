using System;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Conditions;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Conditions;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Condição própria de teste: só existe para o seletor a oferecer ao lado das condições do GAS (CA4).</summary>
    [Serializable]
    public sealed class InspectorTestCustomCondition : Condition<ConditionContext>
    {
        public override bool Evaluate(in ConditionContext context) => true;
    }

    /// <summary>Inspector das regras de ativação e das cargas (B-009, AV9: CA4 no seletor, R22).</summary>
    public class ActivationInspectorTests
    {
        private GasFactory _gas;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
            }

            _gas.Dispose();
        }

        [Test]
        public void Picker_ListsGasConditionsAndCustom_NotCoreBases()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule());
            Type fieldType;
            using (var serialized = new SerializedObject(ability))
            {
                SerializedProperty condition = serialized.FindProperty("activationConditions.Array.data[0].condition");
                fieldType = TypePickerCatalog.ResolveFieldBaseType(condition);
            }

            Type[] offered = TypePickerCatalog.GetCandidates(fieldType).Select(e => e.Type).ToArray();

            Assert.That(offered, Is.SupersetOf(new[]
            {
                typeof(AndCondition), typeof(OrCondition), typeof(NotCondition), typeof(TagQueryCondition),
                typeof(HasTargetsCondition), typeof(AttributeThresholdCondition), typeof(HasTargetPointCondition),
                typeof(CastRangeCondition), typeof(InspectorTestCustomCondition)
            }));
            Assert.That(offered.Where(t => t.IsAbstract || t.IsGenericType), Is.Empty, "as bases do Core não entram no seletor");
        }

        [Test]
        public void RuleRow_ShowsSummaryAndReason()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            GameplayTag lowMana = _gas.CreateTag("Fail.LowMana");
            var threshold = new AttributeThresholdCondition
            {
                Attribute = mana,
                ComparisonType = AttributeThresholdCondition.Comparison.GreaterOrEqual
            };
            threshold.Value.Value = ScalableFloat.Constant(20f);
            GameplayAbility ability = _gas.CreateAbility();
            ability.ActivationConditions.Add(new ActivationRule(threshold, lowMana));
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetPointCondition()));
            ability.ActivationConditions.Add(new ActivationRule(new HasTargetsCondition(), default, "ui.no_targets"));

            VisualElement root = CreateInspector(ability);

            Label[] rows = root.Q(GameplayAbilityEditor.ActivationName).Query<Label>(className: GameplayAbilityEditor.RuleRowClass).ToList().ToArray();
            Assert.That(rows.Length, Is.EqualTo(3));
            Assert.That(rows[0].text, Is.EqualTo("1. Mana Greater Or Equal 20 → motivo: Fail.LowMana"));
            Assert.That(rows[1].text, Is.EqualTo("2. Tem ponto-alvo → motivo: gas.activation.no_target"));
            Assert.That(rows[2].text, Is.EqualTo("3. Tem alvos → motivo: ui.no_targets"));
        }

        [Test]
        public void ChargesPreview_UsesPreviewLevel()
        {
            GameplayAbility dash = _gas.CreateAbility();
            dash.Values.Add("charges", ScalableFloat.FromArray(2f, 3f, 4f));
            dash.Charges.Enabled = true;
            dash.Charges.MaxCharges.Value = ScalableFloat.FromTable("charges");
            dash.Charges.RechargeTime = ScalableFloat.FromArray(8f, 7f, 6f);
            using (var serialized = new SerializedObject(dash))
            {
                serialized.FindProperty("maxLevel").intValue = 3;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            VisualElement root = CreateInspector(dash);
            var preview = root.Q<Label>(GameplayAbilityEditor.ChargesPreviewName);
            Assert.That(preview.text, Is.EqualTo("1 carga a cada 8 s, máximo 2 no nível 1"));

            ((GameplayAbilityEditor)_editor).SetPreviewLevel(3);

            Assert.That(preview.text, Is.EqualTo("1 carga a cada 6 s, máximo 4 no nível 3"));
            Assert.That(preview.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ChargesPreview_HiddenWhenDisabled()
        {
            GameplayAbility ability = _gas.CreateAbility();

            VisualElement root = CreateInspector(ability);

            Assert.That(root.Q<Label>(GameplayAbilityEditor.ChargesPreviewName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private VisualElement CreateInspector(GameplayAbility ability)
        {
            _editor = UnityEditor.Editor.CreateEditor(ability);
            return _editor.CreateInspectorGUI();
        }
    }
}
