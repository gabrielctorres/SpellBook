using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
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

        private static readonly Regex YamlGuid = new Regex(@"\bguid:\s*([0-9a-fA-F]{32})\b");

        // Arquivos serializados do Sample que citam outros assets por guid.
        private static readonly string[] SerializedExtensions = { ".asset", ".prefab", ".unity", ".mat" };

        // Nomes em inglês (emenda de 2026-11-04, CA-EN2). O GUID é o de antes da renomeação; nulo onde o nome não mudou.
        private static readonly string[] DataEnglishFolders = { "Abilities", "Attributes", "Effects", "Tags" };

        private static readonly (string Path, string Guid)[] DataEnglishNames =
        {
            ("Abilities/GA_Fireball.asset", "9d277aace03affb48a3e28d68970e162"),
            ("Attributes/ATR_Health.asset", "9e242287da19ef24897717e50ec580eb"),
            ("Attributes/ATR_Mana.asset", null),
            ("Effects/GE_Burn.asset", "b60a31fea48cb244ca3d131b46987b4e"),
            ("Effects/GE_Fireball_Cooldown.asset", "bb8bfb3f377ca29458a51cbb2bd049ab"),
            ("Effects/GE_Fireball_Cost.asset", "585a1ac1850adaf43994b6466ccd2ce5"),
            ("Effects/GE_Fireball_Damage.asset", "ec6971ff71d4ead4eb319c4a99908b15"),
            ("Tags/TAG_Slot_1.asset", null),
            ("Tags/TAG_Status_Burning.asset", "d746a42321683ae4dbb40510377c858b"),
        };

        // SB-T10: caminhos a partir da pasta do Sample.
        private static readonly string[] EventsPoolingAndMaterialsFolders = { "Data/Events", "Data/Pooling", "Materials" };

        private static readonly (string Path, string Guid)[] EventsPoolingAndMaterialsNames =
        {
            ("Data/Events/CH_BurnTick.asset", "74b910a9c3d0eaf43b27655ddf0ee376"),
            ("Data/Events/CH_CastFailed.asset", "030456ce5d1510941b516001475f7988"),
            ("Data/Events/CH_DamageTaken.asset", "62dea702254525643a50ca6d77aff641"),
            ("Data/Events/CH_FireballHit.asset", "7ddd831673517bc4bbbfc0af2b8faf03"),
            ("Data/Pooling/SP_Numbers.asset", "356af8adce836aa4d9a89d9894c41190"),
            ("Data/Pooling/SP_Projectiles.asset", "dfb5d9ca0f699ca42930bd2360d839c0"),
            ("Materials/M_Caster.mat", "1195b3e1a6b6749479babc7095979549"),
            ("Materials/M_Dummy.mat", "de5e197aae90a1048918e6bee8c7b0bf"),
            ("Materials/M_Fireball.mat", "28b50c120fd8f624baee84ae9357c487"),
            ("Materials/M_Ground.mat", "d1f983affef7eb646a9ad3b9381a05e4"),
        };

        private const string FireballKey = "sandbox.ability.fireball";

        // GUID do provedor de texto do HUD (HUD_Textos; o nome muda no SB-T11, o GUID não).
        private const string TextProviderGuid = "c259d8252affd184da1fb1410c77c626";

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
        public void SampleAssets_ReferenceNoProjectAssets()
        {
            var packageGuids = new HashSet<string>(PackageFiles.Find(string.Empty, ".meta")
                .Select(meta => ReadGuid(Path.Combine(PackageFiles.Root, meta)))
                .Concat(SampleEntries().Where(f => f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Select(ReadGuid))
                .Where(guid => guid != null));
            var violations = new List<string>();

            foreach (string file in SampleEntries().Where(f => SerializedExtensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in YamlGuid.Matches(lines[i]))
                    {
                        string guid = match.Groups[1].Value.ToLowerInvariant();
                        if (packageGuids.Contains(guid))
                        {
                            continue;
                        }

                        // Fora do pacote, só passa o que a Unity resolve fora de Assets/ (pacotes de dependência e recursos embutidos).
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path) || path.StartsWith("Assets/", StringComparison.Ordinal))
                        {
                            string target = string.IsNullOrEmpty(path) ? "asset desconhecido" : path;
                            violations.Add($"{Relative(file)}:{i + 1}: guid {guid} aponta para {target}, fora do pacote e do Sample");
                        }
                    }
                }
            }

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

        [Test]
        public void SampleData_AbilitiesAttributesEffectsAndTags_HaveEnglishNamesAndOriginalGuids()
        {
            AssertEnglishNames(
                DataEnglishFolders.Select(folder => "Data/" + folder),
                DataEnglishNames.Select(n => ("Data/" + n.Path, n.Guid)),
                "arquivos de Data/Abilities, Attributes, Effects e Tags (CA-EN2)");
        }

        [Test]
        public void SampleData_EventsPoolingAndMaterials_HaveEnglishNamesAndOriginalGuids()
        {
            AssertEnglishNames(
                EventsPoolingAndMaterialsFolders,
                EventsPoolingAndMaterialsNames,
                "arquivos de Data/Events, Data/Pooling e Materials (CA-EN2)");
        }

        [Test]
        public void SandboxAbilityName_UsesFireballKeyInBothTables()
        {
            string ability = ReadData("Abilities/GA_Fireball.asset");
            string ptBr = ReadData("UI/TXT_Sandbox.pt-BR.asset");
            string en = ReadData("UI/TXT_Sandbox.en.asset");

            Assert.That(ability, Does.Contain("key: " + FireballKey + "\n    text: Fireball\n"), "chave e texto padrão do GA_Fireball");
            Assert.That(YamlValue(ptBr, "cultureName"), Is.EqualTo("pt-BR"));
            Assert.That(ptBr, Does.Contain("- key: " + FireballKey + "\n    text: Bola de Fogo\n"), "linha da tabela pt-BR");
            Assert.That(YamlValue(en, "m_Name"), Is.EqualTo("TXT_Sandbox.en"));
            Assert.That(YamlValue(en, "cultureName"), Is.EqualTo("en"));
            Assert.That(en, Does.Contain("- key: " + FireballKey + "\n    text: Fireball\n"), "linha da tabela en");
            Assert.That(ability + ptBr + en, Does.Not.Contain("bola_de_fogo"), "a chave antiga ficou");
        }

        [Test]
        public void SandboxTextProvider_ReadsEnglishTableWithEnCulture()
        {
            string provider = File.ReadAllText(SampleFileByGuid(TextProviderGuid));
            string enGuid = ReadGuid(Path.Combine(SampleFolder(), "Data", "UI", "TXT_Sandbox.en.asset.meta"));
            string ptBrGuid = ReadGuid(Path.Combine(SampleFolder(), "Data", "UI", "TXT_Sandbox.pt-BR.asset.meta"));

            Assert.That(YamlValue(provider, "cultureName"), Is.EqualTo("en"), "cultura do provedor (CA-EN6)");
            Assert.That(provider, Does.Contain("guid: " + enGuid + ","), "o provedor não lista a TXT_Sandbox.en");
            Assert.That(provider, Does.Contain("guid: " + ptBrGuid + ","), "o provedor não lista mais a TXT_Sandbox.pt-BR");
        }

        /// <summary>
        /// Os arquivos (sem <c>.meta</c>) das pastas são exatamente os nomes dados, cada <c>m_Name</c> é o nome do
        /// arquivo e o <c>.meta</c> guarda o GUID de antes da renomeação (nulo onde o nome não mudou). Caminhos a partir
        /// da pasta do Sample.
        /// </summary>
        private static void AssertEnglishNames(
            IEnumerable<string> folders, IEnumerable<(string Path, string Guid)> names, string message)
        {
            (string Path, string Guid)[] entries = names.ToArray();
            string[] expected = entries.Select(n => n.Path).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            string[] actual = folders
                .SelectMany(folder => Directory.GetFiles(Path.Combine(SampleFolder(), folder)))
                .Where(file => !file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Select(file => Relative(file).Substring(SamplePath.Length + 1))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            var violations = new List<string>();

            foreach ((string path, string guid) in entries)
            {
                string file = Path.Combine(SampleFolder(), path);
                if (!File.Exists(file))
                {
                    continue;
                }

                string name = Path.GetFileNameWithoutExtension(path);
                if (YamlValue(File.ReadAllText(file), "m_Name") != name)
                {
                    violations.Add($"{path}: m_Name diferente de {name}");
                }

                if (guid != null && ReadGuid(file + ".meta") != guid)
                {
                    violations.Add($"{path}.meta: guid diferente de {guid} (renomeie pela Unity)");
                }
            }

            Assert.That(actual, Is.EqualTo(expected), message);
            Assert.That(violations, Is.Empty);
        }

        /// <summary>Texto de um arquivo de <c>Data/</c> do Sample; falha com o caminho se ele não existe.</summary>
        private static string ReadData(string relativePath)
        {
            string file = Path.Combine(SampleFolder(), "Data", relativePath);
            Assert.That(File.Exists(file), Is.True, $"{SamplePath}/Data/{relativePath} não existe");
            return File.ReadAllText(file).Replace("\r\n", "\n");
        }

        /// <summary>O arquivo do Sample cujo <c>.meta</c> tem o GUID dado.</summary>
        private static string SampleFileByGuid(string guid)
        {
            string meta = SampleEntries().FirstOrDefault(f =>
                f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && ReadGuid(f) == guid);
            Assert.That(meta, Is.Not.Null, $"nenhum arquivo do Sample tem o guid {guid}");
            return meta.Substring(0, meta.Length - ".meta".Length);
        }

        /// <summary>O valor de um campo de primeiro nível do YAML (<c>  campo: valor</c>), ou nulo.</summary>
        private static string YamlValue(string yaml, string field)
        {
            Match match = Regex.Match(yaml, @"^  " + Regex.Escape(field) + @": ?(.*?)\r?$", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : null;
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
