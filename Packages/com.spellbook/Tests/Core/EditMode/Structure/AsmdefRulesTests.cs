using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>Regras dos asmdefs do pacote (ADR 0026, item 1; Spec, seção 3) e mapa de dependências (CA2).</summary>
    public class AsmdefRulesTests
    {
        private static readonly string[] Infra =
        {
            "SpellBook.Core", "SpellBook.Time", "SpellBook.Events", "SpellBook.Pooling", "SpellBook.Tags",
        };

        /// <summary>Módulos de runtime da lista final do CA1 e do CA32.</summary>
        private static readonly string[] RuntimeModules =
        {
            "Core", "Time", "Events", "Pooling", "Tags", "GAS", "GAS.Bridges", "Teams", "Cues", "Localization", "UI", "GAS.UI",
        };

        /// <summary>Módulos opcionais de integração: compilam só com o pacote externo (ADR 0038, itens 6 e 7).</summary>
        private static readonly string[] OptionalModules = { "SpellBook.Localization", "SpellBook.Localization.Editor" };

        /// <summary>Asmdefs do adaptador: os únicos que podem referenciar o Unity Localization e o próprio adaptador (CA10).</summary>
        private static readonly string[] AdapterAsmdefs =
        {
            "SpellBook.Localization", "SpellBook.Localization.Editor", "SpellBook.Localization.Tests.EditMode",
        };

        private static readonly string[] EditorModules =
        {
            "Core", "Events", "Time", "Tags", "Pooling", "GAS", "GAS.Bridges", "Teams", "Cues", "Localization", "UI",
        };

        private static readonly string[] PlayModeModules = { "Core", "Time", "Events", "GAS", "GAS.Bridges", "Cues", "UI", "GAS.UI" };

        [Test]
        public void AsmdefRules_Scan_FindsCoreAndThisTestAsmdef()
        {
            List<string> names = PackageFiles.Asmdefs().Select(a => a.Data.name).ToList();

            Assert.That(names, Does.Contain("SpellBook.Core"));
            Assert.That(names, Does.Contain("SpellBook.Core.Tests.EditMode"));
        }

        [Test]
        public void AsmdefRules_AllOverrideReferences_PrecompiledInAllowList()
        {
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs())
            {
                string[] allowed = asmdef.Kind == AsmdefKind.Test ? new[] { "nunit.framework.dll" } : new string[0];
                if (!asmdef.Data.overrideReferences)
                {
                    violations.Add($"{asmdef.Path}: overrideReferences não é true");
                }

                foreach (string dll in asmdef.Data.precompiledReferences.Where(d => !allowed.Contains(d)))
                {
                    violations.Add($"{asmdef.Path}: precompiledReferences fora da lista permitida: {dll}");
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_RuntimeAsmdefs_AutoReferencedWithoutConstraints()
        {
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs().Where(a => a.Kind == AsmdefKind.Runtime))
            {
                if (!asmdef.Data.autoReferenced)
                {
                    violations.Add($"{asmdef.Path}: autoReferenced não é true");
                }

                if (asmdef.Data.defineConstraints.Length > 0 && !OptionalModules.Contains(asmdef.Data.name))
                {
                    violations.Add($"{asmdef.Path}: defineConstraints não está vazio");
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void OptionalModule_OnlyConstraintIsOwnVersionDefine()
        {
            List<AsmdefFile> optional = PackageFiles.Asmdefs().Where(a => OptionalModules.Contains(a.Data.name)).ToList();
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in optional)
            {
                string[] constraints = asmdef.Data.defineConstraints;
                VersionDefineData[] defines = asmdef.Data.versionDefines;
                if (constraints.Length != 1 || defines.Length != 1 || constraints[0] != defines[0].define)
                {
                    violations.Add($"{asmdef.Path}: a única defineConstraints tem de ser o símbolo do próprio versionDefines");
                }
            }

            Assert.That(optional.Select(a => a.Data.name), Is.EquivalentTo(OptionalModules), "módulo opcional sem asmdef");
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void LocalizationAsmdef_ReferencesCoreAndUnityLocalization()
        {
            AsmdefFile localization = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Localization");

            Assert.That(localization, Is.Not.Null, "não existe asmdef SpellBook.Localization");
            Assert.That(localization.Path, Is.EqualTo("Runtime/Localization/SpellBook.Localization.asmdef"));
            Assert.That(localization.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(localization.ReferenceNames, Is.EquivalentTo(new[] { "SpellBook.Core", "Unity.Localization" }));
            Assert.That(localization.Data.versionDefines.Select(v => (v.name, v.define)),
                Is.EqualTo(new[] { ("com.unity.localization", "SPELLBOOK_UNITY_LOCALIZATION") }));
        }

        [Test]
        public void LocalizationEditor_ConstraintMatchesRuntime()
        {
            List<AsmdefFile> asmdefs = PackageFiles.Asmdefs().ToList();
            AsmdefFile runtime = asmdefs.Single(a => a.Data.name == "SpellBook.Localization");
            AsmdefFile editor = asmdefs.SingleOrDefault(a => a.Data.name == "SpellBook.Localization.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.Localization.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/Localization/SpellBook.Localization.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(editor.ReferenceNames,
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Localization", "SpellBook.Core.Editor" }));
            Assert.That(editor.Data.defineConstraints, Is.EqualTo(runtime.Data.defineConstraints));
            Assert.That(editor.Data.versionDefines.Select(v => (v.name, v.expression, v.define)),
                Is.EqualTo(runtime.Data.versionDefines.Select(v => (v.name, v.expression, v.define))));
        }

        [Test]
        public void NoAsmdefOutsideAdapter_ReferencesUnityLocalization()
        {
            List<AsmdefFile> asmdefs = PackageFiles.Asmdefs().ToList();
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in asmdefs.Where(a => !AdapterAsmdefs.Contains(a.Data.name)))
            {
                foreach (string reference in asmdef.ReferenceNames)
                {
                    bool unityLocalization = reference.StartsWith("Unity.Localization", StringComparison.Ordinal);
                    bool adapter = AdapterAsmdefs.Contains(reference);
                    if (unityLocalization || adapter)
                    {
                        violations.Add($"{asmdef.Path}: {asmdef.Data.name} não pode referenciar {reference}");
                    }
                }
            }

            Assert.That(asmdefs.Select(a => a.Data.name).Intersect(AdapterAsmdefs), Is.EquivalentTo(AdapterAsmdefs),
                "a varredura não achou os asmdefs do adaptador");
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void PackageJson_DoesNotDeclareLocalization()
        {
            string packageJson = PackageFiles.ReadText("package.json");

            Assert.That(packageJson, Does.Contain("\"name\": \"com.spellbook\""), "o package.json lido não é o do SpellBook");
            Assert.That(packageJson, Does.Not.Contain("com.unity.localization"));
        }

        [Test]
        public void AsmdefRules_EditorAsmdefs_AutoReferenced()
        {
            List<string> violations = PackageFiles.Asmdefs()
                .Where(a => a.Kind == AsmdefKind.Editor && !a.Data.autoReferenced)
                .Select(a => $"{a.Path}: autoReferenced não é true")
                .ToList();

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_TestAsmdefs_OnlyWithTestsAndTestRunnerReferences()
        {
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs().Where(a => a.Kind == AsmdefKind.Test))
            {
                if (!asmdef.Data.defineConstraints.Contains("UNITY_INCLUDE_TESTS"))
                {
                    violations.Add($"{asmdef.Path}: falta UNITY_INCLUDE_TESTS em defineConstraints");
                }

                if (asmdef.Data.autoReferenced)
                {
                    violations.Add($"{asmdef.Path}: autoReferenced não é false");
                }

                foreach (string runner in new[] { "UnityEngine.TestRunner", "UnityEditor.TestRunner" })
                {
                    if (!asmdef.ReferenceNames.Contains(runner))
                    {
                        violations.Add($"{asmdef.Path}: falta a referência {runner}");
                    }
                }

                bool editorOnly = asmdef.Data.includePlatforms.SequenceEqual(new[] { "Editor" });
                if (asmdef.Path.Contains("/EditMode/") && !editorOnly)
                {
                    violations.Add($"{asmdef.Path}: EditMode sem includePlatforms [\"Editor\"]");
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_EveryModuleTestAsmdef_ReferencesTimeAndTesting()
        {
            List<AsmdefFile> moduleTests = PackageFiles.Asmdefs()
                .Where(a => a.Kind == AsmdefKind.Test && a.Data.name != "SpellBook.Testing")
                .ToList();
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in moduleTests)
            {
                foreach (string required in new[] { "SpellBook.Time", "SpellBook.Testing" })
                {
                    if (!asmdef.ReferenceNames.Contains(required))
                    {
                        violations.Add($"{asmdef.Path}: falta a referência {required}");
                    }
                }
            }

            Assert.That(moduleTests.Count, Is.GreaterThanOrEqualTo(10), "a varredura achou poucos asmdefs de teste de módulo");
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_ModuleReferences_FollowDependencyMap()
        {
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs())
            {
                string name = asmdef.Data.name;
                foreach (string reference in asmdef.ReferenceNames)
                {
                    bool infraToGas = Infra.Contains(name) && reference.StartsWith("SpellBook.GAS");
                    bool gasToEventsOrPooling = name == "SpellBook.GAS"
                        && (reference == "SpellBook.Events" || reference == "SpellBook.Pooling");
                    bool bridgesToPooling = name == "SpellBook.GAS.Bridges" && reference == "SpellBook.Pooling";
                    bool eventsBeyondCoreTime = name == "SpellBook.Events" && reference.StartsWith("SpellBook.")
                        && reference != "SpellBook.Core" && reference != "SpellBook.Time";

                    if (infraToGas || gasToEventsOrPooling || bridgesToPooling || eventsBeyondCoreTime)
                    {
                        violations.Add($"{asmdef.Path}: {name} não pode referenciar {reference}");
                    }
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_GasAssemblyExists()
        {
            AsmdefFile gas = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS");

            Assert.That(gas, Is.Not.Null, "não existe asmdef SpellBook.GAS");
            Assert.That(gas.Path, Is.EqualTo("Runtime/GAS/SpellBook.GAS.asmdef"));
            Assert.That(gas.ReferenceNames, Does.Contain("SpellBook.Core"));
            Assert.That(LoadedAssembly("SpellBook.GAS"), Is.Not.Null, "o assembly SpellBook.GAS não foi compilado");
        }

        [Test]
        public void AsmdefRules_CoreDoesNotReferenceGas()
        {
            Assembly core = LoadedAssembly("SpellBook.Core");

            List<string> gasTypesInCore = core.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("SpellBook.GAS", StringComparison.Ordinal))
                .Select(t => t.FullName)
                .ToList();
            List<string> gasReferences = core.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("SpellBook.GAS", StringComparison.Ordinal))
                .ToList();

            Assert.That(PackageFiles.Asmdefs().Any(a => a.Data.name == "SpellBook.GAS"), Is.True, "não existe asmdef SpellBook.GAS");
            Assert.That(gasTypesInCore, Is.Empty, "tipos do GAS compilados no SpellBook.Core");
            Assert.That(gasReferences, Is.Empty, "o SpellBook.Core referencia o GAS");
        }

        [Test]
        public void AsmdefRules_TagsAssemblyExists()
        {
            AsmdefFile tags = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Tags");
            AsmdefFile gas = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS");

            Assert.That(tags, Is.Not.Null, "não existe asmdef SpellBook.Tags");
            Assert.That(tags.Path, Is.EqualTo("Runtime/Tags/SpellBook.Tags.asmdef"));
            Assert.That(gas.ReferenceNames, Does.Contain("SpellBook.Tags"));
            Assert.That(LoadedAssembly("SpellBook.Tags"), Is.Not.Null, "o assembly SpellBook.Tags não foi compilado");
        }

        [Test]
        public void AsmdefRules_EventsAssemblyExists_ReferencesOnlyCoreAndTime()
        {
            AsmdefFile events = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Events");

            Assert.That(events, Is.Not.Null, "não existe asmdef SpellBook.Events");
            Assert.That(events.Path, Is.EqualTo("Runtime/Events/SpellBook.Events.asmdef"));
            Assert.That(
                events.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Time" }));
            Assert.That(LoadedAssembly("SpellBook.Events"), Is.Not.Null, "o assembly SpellBook.Events não foi compilado");
        }

        [Test]
        public void AsmdefRules_BridgesDoesNotReferencePooling()
        {
            AsmdefFile bridges = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS.Bridges");

            Assert.That(bridges, Is.Not.Null, "não existe asmdef SpellBook.GAS.Bridges");
            Assert.That(bridges.Path, Is.EqualTo("Runtime/GAS.Bridges/SpellBook.GAS.Bridges.asmdef"));
            Assert.That(bridges.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(bridges.ReferenceNames, Has.None.EqualTo("SpellBook.Pooling"), "o Bridges referencia o Pooling");
            Assert.That(
                bridges.ReferenceNames.Where(r => r.EndsWith(".Editor", StringComparison.Ordinal)),
                Is.Empty,
                "o Bridges referencia um assembly de Editor");
            Assert.That(
                bridges.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[]
                {
                    "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time", "SpellBook.GAS", "SpellBook.Events", "SpellBook.Teams",
                    "SpellBook.Cues",
                }));
            Assert.That(LoadedAssembly("SpellBook.GAS.Bridges"), Is.Not.Null, "o assembly SpellBook.GAS.Bridges não foi compilado");
        }

        [Test]
        public void BridgesReferencesTeams_GasDoesNot()
        {
            List<AsmdefFile> asmdefs = PackageFiles.Asmdefs().ToList();
            AsmdefFile bridges = asmdefs.Single(a => a.Data.name == "SpellBook.GAS.Bridges");
            AsmdefFile gas = asmdefs.Single(a => a.Data.name == "SpellBook.GAS");
            AsmdefFile teams = asmdefs.Single(a => a.Data.name == "SpellBook.Teams");

            Assert.That(bridges.ReferenceNames, Does.Contain("SpellBook.Teams"), "o Bridges não referencia o Teams");
            Assert.That(gas.ReferenceNames, Has.None.EqualTo("SpellBook.Teams"), "o GAS referencia o Teams");
            Assert.That(teams.ReferenceNames.Where(r => r.StartsWith("SpellBook.GAS", StringComparison.Ordinal)), Is.Empty,
                "o Teams referencia o GAS ou o Bridges");
            Assert.That(LoadedAssembly("SpellBook.GAS").GetReferencedAssemblies().Select(a => a.Name),
                Has.None.EqualTo("SpellBook.Teams"), "o assembly do GAS compilou com o Teams");
        }

        [Test]
        public void BridgesReferencesCues_GasDoesNot()
        {
            List<AsmdefFile> asmdefs = PackageFiles.Asmdefs().ToList();
            AsmdefFile bridges = asmdefs.Single(a => a.Data.name == "SpellBook.GAS.Bridges");
            AsmdefFile gas = asmdefs.Single(a => a.Data.name == "SpellBook.GAS");
            AsmdefFile cues = asmdefs.Single(a => a.Data.name == "SpellBook.Cues");

            Assert.That(bridges.ReferenceNames, Does.Contain("SpellBook.Cues"), "o Bridges não referencia o Cues");
            Assert.That(gas.ReferenceNames, Has.None.EqualTo("SpellBook.Cues"), "o GAS referencia o Cues");
            Assert.That(cues.ReferenceNames.Where(r => r.StartsWith("SpellBook.GAS", StringComparison.Ordinal)), Is.Empty,
                "o Cues referencia o GAS ou o Bridges");
            Assert.That(LoadedAssembly("SpellBook.GAS").GetReferencedAssemblies().Select(a => a.Name),
                Has.None.EqualTo("SpellBook.Cues"), "o assembly do GAS compilou com o Cues");
        }

        [Test]
        public void TeamsAsmdef_ReferencesOnlyCore()
        {
            AsmdefFile teams = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Teams");

            Assert.That(teams, Is.Not.Null, "não existe asmdef SpellBook.Teams");
            Assert.That(teams.Path, Is.EqualTo("Runtime/Teams/SpellBook.Teams.asmdef"));
            Assert.That(teams.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(
                teams.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core" }));
            Assert.That(LoadedAssembly("SpellBook.Teams"), Is.Not.Null, "o assembly SpellBook.Teams não foi compilado");
        }

        [Test]
        public void CuesAsmdef_ReferencesCoreTagsTime()
        {
            AsmdefFile cues = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Cues");

            Assert.That(cues, Is.Not.Null, "não existe asmdef SpellBook.Cues");
            Assert.That(cues.Path, Is.EqualTo("Runtime/Cues/SpellBook.Cues.asmdef"));
            Assert.That(cues.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(
                cues.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time" }));
            Assert.That(LoadedAssembly("SpellBook.Cues"), Is.Not.Null, "o assembly SpellBook.Cues não foi compilado");
        }

        [Test]
        public void UIAsmdef_ReferencesOnlyCoreAndTime()
        {
            AsmdefFile ui = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.UI");

            Assert.That(ui, Is.Not.Null, "não existe asmdef SpellBook.UI");
            Assert.That(ui.Path, Is.EqualTo("Runtime/UI/SpellBook.UI.asmdef"));
            Assert.That(ui.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(ui.ReferenceNames, Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Time" }));
            Assembly assembly = LoadedAssembly("SpellBook.UI");
            Assert.That(assembly, Is.Not.Null, "o assembly SpellBook.UI não foi compilado");
            Assert.That(
                assembly.GetReferencedAssemblies().Select(a => a.Name).Where(n => n.StartsWith("SpellBook.GAS", StringComparison.Ordinal)
                    || n == "UnityEngine.UI" || n == "Unity.TextMeshPro"),
                Is.Empty,
                "o assembly SpellBook.UI compilou com o GAS, a uGUI ou o TextMeshPro");
        }

        [Test]
        public void GasUiAsmdef_ReferencesExactly_NoBridgesNoUgui()
        {
            AsmdefFile gasUi = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS.UI");

            Assert.That(gasUi, Is.Not.Null, "não existe asmdef SpellBook.GAS.UI");
            Assert.That(gasUi.Path, Is.EqualTo("Runtime/GAS.UI/SpellBook.GAS.UI.asmdef"));
            Assert.That(gasUi.Kind, Is.EqualTo(AsmdefKind.Runtime));
            Assert.That(gasUi.ReferenceNames, Is.EquivalentTo(new[]
            {
                "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time", "SpellBook.GAS", "SpellBook.UI",
            }));
            Assembly assembly = LoadedAssembly("SpellBook.GAS.UI");
            Assert.That(assembly, Is.Not.Null, "o assembly SpellBook.GAS.UI não foi compilado");
            Assert.That(
                assembly.GetReferencedAssemblies().Select(a => a.Name)
                    .Where(n => n == "SpellBook.GAS.Bridges" || n == "UnityEngine.UI" || n == "Unity.TextMeshPro"),
                Is.Empty,
                "o assembly SpellBook.GAS.UI compilou com o Bridges, a uGUI ou o TextMeshPro");
            Assert.That(
                LoadedAssembly("SpellBook.GAS").GetReferencedAssemblies().Select(a => a.Name)
                    .Where(n => n.StartsWith("SpellBook.UI", StringComparison.Ordinal) || n == "SpellBook.GAS.UI"),
                Is.Empty,
                "o assembly SpellBook.GAS referencia a UI");
        }

        [Test]
        public void UIEditorAsmdef_References()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.UI.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.UI.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/UI/SpellBook.UI.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(editor.ReferenceNames,
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Time", "SpellBook.UI", "SpellBook.Core.Editor" }));
            Assert.That(LoadedAssembly("SpellBook.UI.Editor"), Is.Not.Null, "o assembly SpellBook.UI.Editor não foi compilado");
        }

        [Test]
        public void AsmdefRules_EventsEditorReferencesOnlyCoreTimeEventsCoreEditor()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Events.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.Events.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/Events/SpellBook.Events.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(
                editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Time", "SpellBook.Events", "SpellBook.Core.Editor" }));
            Assert.That(LoadedAssembly("SpellBook.Events.Editor"), Is.Not.Null, "o assembly SpellBook.Events.Editor não foi compilado");
        }

        [Test]
        public void AsmdefRules_TimeTagsPoolingEditorsExist_WithAllowedReferences()
        {
            var expected = new Dictionary<string, string[]>
            {
                ["Time"] = new[] { "SpellBook.Core", "SpellBook.Time", "SpellBook.Core.Editor" },
                ["Tags"] = new[] { "SpellBook.Core", "SpellBook.Tags", "SpellBook.Core.Editor" },
                ["Pooling"] = new[] { "SpellBook.Core", "SpellBook.Pooling", "SpellBook.Core.Editor" },
            };

            foreach (KeyValuePair<string, string[]> module in expected)
            {
                string name = $"SpellBook.{module.Key}.Editor";
                AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == name);

                Assert.That(editor, Is.Not.Null, $"não existe asmdef {name}");
                Assert.That(editor.Path, Is.EqualTo($"Editor/{module.Key}/{name}.asmdef"));
                Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
                Assert.That(
                    editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                    Is.EquivalentTo(module.Value),
                    name);
                Assert.That(LoadedAssembly(name), Is.Not.Null, $"o assembly {name} não foi compilado");
            }
        }

        [Test]
        public void AsmdefRules_GasEditorExists_WithAllowedReferences()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.GAS.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/GAS/SpellBook.GAS.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(
                editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time", "SpellBook.GAS", "SpellBook.Core.Editor" }));
            Assert.That(LoadedAssembly("SpellBook.GAS.Editor"), Is.Not.Null, "o assembly SpellBook.GAS.Editor não foi compilado");
        }

        [Test]
        public void TeamsEditorAsmdef_References()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Teams.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.Teams.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/Teams/SpellBook.Teams.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(
                editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Teams", "SpellBook.Core.Editor" }));
            Assert.That(LoadedAssembly("SpellBook.Teams.Editor"), Is.Not.Null, "o assembly SpellBook.Teams.Editor não foi compilado");
        }

        [Test]
        public void CuesEditorAsmdef_References()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Cues.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.Cues.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/Cues/SpellBook.Cues.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(
                editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[] { "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time", "SpellBook.Cues", "SpellBook.Core.Editor" }));
            Assert.That(LoadedAssembly("SpellBook.Cues.Editor"), Is.Not.Null, "o assembly SpellBook.Cues.Editor não foi compilado");
        }

        [Test]
        public void GasBridgesEditorAsmdef_ReferencesExactly()
        {
            AsmdefFile editor = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.GAS.Bridges.Editor");

            Assert.That(editor, Is.Not.Null, "não existe asmdef SpellBook.GAS.Bridges.Editor");
            Assert.That(editor.Path, Is.EqualTo("Editor/GAS.Bridges/SpellBook.GAS.Bridges.Editor.asmdef"));
            Assert.That(editor.Kind, Is.EqualTo(AsmdefKind.Editor));
            Assert.That(
                editor.ReferenceNames.Where(r => r.StartsWith("SpellBook.", StringComparison.Ordinal)),
                Is.EquivalentTo(new[]
                {
                    "SpellBook.Core", "SpellBook.Tags", "SpellBook.Time", "SpellBook.GAS", "SpellBook.Events",
                    "SpellBook.GAS.Bridges", "SpellBook.Core.Editor",
                }));
            Assert.That(LoadedAssembly("SpellBook.GAS.Bridges.Editor"), Is.Not.Null, "o assembly SpellBook.GAS.Bridges.Editor não foi compilado");
        }

        [Test]
        public void AsmdefRules_ModuleEditorsDoNotReferenceOtherModuleEditors()
        {
            List<AsmdefFile> moduleEditors = PackageFiles.Asmdefs()
                .Where(a => a.Kind == AsmdefKind.Editor && a.Data.name != "SpellBook.Core.Editor")
                .ToList();
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs().Where(a => a.Kind == AsmdefKind.Editor))
            {
                foreach (string reference in asmdef.ReferenceNames)
                {
                    bool otherEditor = reference.StartsWith("SpellBook.", StringComparison.Ordinal)
                        && reference.EndsWith(".Editor", StringComparison.Ordinal);
                    bool allowed = reference == "SpellBook.Core.Editor" && asmdef.Data.name != "SpellBook.Core.Editor";
                    if (otherEditor && !allowed)
                    {
                        violations.Add($"{asmdef.Path}: {asmdef.Data.name} não pode referenciar {reference}");
                    }
                }
            }

            Assert.That(moduleEditors, Is.Not.Empty, "a varredura não achou nenhum asmdef .Editor de módulo");
            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void AsmdefRules_CoreAsmdefInRuntimeCore()
        {
            List<string> paths = PackageFiles.Asmdefs().Select(a => a.Path).ToList();
            AsmdefFile core = PackageFiles.Asmdefs().SingleOrDefault(a => a.Data.name == "SpellBook.Core");

            Assert.That(core, Is.Not.Null, "não existe asmdef SpellBook.Core");
            Assert.That(core.Path, Is.EqualTo("Runtime/Core/SpellBook.Core.asmdef"));
            Assert.That(paths.Where(p => p.Count(c => c == '/') < 2), Is.Empty, "asmdef fora da pasta de um módulo");
        }

        [Test]
        public void AllExpectedAssembliesExist()
        {
            var expected = new List<string>();
            expected.AddRange(RuntimeModules.Select(m => $"SpellBook.{m}"));
            expected.AddRange(EditorModules.Select(m => $"SpellBook.{m}.Editor"));
            expected.AddRange(RuntimeModules.Select(m => $"SpellBook.{m}.Tests.EditMode"));
            expected.AddRange(PlayModeModules.Select(m => $"SpellBook.{m}.Tests.PlayMode"));
            expected.Add("SpellBook.Testing");

            List<string> names = PackageFiles.Asmdefs().Select(a => a.Data.name).ToList();

            Assert.That(names, Is.EquivalentTo(expected));
            Assert.That(names, Has.None.EqualTo("SpellBook.Setup.Editor"));
            Assert.That(expected.Where(n => LoadedAssembly(n) == null), Is.Empty, "assemblies não compilados");
        }

        [Test]
        public void EachRuntimeModuleHasEditModeTests()
        {
            List<AsmdefFile> asmdefs = PackageFiles.Asmdefs().ToList();
            List<string> runtime = asmdefs.Where(a => a.Kind == AsmdefKind.Runtime).Select(a => a.Data.name).ToList();
            var missing = new List<string>();

            foreach (string name in runtime)
            {
                string module = name.Substring("SpellBook.".Length);
                string path = $"Tests/{module}/EditMode/{name}.Tests.EditMode.asmdef";
                if (!asmdefs.Any(a => a.Path == path && a.Data.name == $"{name}.Tests.EditMode"))
                {
                    missing.Add(path);
                }
            }

            Assert.That(runtime, Is.EquivalentTo(RuntimeModules.Select(m => $"SpellBook.{m}")));
            Assert.That(missing, Is.Empty);
        }

        [TestCase("GameplayTag")]
        [TestCase("GameplayTagDefinition")]
        public void GameplayTag_LivesInTagsAssembly(string typeName)
        {
            List<string> owners = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("SpellBook.", StringComparison.Ordinal))
                .SelectMany(a => a.GetTypes())
                .Where(t => t.Name == typeName)
                .Select(t => t.Assembly.GetName().Name)
                .ToList();

            Assert.That(owners, Is.EqualTo(new[] { "SpellBook.Tags" }));
        }

        [TestCase("GameplayTag")]
        [TestCase("GameplayTagDefinition")]
        public void GameplayTag_NamespaceIsSpellBookTags(string typeName)
        {
            List<string> namespaces = LoadedAssembly("SpellBook.Tags").GetTypes()
                .Where(t => t.Name == typeName)
                .Select(t => t.Namespace)
                .ToList();

            Assert.That(namespaces, Is.EqualTo(new[] { "SpellBook.Tags" }));
        }

        private static Assembly LoadedAssembly(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == name);
        }
    }
}
