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

        private const string SamplesPage = "Documentation~/manual/exemplos/sandbox.md";

        // Termos proibidos no corpo de Update, LateUpdate e FixedUpdate (convenções Unity do CLAUDE.md).
        private static readonly string[] ForbiddenPerFrameTerms = { "GetComponent", "Find", "new List", "Linq" };

        private static readonly Regex MonoBehaviourDeclaration =
            new Regex(@"\bclass\s+(?<name>\w+)\s*:\s*(UnityEngine\.)?MonoBehaviour\b");

        private static readonly Regex SamplesHelpUrl =
            new Regex(@"\[\s*HelpURL\s*\(\s*DocUrls\.Samples\s*\+\s*""#(?<anchor>[\w-]+)""\s*\)\s*\]");

        private static readonly Regex PerFrameMethod = new Regex(@"\bvoid\s+(?<name>Update|LateUpdate|FixedUpdate)\s*\(\s*\)");

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

        [Test]
        public void SampleBehaviours_HaveHelpUrlToSamplesPage()
        {
            string page = File.ReadAllText(Path.Combine(PackageFiles.RepositoryRoot, SamplesPage));
            var behaviours = new List<string>();
            var violations = new List<string>();

            foreach (string file in SampleSources())
            {
                string text = File.ReadAllText(file);
                foreach (Match declaration in MonoBehaviourDeclaration.Matches(text))
                {
                    string name = declaration.Groups["name"].Value;
                    behaviours.Add(name);

                    Match help = SamplesHelpUrl.Match(AttributesBefore(text, declaration.Index));
                    if (!help.Success)
                    {
                        violations.Add($"{Relative(file)}: {name} sem [HelpURL(DocUrls.Samples + \"#...\")]");
                    }
                    else if (!page.Contains("{#" + help.Groups["anchor"].Value + "}"))
                    {
                        violations.Add($"{Relative(file)}: âncora {{#{help.Groups["anchor"].Value}}} não existe em {SamplesPage}");
                    }
                }
            }

            Assert.That(behaviours, Is.Not.Empty, $"nenhum \": MonoBehaviour\" nos .cs de {SamplePath}");
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void SampleSources_HaveNoFindOrGetComponentInUpdate()
        {
            var violations = new List<string>();

            foreach (string file in SampleSources())
            {
                string text = File.ReadAllText(file);
                foreach (Match method in PerFrameMethod.Matches(text))
                {
                    string[] lines = MethodBody(text, method.Index + method.Length).Split('\n');
                    foreach (string line in lines)
                    {
                        foreach (string term in ForbiddenPerFrameTerms.Where(t => line.Contains(t)))
                        {
                            violations.Add($"{Relative(file)}: {method.Groups["name"].Value} usa {term}: {line.Trim()}");
                        }
                    }
                }
            }

            Assert.That(violations, Is.Empty);
        }

        /// <summary>Os atributos logo antes da declaração: o trecho desde o último <c>;</c>, <c>{</c> ou <c>}</c>.</summary>
        private static string AttributesBefore(string text, int declarationIndex)
        {
            int start = text.LastIndexOfAny(new[] { ';', '{', '}' }, declarationIndex) + 1;
            return text.Substring(start, declarationIndex - start);
        }

        /// <summary>O corpo entre a primeira <c>{</c> depois de <paramref name="from"/> e a <c>}</c> que a fecha.</summary>
        private static string MethodBody(string text, int from)
        {
            int open = text.IndexOf('{', from);
            if (open < 0)
            {
                return string.Empty;
            }

            int depth = 0;
            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    depth++;
                }
                else if (text[i] == '}' && --depth == 0)
                {
                    return text.Substring(open + 1, i - open - 1);
                }
            }

            return text.Substring(open + 1);
        }

        private static IEnumerable<string> SampleSources()
        {
            return SampleEntries().Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
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
