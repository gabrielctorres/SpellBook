#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Leitura de atributos e tags sem aviso (B-003, EA1: CA23 e a parte de <c>TryGetValue</c> do CA27).</summary>
    public class AttributeReadTests
    {
        private GasFactory _gas;
        private GameObject _manual;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manual != null) Object.Destroy(_manual);
            _gas.Dispose();
        }

        [Test]
        public void TryGetValue_Present_TrueWithValue()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;

            bool found = set.TryGetValue(health, out float value);

            Assert.That(found, Is.True);
            Assert.That(value, Is.EqualTo(40f));
        }

        [Test]
        public void TryGetValue_Missing_FalseNoWarning()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;

            bool found = set.TryGetValue(mana, out float value);
            bool foundNull = set.TryGetValue(null, out float nullValue);

            Assert.That(found, Is.False);
            Assert.That(value, Is.EqualTo(0f));
            Assert.That(foundNull, Is.False);
            Assert.That(nullValue, Is.EqualTo(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetValueOrDefault_Missing_ReturnsFallback()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;

            Assert.That(set.GetValueOrDefault(mana, 7f), Is.EqualTo(7f));
            Assert.That(set.GetValueOrDefault(mana), Is.EqualTo(0f));
            Assert.That(set.GetValueOrDefault(null, 3f), Is.EqualTo(3f));
            Assert.That(set.GetValueOrDefault(health, 7f), Is.EqualTo(40f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void HasAttribute_PresentAndMissing()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;

            Assert.That(set.HasAttribute(health), Is.True);
            Assert.That(set.HasAttribute(mana), Is.False);
            Assert.That(set.HasAttribute(null), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetBaseValue_PresentAndMissing()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;
            set.AddModifier(health, new AttributeModifier(10f, ModifierOperation.Add));

            Assert.That(set.GetBaseValue(health), Is.EqualTo(40f));
            Assert.That(set.GetValueOrDefault(health), Is.EqualTo(50f), "o modificador não entrou no valor atual");
            Assert.That(set.GetBaseValue(mana), Is.EqualTo(0f));
            Assert.That(set.GetBaseValue(null), Is.EqualTo(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AscShortcuts_MatchAttributeSet()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AbilitySystemComponent asc = _gas.CreateAsc((health, 40f));
            asc.AttributeSet.AddModifier(health, new AttributeModifier(10f, ModifierOperation.Add));

            Assert.That(asc.TryGetAttributeValue(health, out float value), Is.True);
            Assert.That(value, Is.EqualTo(50f));
            Assert.That(asc.TryGetAttributeValue(mana, out float missing), Is.False);
            Assert.That(missing, Is.EqualTo(0f));
            Assert.That(asc.GetAttributeValueOrDefault(mana, 7f), Is.EqualTo(7f));
            Assert.That(asc.GetAttributeValueOrDefault(health, 7f), Is.EqualTo(50f));
            Assert.That(asc.HasAttribute(health), Is.True);
            Assert.That(asc.HasAttribute(mana), Is.False);
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(40f));
            Assert.That(asc.GetAttributeBaseValue(mana), Is.EqualTo(0f));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void States_ListsStartingAttributesInOrder()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeDefinition speed = CreateAttribute("Speed");
            AttributeSet set = _gas.CreateAsc((mana, 5f), (health, 40f), (speed, 3f)).AttributeSet;

            Assert.That(set.States.Count, Is.EqualTo(3));
            Assert.That(set.States[0].Definition, Is.SameAs(mana));
            Assert.That(set.States[1].Definition, Is.SameAs(health));
            Assert.That(set.States[2].Definition, Is.SameAs(speed));
            Assert.That(set.TryGetState(health, out AttributeState healthState), Is.True);
            Assert.That(set.States[1], Is.SameAs(healthState));
        }

        [Test]
        public void States_BeforeAwakeOrder_IsInitialized()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = CreateInactiveSet((health, 40f), (mana, 5f));

            // Um componente que lê o AttributeSet antes do Awake dele (ordem de componentes) já vê os estados
            int countBeforeAwake = set.States.Count;
            AttributeState healthBeforeAwake = set.States[0];
            _manual.SetActive(true);

            Assert.That(countBeforeAwake, Is.EqualTo(2));
            Assert.That(healthBeforeAwake.CurrentValue, Is.EqualTo(40f));
            Assert.That(set.States.Count, Is.EqualTo(2), "o Awake criou os estados de novo");
            Assert.That(set.States[0], Is.SameAs(healthBeforeAwake));
        }

        [Test]
        public void GetTagCount_CountsReferences()
        {
            GameplayTag state = _gas.CreateTag("State");
            GameplayTag stunned = _gas.CreateTag("State.Stunned", state);
            AbilitySystemComponent asc = _gas.CreateAsc();

            asc.AddTag(stunned);
            asc.AddTag(stunned);
            asc.AddTag(stunned);
            asc.RemoveTag(stunned);

            Assert.That(asc.GetTagCount(stunned), Is.EqualTo(2));
            Assert.That(asc.GetTagCount(state), Is.EqualTo(0), "a contagem é exata, sem hierarquia");
            Assert.That(asc.TagCounts.Count, Is.EqualTo(1));
            Assert.That(asc.TagCounts[stunned], Is.EqualTo(2));
        }

        [Test]
        public void GetTagCount_InvalidTag_Zero()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            asc.AddTag(_gas.CreateTag("State.Stunned"));

            Assert.That(asc.GetTagCount(default), Is.EqualTo(0));
        }

        [Test]
        public void TryGetValue_DoesNotAllocate_10000Times()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AttributeDefinition mana = CreateAttribute("Mana");
            AttributeSet set = _gas.CreateAsc((health, 40f)).AttributeSet;
            float sum = 0f;
            set.TryGetValue(health, out float warmup);
            set.TryGetValue(mana, out warmup);
            sum += warmup;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    set.TryGetValue(health, out float present);
                    set.TryGetValue(mana, out float missing);
                    sum += present + missing;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sum, Is.EqualTo(400000f));
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        /// <summary>AttributeSet num objeto inativo, sem ASC, com os atributos dados e o Awake ainda não chamado.</summary>
        private AttributeSet CreateInactiveSet(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            _manual = new GameObject("AttributeSet inativo");
            _manual.SetActive(false);
            var set = _manual.AddComponent<AttributeSet>();
            using (var so = new SerializedObject(set))
            {
                SerializedProperty list = so.FindProperty("startingAttributes");
                list.arraySize = attributes.Length;
                for (int i = 0; i < attributes.Length; i++)
                {
                    SerializedProperty item = list.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.Definition)).objectReferenceValue =
                        attributes[i].Definition;
                    item.FindPropertyRelative(nameof(AttributeSet.InitialAttribute.InitialValue)).floatValue =
                        attributes[i].InitialValue;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return set;
        }
    }
}
#endif
