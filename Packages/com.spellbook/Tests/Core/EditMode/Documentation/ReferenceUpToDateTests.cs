using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Guarda da referência commitada: o catálogo, a impressão digital e os YAML da API estão em dia com o código
    /// (ADR 0040, item 4; Spec referencia-catalogo-e-api, RC4).
    /// </summary>
    public class ReferenceUpToDateTests
    {
        private const string Instruction = "rode SpellBook > Documentação > Gerar referência e commite o resultado";

        private static readonly Regex MachinePath = new Regex(@"(?<![A-Za-z])[A-Za-z]:[\\/]|/Users/|\\Users\\");
        private static readonly Regex Branch = new Regex(@"^\s*branch:\s*(\S+)");

        [Test]
        public void CommittedCatalog_MatchesGenerated()
        {
            string path = Path.Combine(Docs(), "manual", "referencia", "catalogo.md");
            Assert.That(File.Exists(path), Is.True, $"{path} não existe; {Instruction}");

            // O git pode trocar \n por \r\n na cópia de trabalho (core.autocrlf); o gerador escreve só \n.
            string committed = File.ReadAllText(path).Replace("\r\n", "\n");

            Assert.That(committed, Is.EqualTo(TypeCatalogPage.Render(TypeCatalog.Build())), $"catálogo desatualizado; {Instruction}");
        }

        [Test]
        public void CommittedFingerprint_MatchesSources()
        {
            string path = Path.Combine(Docs(), "api", "reference.sha256");
            Assert.That(File.Exists(path), Is.True, $"{path} não existe; {Instruction}");

            string committed = File.ReadAllText(path).Trim();

            Assert.That(committed, Is.EqualTo(ReferenceFingerprint.Compute(PackageFiles.RepositoryRoot)),
                $"o código do pacote mudou depois da última geração; {Instruction}");
        }

        [Test]
        public void CommittedReference_HasNoMachinePathOrForeignBranch()
        {
            string folder = Path.Combine(Docs(), "api", "reference");
            Assert.That(Directory.Exists(folder), Is.True, $"{folder} não existe; {Instruction}");
            string[] files = Directory.GetFiles(folder, "*.yml", SearchOption.AllDirectories);
            var problems = new List<string>();

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                foreach (string line in File.ReadLines(file))
                {
                    Match branch = Branch.Match(line);
                    if (MachinePath.IsMatch(line) || (branch.Success && branch.Groups[1].Value != "main"))
                    {
                        problems.Add($"{name}: {line.Trim()}");
                    }
                }
            }

            Assert.That(files, Is.Not.Empty, $"{folder} não tem YAML; {Instruction}");
            Assert.That(problems.Take(20), Is.Empty, "caminho da máquina ou branch diferente de main nos YAML:\n" + string.Join("\n", problems.Take(20)));
        }

        private static string Docs()
        {
            return Path.Combine(PackageFiles.RepositoryRoot, "Documentation~");
        }
    }
}
