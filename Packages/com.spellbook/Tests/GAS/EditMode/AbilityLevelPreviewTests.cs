using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Prévia por nível da aba Níveis e prévia (IH3; CA4, CA8; DS do Inspector da habilidade, seções 5 e 7).</summary>
    public class AbilityLevelPreviewTests
    {
        private GasFactory _gas;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                SessionState.EraseString(InspectorTabsView.SessionKey(created));
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void LevelTable_OneRowPerLevel_NamedValuesAndCooldown()
        {
            GameplayAbility ability = _gas.CreateAbilityWithCooldown(0f);
            ability.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f));
            ability.Values.Add("cooldown", ScalableFloat.FromArray(8f, 7f, 6.5f));
            SetMaxLevel(ability, 3);

            VisualElement root = CreateInspector(ability);

            Assert.That(Header(root), Is.EqualTo(new[] { "Nível", "damage", "cooldown", "Recarga" }),
                "sem custo e sem cargas, as colunas não aparecem");
            Assert.That(Rows(root).Select(Cells), Is.EqualTo(new[]
            {
                new[] { "1 ◂", "50", "8", "8 s" },
                new[] { "2", "75", "7", "7 s" },
                new[] { "3", "100", "6" + Decimal() + "5", "6" + Decimal() + "5 s" }
            }));
        }

        [Test]
        public void LevelTable_CostAndCharges_WhenPresent()
        {
            GameplayAbility ability = _gas.CreateAbility();
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AttributeDefinition stamina = _gas.CreateAttribute("Stamina");
            ability.CostEffect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            ability.CostEffect.Modifiers.Add(Modifier(mana, ScalableFloat.FromArray(-20f, -25f)));
            var fromAttribute = Modifier(stamina, ScalableFloat.Constant(0f));
            fromAttribute.ValueSource.Type = ModifierValueSource.SourceType.Attribute;
            ability.CostEffect.Modifiers.Add(fromAttribute);
            ability.Values.Add("charges", ScalableFloat.FromArray(2f, 3f));
            ability.Charges.Enabled = true;
            ability.Charges.MaxCharges.Value = ScalableFloat.FromTable("charges");
            ability.Charges.RechargeTime = ScalableFloat.FromArray(8f, 7f);
            SetMaxLevel(ability, 2);

            VisualElement root = CreateInspector(ability);

            Assert.That(Header(root), Is.EqualTo(new[] { "Nível", "charges", "Custo", "Cargas" }),
                "sem efeito de recarga, a coluna de recarga não aparece");
            Assert.That(Rows(root).Select(Cells), Is.EqualTo(new[]
            {
                new[] { "1 ◂", "2", "Mana 20, Stamina attribute", "2 (8 s)" },
                new[] { "2", "3", "Mana 25, Stamina attribute", "3 (7 s)" }
            }));
        }

        [Test]
        public void PreviewLevel_MarksRow_DescriptionUsesSameLevel()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f, 100f));
            SetMaxLevel(ability, 3);
            var editor = (GameplayAbilityEditor)CreateEditor(ability);
            VisualElement root = editor.CreateInspectorGUI();

            editor.SetPreviewLevel(2);

            Assert.That(Rows(root).Select(r => r.ClassListContains(GameplayAbilityEditor.LevelRowSelectedClass)),
                Is.EqualTo(new[] { false, true, false }));
            Assert.That(Rows(root).Select(r => Cells(r)[0]), Is.EqualTo(new[] { "1", "2 ◂", "3" }),
                "o marcador não depende só de cor");
            Assert.That(root.Q<DescriptionPreviewElement>().Level, Is.EqualTo(2));
        }

        [Test]
        public void Edit_RebuildsTable_WithoutPlay()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f));
            SetMaxLevel(ability, 2);
            var editor = (GameplayAbilityEditor)CreateEditor(ability);
            VisualElement root = editor.CreateInspectorGUI();

            editor.serializedObject.FindProperty("maxLevel").intValue = 3;
            editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            ability.Values.Set("damage", ScalableFloat.FromArray(10f, 20f, 30f));
            editor.SetPreviewLevel(3);

            Assert.That(Rows(root).Select(Cells), Is.EqualTo(new[]
            {
                new[] { "1", "10" }, new[] { "2", "20" }, new[] { "3 ◂", "30" }
            }));
        }

        [Test]
        public void LevelValues_OnlyInLevelsTab()
        {
            GameplayAbility ability = _gas.CreateAbility();
            ability.Values.Add("damage", ScalableFloat.FromArray(50f, 75f));
            SetMaxLevel(ability, 2);

            VisualElement root = CreateInspector(ability);

            string[] perLevel =
            {
                GameplayAbilityEditor.LevelTableName, GameplayAbilityEditor.PreviewLevelName, GameplayAbilityEditor.PreviewName,
                GameplayAbilityEditor.ChargesPreviewName, GameplayAbilityEditor.DescriptionSectionName
            };
            foreach (Tab tab in TopTabs(root))
            {
                bool levels = tab.label == "Níveis e prévia";
                foreach (string name in perLevel)
                {
                    Assert.That(tab.Q(name) != null, Is.EqualTo(levels), $"{name} na aba {tab.label}");
                }
            }
        }

        [Test]
        public void MultiSelection_TableUsesFirstAsset()
        {
            GameplayAbility first = _gas.CreateAbility();
            first.Values.Add("damage", ScalableFloat.FromArray(50f, 75f));
            SetMaxLevel(first, 2);
            GameplayAbility second = _gas.CreateAbility();
            second.Values.Add("heal", ScalableFloat.Constant(5f));
            var editor = UnityEditor.Editor.CreateEditor(new Object[] { first, second });
            _created.Add(editor);

            VisualElement root = editor.CreateInspectorGUI();

            Assert.That(Header(root), Is.EqualTo(new[] { "Nível", "damage" }));
            Assert.That(Rows(root).Count, Is.EqualTo(2));
        }

        private UnityEditor.Editor CreateEditor(GameplayAbility ability)
        {
            var editor = UnityEditor.Editor.CreateEditor(ability);
            _created.Add(editor);
            return editor;
        }

        private VisualElement CreateInspector(GameplayAbility ability)
        {
            return CreateEditor(ability).CreateInspectorGUI();
        }

        private static void SetMaxLevel(GameplayAbility ability, int level)
        {
            using (var serialized = new SerializedObject(ability))
            {
                serialized.FindProperty("maxLevel").intValue = level;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ScalableFloat value)
        {
            var modifier = new AttributeEffectModifier { Attribute = attribute, Operation = ModifierOperation.Add };
            modifier.ValueSource.Value = value;
            return modifier;
        }

        private static string Decimal()
        {
            return System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        }

        private static VisualElement Table(VisualElement root)
        {
            VisualElement table = root.Q(GameplayAbilityEditor.LevelTableName);
            Assert.That(table, Is.Not.Null, "falta a tabela por nível");
            return table;
        }

        private static string[] Header(VisualElement root)
        {
            return Cells(Table(root).Q(className: GameplayAbilityEditor.LevelHeaderClass));
        }

        private static List<VisualElement> Rows(VisualElement root)
        {
            return Table(root).Query(className: GameplayAbilityEditor.LevelRowClass).ToList();
        }

        private static string[] Cells(VisualElement row)
        {
            return row.Children().OfType<Label>().Select(l => l.text).ToArray();
        }

        private static List<Tab> TopTabs(VisualElement root)
        {
            return root.Q<InspectorTabsView>().Query<Tab>()
                .Where(t => t.GetFirstAncestorOfType<ActionEventsView>() == null).ToList();
        }
    }
}
