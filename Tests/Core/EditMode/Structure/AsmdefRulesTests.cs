using System.Collections.Generic;
using System.Linq;
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

                if (asmdef.Data.defineConstraints.Length > 0)
                {
                    violations.Add($"{asmdef.Path}: defineConstraints não está vazio");
                }
            }

            Assert.That(violations, Is.Empty);
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
    }
}
