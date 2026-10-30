using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// O GAS conta tempo só pelo ASC ou por uma fonte de tempo: nenhum fonte de <c>Runtime/GAS</c> lê o
    /// <c>Time</c> da Unity nem espera com <c>WaitForSeconds</c> (CA10; Spec 4.4).
    /// </summary>
    public class GasSourceScanTests
    {
        // Time. sem qualificação (não casa SpellBook.Time.), UnityEngine.Time e WaitForSeconds(Realtime).
        private static readonly Regex Forbidden = new Regex(@"(?<![\w.])Time\.|\bUnityEngine\.Time\b|\bWaitForSeconds");

        [Test]
        public void GasSources_DoNotReadUnityTimeOrWaitForSeconds()
        {
            string root = PackageRoot();
            string gasFolder = Path.Combine(root, "Runtime", "GAS");
            Assert.That(Directory.Exists(gasFolder), Is.True, $"pasta não encontrada: {gasFolder}");
            var violations = new List<string>();

            foreach (string file in Directory.GetFiles(gasFolder, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (Forbidden.IsMatch(lines[i]))
                    {
                        string relative = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
                        violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            Assert.That(violations, Is.Empty);
        }

        /// <summary>Caminho real do pacote (risco 22); os testes rodam no host com o pacote em <c>file:</c>.</summary>
        private static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(GasSourceScanTests).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
            }

            return info.resolvedPath;
        }
    }
}
