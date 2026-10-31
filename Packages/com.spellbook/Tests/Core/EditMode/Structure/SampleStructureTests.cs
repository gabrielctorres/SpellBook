using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Guardas de estrutura do Sample Sandbox (Spec <c>sample-sandbox.md</c>, T1; ADR 0028). Leem os arquivos do
    /// <c>Samples~</c> sem abrir a cena nem compilar o Sample, que fica fora da suíte.
    /// </summary>
    public class SampleStructureTests
    {
        private const string SamplePath = "Samples~/Sandbox";
        private const string AsmdefPath = SamplePath + "/Scripts/SpellBook.Samples.Sandbox.asmdef";

        private static readonly string[] AllowedReferences =
        {
            "SpellBook.Core", "SpellBook.Time", "SpellBook.Events", "SpellBook.Tags", "SpellBook.GAS",
        };

        // Montado por partes: o NoOdinTests também lê este arquivo.
        private static readonly string[] ForbiddenSourceTerms =
        {
            "UnityEngine.InputSystem", "UnityEngine.UI", "TMPro", "UnityEditor", "Sire" + "nix",
        };

        private static readonly Regex MetaGuid = new Regex(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);

        [Test]
        public void Package_DeclaresSandboxSample()
        {
            PackageManifest manifest = JsonUtility.FromJson<PackageManifest>(PackageFiles.ReadText("package.json"));

            List<SampleEntry> sandbox = manifest.samples.Where(s => s.displayName == "Sandbox").ToList();

            Assert.That(sandbox, Has.Count.EqualTo(1), "o package.json deve declarar um Sample \"Sandbox\"");
            Assert.That(sandbox[0].path, Is.EqualTo(SamplePath));
            Assert.That(string.IsNullOrWhiteSpace(sandbox[0].description), Is.False, "o Sample Sandbox não tem description");
            Assert.That(Directory.Exists(SampleFolder()), Is.True, $"a pasta {SamplePath} não existe");
        }

        [Test]
        public void SampleAsmdef_ReferencesOnlyAllowedModules()
        {
            string fullPath = Path.Combine(PackageFiles.Root, AsmdefPath);
            Assert.That(File.Exists(fullPath), Is.True, $"a asmdef do Sample não existe em {AsmdefPath}");

            var asmdef = new AsmdefFile(AsmdefPath, JsonUtility.FromJson<AsmdefData>(File.ReadAllText(fullPath)));

            Assert.That(asmdef.Data.name, Is.EqualTo("SpellBook.Samples.Sandbox"));
            Assert.That(asmdef.ReferenceNames, Is.SubsetOf(AllowedReferences), "referência fora de Core, Time, Events, Tags e GAS");
            Assert.That(asmdef.ReferenceNames.Where(r => r.EndsWith(".Editor", StringComparison.Ordinal)), Is.Empty);
            Assert.That(asmdef.Data.overrideReferences, Is.True, "overrideReferences não é true");
            Assert.That(asmdef.Data.precompiledReferences, Is.Empty, "a asmdef do Sample tem precompiledReferences");
            Assert.That(asmdef.Data.autoReferenced, Is.True, "autoReferenced não é true");
            Assert.That(asmdef.Data.includePlatforms, Is.Empty, "includePlatforms não está vazio");
        }

        [Test]
        public void Package_HasNoUntildedSamplesFolder()
        {
            string root = PackageFiles.Root;

            Assert.That(Directory.Exists(Path.Combine(root, "Samples")), Is.False,
                "a pasta Samples/ (sem til) existe; renomeie de volta para Samples~ (ADR 0028)");
            Assert.That(File.Exists(Path.Combine(root, "Samples.meta")), Is.False,
                "Samples.meta existe na raiz do pacote; apague-o (ADR 0028)");
        }

        [Test]
        public void SampleSources_UseNoInputSystemUiOrEditor()
        {
            var violations = new List<string>();

            foreach (string file in SampleEntries().Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                string text = File.ReadAllText(file);
                foreach (string term in ForbiddenSourceTerms.Where(t => text.Contains(t)))
                {
                    violations.Add($"{Relative(file)}: cita {term}");
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void SampleFiles_EveryAssetHasMetaAndUniqueGuid()
        {
            List<string> entries = SampleEntries().Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToList();
            var violations = new List<string>();
            var sampleGuids = new Dictionary<string, string>();

            foreach (string entry in entries)
            {
                string meta = entry + ".meta";
                if (!File.Exists(meta))
                {
                    violations.Add($"{Relative(entry)}: sem .meta");
                    continue;
                }

                string guid = ReadGuid(meta);
                if (guid == null)
                {
                    violations.Add($"{Relative(meta)}: sem guid");
                }
                else if (sampleGuids.TryGetValue(guid, out string other))
                {
                    violations.Add($"{Relative(meta)}: guid {guid} repetido em {other}");
                }
                else
                {
                    sampleGuids.Add(guid, Relative(meta));
                }
            }

            foreach (string meta in PackageFiles.Find(string.Empty, ".meta"))
            {
                string guid = ReadGuid(Path.Combine(PackageFiles.Root, meta));
                if (guid != null && sampleGuids.TryGetValue(guid, out string sampleMeta))
                {
                    violations.Add($"{sampleMeta}: guid {guid} colide com {meta}");
                }
            }

            Assert.That(entries, Is.Not.Empty, $"a varredura não achou nenhum arquivo em {SamplePath}");
            Assert.That(violations, Is.Empty);
        }

        private static string SampleFolder()
        {
            return Path.Combine(PackageFiles.Root, SamplePath);
        }

        /// <summary>Arquivos e subpastas do Sample, com caminho completo.</summary>
        private static IEnumerable<string> SampleEntries()
        {
            string folder = SampleFolder();
            if (!Directory.Exists(folder))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Concat(Directory.GetDirectories(folder, "*", SearchOption.AllDirectories))
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        private static string ReadGuid(string metaPath)
        {
            Match match = MetaGuid.Match(File.ReadAllText(metaPath));
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        private static string Relative(string fullPath)
        {
            return fullPath.Substring(PackageFiles.Root.Length).Replace('\\', '/').TrimStart('/');
        }

        /// <summary>Campos do <c>package.json</c> lidos aqui (nomes iguais aos do JSON).</summary>
        [Serializable]
        private sealed class PackageManifest
        {
            public SampleEntry[] samples = Array.Empty<SampleEntry>();
        }

        [Serializable]
        private sealed class SampleEntry
        {
            public string displayName = string.Empty;
            public string description = string.Empty;
            public string path = string.Empty;
        }
    }
}
