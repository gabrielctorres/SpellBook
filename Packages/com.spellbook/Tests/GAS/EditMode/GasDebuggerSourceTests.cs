using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// O GAS Debugger lê o ASC só pela API pública: nenhum fonte da janela (<c>Editor/GAS/Debugger</c>) nem do modelo
    /// (<c>Runtime/GAS/Diagnostics</c>) usa reflexão (CA26).
    /// </summary>
    public class GasDebuggerSourceTests
    {
        private static readonly string[] Folders =
        {
            Path.Combine("Editor", "GAS", "Debugger"),
            Path.Combine("Runtime", "GAS", "Diagnostics"),
        };

        private static readonly string[] Forbidden = { "System.Reflection", "BindingFlags", "GetField(" };

        [Test]
        public void GasDebuggerSources_UseNoReflection()
        {
            string root = PackageRoot();
            var violations = new List<string>();
            int scanned = 0;

            foreach (string folder in Folders)
            {
                string path = Path.Combine(root, folder);
                Assert.That(Directory.Exists(path), Is.True, $"pasta não encontrada: {path}");

                foreach (string file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        foreach (string token in Forbidden)
                        {
                            if (lines[i].IndexOf(token, StringComparison.Ordinal) >= 0)
                            {
                                string relative = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
                                violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                            }
                        }
                    }
                }
            }

            Assert.That(scanned, Is.GreaterThanOrEqualTo(2), "a varredura deveria achar a janela e o modelo");
            Assert.That(violations, Is.Empty);
        }

        /// <summary>Caminho real do pacote (risco 22); os testes rodam no host com o pacote em <c>file:</c>.</summary>
        private static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(GasDebuggerSourceTests).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException(
                    "O assembly de teste não pertence a um pacote; rode os testes no host com o pacote em file: e testables.");
            }

            return info.resolvedPath;
        }
    }
}
