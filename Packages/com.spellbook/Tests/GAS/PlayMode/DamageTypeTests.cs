#if UNITY_EDITOR
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>Tags de dano, resistência e imunidade (B-010, DM2: CA11 a CA13 e a parte de runtime do CA14).</summary>
    public class DamageTypeTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private GameplayTag _damage;
        private GameplayTag _fire;
        private GameplayTag _poison;
        private GameplayTag _magic;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _damage = _gas.CreateTag("Dano");
            _fire = _gas.CreateTag("Dano.Fogo", _damage);
            _poison = _gas.CreateTag("Dano.Veneno", _damage);
            _magic = _gas.CreateTag("Dano.Magico", _damage);
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Fire50pct_Fire100Reduces50_Poison100Reduces100()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition fireResistance = CreateAttribute("ResistenciaFogo");
            SetProcessors(health, Percent(fireResistance, _fire));
            AbilitySystemComponent asc = CreateManualAsc((health, 300f), (fireResistance, 0.5f));

            asc.ApplyEffect(Damage(health, 100f, _fire));
            float afterFire = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(Damage(health, 100f, _poison));

            Assert.That(afterFire, Is.EqualTo(250f), "fogo reduzido pela metade");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(150f), "veneno sem redução");
        }

        [Test]
        public void FireAndMagic_ParentDamageMatchesBoth()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition resistance = CreateAttribute("Resistencia");
            AttributeDefinition magicResistance = CreateAttribute("ResistenciaMagica");
            SetProcessors(health, Percent(resistance, _damage), Percent(magicResistance, _magic));
            AbilitySystemComponent asc = CreateManualAsc((health, 300f), (resistance, 0.5f), (magicResistance, 0.5f));

            asc.ApplyEffect(Damage(health, 100f, _fire, _magic));
            float afterBoth = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(Damage(health, 100f, _fire));

            Assert.That(afterBoth, Is.EqualTo(275f), "Dano casa com Fogo e Magico: 100 x 0,5 x 0,5");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(225f), "só Fogo: só a resistência a Dano");
        }

        [Test]
        public void Immune_AnnulsOnlyDamage_EffectStillApplied()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition mana = CreateAttribute("Mana");
            SetProcessors(health, new ImmuneToTags { DamageTags = { _fire } });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (mana, 100f));
            int healthChanges = 0;
            asc.AttributeSet.TryGetState(health, out AttributeState state);
            state.OnValueChanged += (_, _) => healthChanges++;
            GameplayTag burning = _gas.CreateTag("Estado.Queimando");
            GameplayEffect burn = _gas.CreateDurationEffect(5f, burning);
            burn.DamageTags.Add(_fire);
            burn.Modifiers.Add(Modifier(mana, ModifierOperation.Add, -20f));
            burn.Period = 1f;
            burn.PeriodicModifiers.Add(Modifier(health, ModifierOperation.Add, -10f));

            asc.ApplyEffect(burn);
            asc.Tick(1f);
            asc.ApplyEffect(Damage(health, 30f, _poison));

            Assert.That(healthChanges, Is.EqualTo(1), "só o veneno muda a vida");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(70f), "tique de fogo anulado, veneno aplicado");
            Assert.That(asc.GetAttributeValue(mana), Is.EqualTo(80f), "modificador persistente do efeito vale");
            Assert.That(asc.HasTag(burning), Is.True, "tag concedida pelo efeito");
        }

        [Test]
        public void InvalidDamageTag_NoException()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition fireResistance = CreateAttribute("ResistenciaFogo");
            SetProcessors(health, Percent(fireResistance, _fire), new ImmuneToTags { DamageTags = { _poison } });
            AbilitySystemComponent asc = CreateManualAsc((health, 300f), (fireResistance, 0.5f));
            GameplayEffect onlyInvalid = Damage(health, 100f);
            AddEmptyDamageTagRow(onlyInvalid);
            GameplayEffect invalidAndFire = Damage(health, 100f, _fire);
            AddEmptyDamageTagRow(invalidAndFire);

            Assert.DoesNotThrow(() => asc.ApplyEffect(onlyInvalid), "só a tag vazia");
            float afterInvalid = asc.GetAttributeBaseValue(health);
            Assert.DoesNotThrow(() => asc.ApplyEffect(invalidAndFire), "tag vazia e Fogo");

            Assert.That(onlyInvalid.DamageTags.Count, Is.EqualTo(1), "a linha vazia continua no asset");
            Assert.That(afterInvalid, Is.EqualTo(200f), "tag vazia não casa com nada");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(150f), "Fogo continua casando");
        }

        [Test]
        public void Flat_DoesNotInvertSign()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition armor = CreateAttribute("Armadura");
            SetProcessors(health, new ReduceByAttribute { SourceAttribute = armor, Mode = ReductionMode.Flat });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (armor, 15f));

            float small = asc.ApplyAttributeChange(health, -10f);
            float large = asc.ApplyAttributeChange(health, -40f);
            float heal = asc.ApplyAttributeChange(health, 20f);

            Assert.That(small, Is.EqualTo(0f), "dano menor que a armadura vira zero, não cura");
            Assert.That(large, Is.EqualTo(-25f), "reduz até o valor da armadura");
            Assert.That(heal, Is.EqualTo(20f), "Only Negative: cura passa inteira");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(95f), "base final");
        }

        private static ReduceByAttribute Percent(AttributeDefinition source, GameplayTag tag)
        {
            var reduce = new ReduceByAttribute { SourceAttribute = source, Mode = ReductionMode.Percent };
            reduce.DamageTags.Add(tag);
            return reduce;
        }

        private GameplayEffect Damage(AttributeDefinition attribute, float amount, params GameplayTag[] damageTags)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Modifier(attribute, ModifierOperation.Add, -amount));
            foreach (GameplayTag tag in damageTags) effect.DamageTags.Add(tag);
            return effect;
        }

        private static void AddEmptyDamageTagRow(GameplayEffect effect)
        {
            using (var so = new SerializedObject(effect))
            {
                SerializedProperty tags = so.FindProperty("damageTags.tags");
                tags.InsertArrayElementAtIndex(0);
                tags.GetArrayElementAtIndex(0).FindPropertyRelative("definition").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation operation, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = operation,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) },
            };
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }
    }
}
#endif
