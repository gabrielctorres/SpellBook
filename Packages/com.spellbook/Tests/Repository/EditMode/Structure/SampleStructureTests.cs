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

        // SB-T11: arquivos de Data/UI.
        private static readonly string[] UiFolders = { "Data/UI" };

        private static readonly (string Path, string Guid)[] UiNames =
        {
            ("Data/UI/ANC_Dummy.asset", "0471630398ccfdd449757eda6094bea1"),
            ("Data/UI/ANC_HUD.asset", null),
            ("Data/UI/ANC_Player.asset", "2e6c5468436271643b1832b95d9b062f"),
            ("Data/UI/HUD_Damage.asset", "9baef9d8b04cd6e4cbc02cbfa4b330b1"),
            ("Data/UI/HUD_Heal.asset", "fa624a36b76e8e24db221b12e8fa09a9"),
            ("Data/UI/HUD_Keys.asset", "d78bfc631c6abcc44a0955ba7fbca1c7"),
            ("Data/UI/HUD_Loadout.asset", null),
            ("Data/UI/HUD_Rules.asset", "68dd9aada83891341888632fd11a879d"),
            ("Data/UI/HUD_Texts.asset", "c259d8252affd184da1fb1410c77c626"),
            ("Data/UI/HUD_Theme.asset", "09cbb528a963303408c552031896562b"),
            ("Data/UI/TXT_Sandbox.en.asset", null),
            ("Data/UI/TXT_Sandbox.pt-BR.asset", null),
        };

        // SB-T11: nomes de GameObject do HUD.prefab (CA-EN1), com os das instâncias aninhadas.
        private const string HudPrefabPath = "Prefabs/HUD.prefab";

        private static readonly string[] HudGameObjectNames =
        {
            "Abilities", "Dummy", "HUD", "Health", "Health", "Mana", "Numbers", "Player",
        };

        // Leem o YAML com quebras normalizadas para \n.
        private static readonly Regex GameObjectName =
            new Regex(@"^--- !u!1 &\d+\nGameObject:\n(?:  .*\n)*?  m_Name: ?(?<name>.*)$", RegexOptions.Multiline);

        private static readonly Regex NameOverride =
            new Regex(@"^      propertyPath: m_Name\n      value: ?(?<name>.*)$", RegexOptions.Multiline);

        private const string FireballKey = "sandbox.ability.fireball";

        // GUID do provedor de texto do HUD (HUD_Texts, antes HUD_Textos; o GUID não mudou no SB-T11).
        private const string TextProviderGuid = "c259d8252affd184da1fb1410c77c626";

        // SB-T12: arquivos de Prefabs (CA-EN2) e GameObjects dos prefabs e da cena (CA-EN1).
        private static readonly string[] PrefabFolders = { "Prefabs" };

        private static readonly (string Path, string Guid)[] PrefabNames =
        {
            ("Prefabs/Caster.prefab", "2839213b5c9c01442846d899efd44e01"),
            ("Prefabs/Fireball.prefab", "0828333217fdef24092acd45ae76889e"),
            ("Prefabs/HUD.prefab", null),
            ("Prefabs/TrainingDummy.prefab", "f1d610bd2f0400c4e97953c054c9e722"),
        };

        private static readonly (string Path, string[] Names)[] PrefabGameObjectNames =
        {
            ("Prefabs/Caster.prefab", new[] { "Caster", "Visual" }),
            ("Prefabs/Fireball.prefab", new[] { "Fireball" }),
            ("Prefabs/TrainingDummy.prefab", new[] { "TrainingDummy", "Visual" }),
        };

        private const string ScenePath = "Scenes/Sandbox.unity";

        private static readonly string[] SceneGameObjectNames =
        {
            "Caster Spawn", "Console Without HUD", "Controls Sign", "Dummy Spawn", "Ground", "HUD", "Log Listener",
            "Main Camera", "Sandbox Director",
        };

        // Texto da placa de controles, repetido no README.md do Sample (CA-EN6, EN2).
        private const string ControlsSignText =
            "SpellBook Sandbox\n" +
            "Mouse: aim at the ground (the caster turns)\n" +
            "1: Fireball (cost, cooldown, pool, damage, burn)\n" +
            "P: pause and resume the game (gameplay time group)\n" +
            "T: slow motion, scale 0.25 (scaled time)\n" +
            "R: restart (health, mana, cooldown, pause and scale)";

        // Mensagens de LogReaction e LogPayloadReaction gravadas na cena, em ordem ordinal.
        private static readonly string[] SceneConsoleMessages = { "Burn: tick", "Damage taken: {0:0.#}", "Fireball hit", "{0}" };

        private static readonly string[] OldReadmeNames = { "BolaDeFogo", "BonecoDeTreino", "Conjurador", "Bola de Fogo" };

        // Literais de texto dos scripts do Sample, fora de comentário (CA-EN6). A categoria "Depuração" é a chave
        // que agrupa a reação com as do pacote no seletor de tipos e fica.
        private static readonly string[] SampleSourceLiterals =
        {
            "#attributereadout", "#damagetakenrelay", "#sandboxcaster", "#sandboxdirector", "#trainingdummyfeedback",
            "_Color",
            "Ability cast by the key.",
            "Anchor the HUD publishes itself to. If empty or unset, the HUD counts as absent.",
            "Attribute shown.",
            "Attributes shown and the current value of each.",
            "Caster prefab.",
            "Channel raised with the damage of each drop.",
            "Channel that receives the text of each failure. If empty, failures are not written.",
            "Color while the burn tag is active.",
            "Color without hit or burn.",
            "Console entry type.",
            "Current value, updated during Play.",
            "Damage taken: {0:0.#}",
            "DamageTakenRelay: the health attribute is not in the AttributeSet.",
            "Depuração",
            "Fireball cast",
            "Fireball failed: {Reason(in failure)}",
            "Fireball on cooldown: {0:0.0} s",
            "Flash color when taking damage.",
            "Flash duration, in seconds of the time source.",
            "Game paused",
            "Game resumed",
            "Gameplay time group, paused by the pause key.",
            "Height of the aim plane, in meters.",
            "Key that casts the ability.",
            "Listener that writes to the Console what the HUD would show. Enabled only without the HUD.",
            "Log has no format: fill in Format.",
            "Message format. {0} is the payload.",
            "Normal speed: scale 1",
            "Not enough mana for Fireball",
            "Observed health attribute.",
            "Pauses or resumes the gameplay group.",
            "Renderer painted by the feedback.",
            "Restarts the scene.",
            "Scene restarted",
            "Slow motion: scale {0:0.##}",
            "Tag that marks the burn.",
            "Time scale of the slow motion.",
            "Time source of the flash. If empty, uses scaled time.",
            "Training dummy prefab.",
            "Turns slow motion on or off.",
            "Where the caster spawns.",
            "Where the dummy spawns.",
            "Writes a message with the payload.",
        };

        private static readonly Regex StringFormatCall = new Regex(@"\bstring\.Format\(\s*(?<first>[^,]*),");

        private static readonly Regex ConsoleMessage =
            new Regex(@"^        (?:message|format): ?(?<value>.*)$", RegexOptions.Multiline);

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
                    else if (!Regex.Matches(page, @"^#{1,6} (?<title>.+?)\r?$", RegexOptions.Multiline)
                        .Cast<Match>().Any(title => title.Groups["title"].Value.ToLowerInvariant() == help.Groups["anchor"].Value))
                    {
                        violations.Add($"{Relative(file)}: nenhum título {help.Groups["anchor"].Value} em {SamplesPage}");
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
        public void SampleData_UI_HasEnglishNamesAndOriginalGuids()
        {
            AssertEnglishNames(UiFolders, UiNames, "arquivos de Data/UI (CA-EN2)");
        }

        [Test]
        public void HudPrefab_GameObjects_HaveEnglishNames()
        {
            Assert.That(GameObjectNames(HudPrefabPath), Is.EqualTo(HudGameObjectNames), "GameObjects do HUD.prefab (CA-EN1)");
        }

        [Test]
        public void SamplePrefabs_HaveEnglishNamesAndOriginalGuids()
        {
            // O primeiro m_Name de um prefab pode ser de um filho; o nome da raiz é conferido pelos GameObjects.
            AssertEnglishNames(PrefabFolders, PrefabNames, "arquivos de Prefabs (CA-EN2)", checkAssetName: false);
        }

        [Test]
        public void SamplePrefabs_GameObjects_HaveEnglishNames()
        {
            foreach ((string path, string[] names) in PrefabGameObjectNames)
            {
                Assert.That(GameObjectNames(path), Is.EqualTo(names), $"GameObjects do {path} (CA-EN1)");
            }
        }

        [Test]
        public void SandboxScene_GameObjects_HaveEnglishNames()
        {
            Assert.That(GameObjectNames(ScenePath), Is.EqualTo(SceneGameObjectNames), "GameObjects do Sandbox.unity (CA-EN1)");
        }

        [Test]
        public void SandboxScene_ControlsSignAndConsoleMessages_AreInEnglish()
        {
            string yaml = ReadSample(ScenePath);

            string[] messages = ConsoleMessage.Matches(yaml).Cast<Match>()
                .Select(m => YamlScalar(m.Groups["value"].Value))
                .OrderBy(m => m, StringComparer.Ordinal)
                .ToArray();

            Assert.That(ControlsSign(yaml), Is.EqualTo(ControlsSignText), "texto da placa de controles (CA-EN6)");
            Assert.That(messages, Is.EqualTo(SceneConsoleMessages), "mensagens de Console gravadas na cena (CA-EN6)");
        }

        [Test]
        public void SampleReadmeAndCredits_UseEnglishNames()
        {
            string readme = ReadSample("README.md");
            string credits = ReadSample("CREDITS.md");

            Assert.That(readme, Does.Contain("```text\n" + ControlsSignText + "\n```"), "o README.md não repete a placa (EN2)");
            Assert.That(readme.Where(c => c > '\u007f').Distinct(), Is.Empty, "o README.md tem texto fora do inglês (EN2)");
            Assert.That(OldReadmeNames.Where(readme.Contains), Is.Empty, "o README.md cita nomes antigos (CA-EN4)");
            Assert.That(credits, Does.Contain("`GA_Fireball`"), "o CREDITS.md não cita o GA_Fireball (CA-EN4)");
            Assert.That(credits, Does.Not.Contain("GA_BolaDeFogo"), "o CREDITS.md cita o nome antigo (CA-EN4)");
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

        [Test]
        public void SampleSources_StringLiteralsAreInEnglish()
        {
            string[] literals = SampleSources()
                .SelectMany(file => StringLiterals(File.ReadAllText(file)))
                .Distinct()
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToArray();

            Assert.That(literals, Is.EqualTo(SampleSourceLiterals.OrderBy(l => l, StringComparer.Ordinal).ToArray()),
                "textos dos scripts do Sample (CA-EN6)");
        }

        [Test]
        public void SampleSources_FormatNumbersWithInvariantCulture()
        {
            var violations = new List<string>();

            foreach (string file in SampleSources())
            {
                string text = File.ReadAllText(file);
                violations.AddRange(StringFormatCall.Matches(text).Cast<Match>()
                    .Where(m => m.Groups["first"].Value.Trim() != "CultureInfo.InvariantCulture")
                    .Select(m => $"{Relative(file)}: string.Format sem InvariantCulture ({m.Groups["first"].Value.Trim()})"));
                if (text.Contains("pt-BR"))
                {
                    violations.Add($"{Relative(file)}: cita pt-BR");
                }
            }

            Assert.That(violations, Is.Empty, "cultura dos números formatados pelos scripts (CA-EN6)");
        }

        /// <summary>
        /// Os arquivos (sem <c>.meta</c>) das pastas são exatamente os nomes dados, cada <c>m_Name</c> é o nome do
        /// arquivo (se <paramref name="checkAssetName"/>) e o <c>.meta</c> guarda o GUID de antes da renomeação (nulo
        /// onde o nome não mudou). Caminhos a partir da pasta do Sample.
        /// </summary>
        private static void AssertEnglishNames(
            IEnumerable<string> folders, IEnumerable<(string Path, string Guid)> names, string message,
            bool checkAssetName = true)
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
                if (checkAssetName && YamlValue(File.ReadAllText(file), "m_Name") != name)
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

        /// <summary>Texto de um arquivo do Sample, com quebras normalizadas para \n; falha com o caminho se ele não existe.</summary>
        private static string ReadSample(string relativePath)
        {
            string file = Path.Combine(SampleFolder(), relativePath);
            Assert.That(File.Exists(file), Is.True, $"{SamplePath}/{relativePath} não existe");
            return File.ReadAllText(file).Replace("\r\n", "\n");
        }

        /// <summary>
        /// Os nomes de GameObject de um prefab ou cena do Sample, em ordem ordinal: o <c>m_Name</c> de cada
        /// <c>GameObject</c> e o override <c>m_Name</c> de cada instância de prefab.
        /// </summary>
        private static string[] GameObjectNames(string relativePath)
        {
            string yaml = ReadSample(relativePath);
            return GameObjectName.Matches(yaml).Cast<Match>()
                .Concat(NameOverride.Matches(yaml).Cast<Match>())
                .Select(m => YamlScalar(m.Groups["name"].Value))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// O <c>m_Text</c> do <c>TextMesh</c> da cena (a placa de controles). Desfaz a dobra do YAML: a quebra simples
        /// vira espaço, e cada linha em branco vira uma quebra de linha.
        /// </summary>
        private static string ControlsSign(string yaml)
        {
            Match match = Regex.Match(yaml, @"^TextMesh:\n(?:  [^\n]*\n)*?  m_Text: (?<value>[\s\S]*?)\n(?=  \S)",
                RegexOptions.Multiline);
            Assert.That(match.Success, Is.True, "a cena não tem o TextMesh da placa de controles");

            string[] lines = match.Groups["value"].Value.Split('\n');
            string unfolded = lines[0];
            int breaks = 0;
            foreach (string line in lines.Skip(1).Select(l => l.Trim()))
            {
                if (line.Length == 0)
                {
                    breaks++;
                    continue;
                }

                unfolded += (breaks > 0 ? new string('\n', breaks) : " ") + line;
                breaks = 0;
            }

            return YamlScalar(unfolded);
        }

        /// <summary>O valor de um escalar YAML de uma linha: sem aspas, entre aspas simples ou entre aspas duplas.</summary>
        private static string YamlScalar(string raw)
        {
            if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
            {
                return raw.Substring(1, raw.Length - 2).Replace("''", "'");
            }

            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
            {
                return Regex.Replace(raw.Substring(1, raw.Length - 2), @"\\(x[0-9A-Fa-f]{2}|u[0-9A-Fa-f]{4}|.)", m =>
                {
                    string escape = m.Groups[1].Value;
                    switch (escape[0])
                    {
                        case 'n': return "\n";
                        case 't': return "\t";
                        case 'x':
                        case 'u': return ((char)Convert.ToInt32(escape.Substring(1), 16)).ToString();
                        default: return escape;
                    }
                });
            }

            return raw;
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

        /// <summary>
        /// Conteúdo dos literais de texto do código C#, em ordem, pulando comentários e literais de caractere. Lê o
        /// texto entre as aspas como está, sem resolver escapes; o de uma string interpolada vem com as chaves.
        /// </summary>
        private static IEnumerable<string> StringLiterals(string source)
        {
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    int end = source.IndexOf('\n', i);
                    i = end < 0 ? source.Length : end;
                }
                else if (c == '/' && next == '*')
                {
                    int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? source.Length : end + 2;
                }
                else if (c == '"' || c == '\'')
                {
                    int start = i + 1;
                    int j = start;
                    while (j < source.Length && source[j] != c)
                    {
                        j += source[j] == '\\' ? 2 : 1;
                    }

                    if (c == '"')
                    {
                        yield return source.Substring(start, Math.Min(j, source.Length) - start);
                    }

                    i = j + 1;
                }
                else
                {
                    i++;
                }
            }
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
