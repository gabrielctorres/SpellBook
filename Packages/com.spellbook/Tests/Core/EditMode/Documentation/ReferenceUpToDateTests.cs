using System;
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
    /// Guarda da referência commitada: o catálogo, a impressão digital, os YAML da API e o mapa da Unity estão em dia
    /// com o código (ADR 0040, item 4; Spec referencia-catalogo-e-api, RC4 e XR2).
    /// </summary>
    public class ReferenceUpToDateTests
    {
        private const string Instruction = "rode SpellBook > Documentação > Gerar referência e commite o resultado";
        private const string XrefUid = "- uid: ";

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

        [Test]
        public void CommittedXrefMap_MatchesCommittedReference()
        {
            string path = XrefMapPath();
            Assert.That(File.Exists(path), Is.True, $"{path} não existe; {Instruction}");

            string committed = File.ReadAllText(path).Replace("\r\n", "\n");

            Assert.That(committed, Is.EqualTo(UnityXrefMap.Render(ReferenceDir())),
                $"mapa da Unity desatualizado em relação aos YAML ou aos pacotes com.unity.* instalados; {Instruction}");
        }

        [Test]
        public void EveryUnityReference_HasXrefEntry()
        {
            string path = XrefMapPath();
            Assert.That(File.Exists(path), Is.True, $"{path} não existe; {Instruction}");
            var uids = new HashSet<string>(File.ReadLines(path)
                .Where(l => l.StartsWith(XrefUid, StringComparison.Ordinal))
                .Select(l => Unquote(l.Substring(XrefUid.Length))));

            List<string> missing = UnityXrefMap.ReadExternalReferences(ReferenceDir())
                .Where(r => UnityXrefMap.ResolveHref(r) != null && !uids.Contains(r.Uid))
                .Select(r => r.Uid)
                .ToList();

            Assert.That(missing.Take(20), Is.Empty, "referências da Unity sem entrada no mapa:\n" + string.Join("\n", missing.Take(20)));
        }

        /// <summary>Escalar do mapa: o <see cref="UnityXrefMap"/> põe entre aspas simples o que o YAML não aceita sem aspas.</summary>
        private static string Unquote(string value)
        {
            return value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\''
                ? value.Substring(1, value.Length - 2).Replace("''", "'")
                : value;
        }

        private static string XrefMapPath()
        {
            return Path.Combine(Docs(), "api", "unity.xrefmap.yml");
        }

        private static string ReferenceDir()
        {
            return Path.Combine(Docs(), "api", "reference");
        }

        private static string Docs()
        {
            return Path.Combine(PackageFiles.RepositoryRoot, "Documentation~");
        }
    }
}
