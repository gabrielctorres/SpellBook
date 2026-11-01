using System.IO;
using NUnit.Framework;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Menu <c>SpellBook &gt; Migrar assets do GAS (3.0)</c> (VN3): regrava os <see cref="GameplayEffect"/> 2.x no
    /// formato novo, sem os campos legados, e rodar de novo não muda nada.
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
    }
}
