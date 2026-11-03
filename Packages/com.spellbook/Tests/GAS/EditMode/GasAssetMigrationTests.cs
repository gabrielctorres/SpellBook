using System.IO;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Menu <c>SpellBook &gt; Migrar assets do GAS (3.0)</c> (VN3, EV3d): regrava os <see cref="GameplayEffect"/> e os
    /// <see cref="GameplayAbility"/> 2.x no formato novo, sem os campos legados, e rodar de novo não muda nada.
    /// </summary>
    public class GasAssetMigrationTests
    {
        private const string MenuPath = "SpellBook/Migrar assets do GAS (3.0)";
        private const string TempFolder = "Assets/__ScalableMigrationTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [Test]
        public void Migrate_RewritesLegacyAsset_Idempotent()
        {
            GameplayEffect effect = ScalableMigrationTests.LoadLegacyEffect("GE_Migrate",
                "  DurationPolicy: 1\n" +
                "  Duration: 4\n" +
                "  Period: 0\n" +
                "  Modifiers:\n" +
                "  - Attribute: {fileID: 0}\n" +
                "    Operation: 0\n" +
                "    ValueSource:\n" +
                "      Type: 0\n" +
                "      ConstantValue: -7.5\n" +
                "      Attribute: {fileID: 0}\n" +
                "      Formula: {fileID: 0}\n");
            string path = AssetDatabase.GetAssetPath(effect);

            bool ran = EditorApplication.ExecuteMenuItem(MenuPath);
            string firstYaml = File.ReadAllText(path);
            EditorApplication.ExecuteMenuItem(MenuPath);
            string secondYaml = File.ReadAllText(path);

            Assert.That(ran, Is.True, "o menu de migração não existe");
            Assert.That(firstYaml, Does.Not.Contain("\n  Duration:"));
            Assert.That(firstYaml, Does.Not.Contain("ConstantValue:"));
            Assert.That(firstYaml, Does.Contain("legacyDuration: NaN"));
            Assert.That(firstYaml, Does.Contain("legacyConstant: NaN"));
            Assert.That(firstYaml, Does.Contain("constant: 4\n"));
            Assert.That(firstYaml, Does.Contain("constant: -7.5\n"));
            Assert.That(effect.Duration.Evaluate(1), Is.EqualTo(4f));
            Assert.That(effect.Modifiers[0].ValueSource.Value.Evaluate(1), Is.EqualTo(-7.5f));
            Assert.That(effect.Modifiers[0].ValueSource.Value.Mode, Is.EqualTo(ScalableFloat.ScalableMode.Constant));
            Assert.That(secondYaml, Is.EqualTo(firstYaml));
        }

        [Test]
        public void Migrate_RewritesLegacyWaitAction_Idempotent()
        {
            GameplayAbility ability = LoadLegacyAbilityWithWait("GA_MigrateWait", 1.5f);
            string path = AssetDatabase.GetAssetPath(ability);
            var wait = (WaitAction)ability.OnActivate[0];
            float loadedWait = wait.Duration.Evaluate(1);
            ScalableFloat.ScalableMode loadedMode = wait.Duration.Mode;

            EditorApplication.ExecuteMenuItem(MenuPath);
            string firstYaml = File.ReadAllText(path);
            EditorApplication.ExecuteMenuItem(MenuPath);
            string secondYaml = File.ReadAllText(path);

            Assert.That(loadedWait, Is.EqualTo(1.5f), "o callback não converteu a espera guardada por SerializeReference");
            Assert.That(loadedMode, Is.EqualTo(ScalableFloat.ScalableMode.Constant));
            Assert.That(firstYaml, Does.Contain("OnActivate:"));
            Assert.That(firstYaml, Does.Contain("legacyDuration: NaN"));
            Assert.That(firstYaml, Does.Contain("constant: 1.5\n"));
            Assert.That(firstYaml, Does.Not.Contain(" Duration:"));
            Assert.That(secondYaml, Is.EqualTo(firstYaml));
        }

        /// <summary>
        /// Grava um <see cref="GameplayAbility"/> no YAML 2.x (lista no campo antigo <c>Actions</c>, com uma
        /// <see cref="WaitAction"/> de <c>Duration</c> em <c>float</c>) e importa o asset.
        /// </summary>
        private static GameplayAbility LoadLegacyAbilityWithWait(string name, float seconds)
        {
            if (!AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.CreateFolder("Assets", TempFolder.Substring("Assets/".Length));
            var probe = ScriptableObject.CreateInstance<GameplayAbility>();
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
                "  Actions:\n" +
                "  - rid: 1000\n" +
                "  references:\n" +
                "    version: 2\n" +
                "    RefIds:\n" +
                "    - rid: 1000\n" +
                "      type: {class: WaitAction, ns: SpellBook.GAS.Abilities.Actions, asm: SpellBook.GAS}\n" +
                "      data:\n" +
                $"        Duration: {seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n";
            File.WriteAllText(path, yaml);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameplayAbility>(path);
        }
    }
}
