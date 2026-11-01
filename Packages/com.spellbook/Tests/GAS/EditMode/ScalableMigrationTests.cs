using System.IO;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Migração dos assets 2.x para os campos <see cref="ScalableFloat"/> (B-007, VN2a; ADR 0009) e entradas nulas
    /// nas listas de modificadores, agora que <see cref="AttributeEffectModifier"/> é classe.
    /// </summary>
    public class ScalableMigrationTests
    {
        private const string TempFolder = "Assets/__ScalableMigrationTests";

        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void LegacyYaml_Duration4_LoadsAsConstant4()
        {
            GameplayEffect effect = LoadLegacyEffect("GE_Legacy", "  DurationPolicy: 2\n  Duration: 4\n  Period: 0\n");

            Assert.That(effect.Duration.Mode, Is.EqualTo(ScalableFloat.ScalableMode.Constant));
            Assert.That(effect.Duration.Evaluate(1), Is.EqualTo(4f));
            Assert.That(effect.Duration.Evaluate(5), Is.EqualTo(4f));
            Assert.That(new SerializedObject(effect).FindProperty("legacyDuration").floatValue, Is.NaN);
        }

        [Test]
        public void NewEffect_SavesWithoutLegacyDuration()
        {
            var effect = ScriptableObject.CreateInstance<GameplayEffect>();
            effect.DurationPolicy = EffectDurationPolicy.HasDuration;
            effect.Duration = ScalableFloat.FromArray(3f, 2f);
            string path = TempFolder + "/GE_New.asset";
            EnsureTempFolder();

            AssetDatabase.CreateAsset(effect, path);
            AssetDatabase.SaveAssets();
            string yaml = File.ReadAllText(path);

            Assert.That(new SerializedObject(effect).FindProperty("legacyDuration").floatValue, Is.NaN);
            Assert.That(yaml, Does.Contain("legacyDuration: NaN"));
            Assert.That(yaml, Does.Not.Contain("\n  Duration:"));
        }

        [Test]
        public void NullModifierEntry_Ignored()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            health.MinValue = 0f;
            health.MaxValue = 1000f;
            AbilitySystemComponent asc = CreateAscWithAttributes(health, 100f);
            GameplayEffect instant = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            instant.Modifiers.Add(null);
            instant.Modifiers.Add(Add(health, 10f));
            GameplayEffect overTime = _gas.CreateDurationEffect(5f);
            overTime.Period = 1f;
            overTime.Modifiers.Add(null);
            overTime.Modifiers.Add(Add(health, 5f));
            overTime.PeriodicModifiers.Add(null);
            overTime.PeriodicModifiers.Add(Add(health, -1f));
            GameplayEffect cost = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            cost.Modifiers.Add(null);
            cost.Modifiers.Add(Add(health, -5f));

            asc.ApplyEffect(instant);
            float afterInstant = asc.GetAttributeValue(health);
            ActiveEffectInstance instance = asc.ApplyEffect(overTime);
            float afterApply = asc.GetAttributeValue(health);
            asc.Tick(1f);
            float afterTick = asc.GetAttributeValue(health);
            asc.RemoveEffect(instance);
            float afterRemove = asc.GetAttributeValue(health);
            bool canAfford = asc.CanAffordCost(cost);

            Assert.That(afterInstant, Is.EqualTo(110f));
            Assert.That(afterApply, Is.EqualTo(115f));
            Assert.That(afterTick, Is.EqualTo(114f));
            Assert.That(afterRemove, Is.EqualTo(109f));
            Assert.That(canAfford, Is.True);
        }

        // ASC em EditMode: sem Awake, então o AttributeSet é ligado à mão (ele se inicializa na primeira leitura).
        private AbilitySystemComponent CreateAscWithAttributes(AttributeDefinition attribute, float initialValue)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((attribute, initialValue));
            GasFactory.SetAutoTick(asc, false);
            GasFactory.SetObjectField(asc, "attributeSet", asc.GetComponent<AttributeSet>());
            return asc;
        }

        private static AttributeEffectModifier Add(AttributeDefinition attribute, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Constant, ConstantValue = value }
            };
        }

        /// <summary>
        /// Escreve um <see cref="GameplayEffect"/> no formato YAML 2.x com os campos dados em <paramref name="fields"/>
        /// (linhas com dois espaços de recuo), importa e devolve o asset carregado.
        /// </summary>
        internal static GameplayEffect LoadLegacyEffect(string name, string fields)
        {
            EnsureTempFolder();
            var probe = ScriptableObject.CreateInstance<GameplayEffect>();
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(probe)));
            Object.DestroyImmediate(probe);

            string path = $"{TempFolder}/{name}.asset";
            string yaml =
                "%YAML 1.1\n" +
                "%TAG !u! tag:unity3d.com,2011:\n" +
                "--- !u!114 &11400000\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 0}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                $"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n" +
                $"  m_Name: {name}\n" +
                "  m_EditorClassIdentifier: \n" +
                fields;
            File.WriteAllText(path, yaml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameplayEffect>(path);
        }

        private static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
        }
    }
}
