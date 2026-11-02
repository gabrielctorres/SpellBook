using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using UnityEditor.Compilation;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>Configuração de metadados do DocFX a partir da compilação da Unity (Spec referencia-catalogo-e-api, D4; RC3).</summary>
    public class DocfxConfigTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "spellbook-docfx-config"));
        private static readonly string ConfigDir = Path.Combine(Root, "Documentation~", "api", "obj");
        private static readonly string[] NoSystemDirectories = new string[0];

        [Test]
        public void Render_SourcesOnlyFromPackageAssemblies()
        {
            string text = DocfxConfig.Render(new[] { CoreAssembly(), TestsAssembly() }, NoSystemDirectories, ConfigDir);

            StringAssert.Contains("\"DocUrls.cs\"", text);
            StringAssert.DoesNotContain("ManualLinksTests.cs", text);
            StringAssert.DoesNotContain("nunit.framework.dll", text);
        }

        [Test]
        public void Render_SourcePathsRelativeToConfigDirWithForwardSlashes()
        {
            string text = DocfxConfig.Render(new[] { CoreAssembly() }, NoSystemDirectories, ConfigDir);

            StringAssert.Contains("\"src\": \"../../../Packages/com.spellbook/Runtime/Core\"", text);
            StringAssert.DoesNotContain("\\", text);
        }

        [Test]
        public void Render_ReferencesExcludePackageOutputs()
        {
            var tags = new DocfxAssembly("SpellBook.Tags", new[] { At("Packages", "com.spellbook", "Runtime", "Tags", "GameplayTag.cs") },
                new[] { At("Library", "ScriptAssemblies", "SpellBook.Core.dll"), Unity("UnityEngine.CoreModule.dll") });

            string text = DocfxConfig.Render(new[] { CoreAssembly(), tags }, NoSystemDirectories, ConfigDir);

            StringAssert.Contains("\"UnityEngine.CoreModule.dll\"", text);
            StringAssert.DoesNotContain("SpellBook.Core.dll", text);
            StringAssert.DoesNotContain("ScriptAssemblies", text);
        }

        [Test]
        public void Render_SkipsReferencesInSystemAssemblyDirectories()
        {
            var unity = new DocfxAssembly("SpellBook.Core", new[] { At("Packages", "com.spellbook", "Runtime", "Core", "DocUrls.cs") },
                new[]
                {
                    "C:/Unity/NetStandard/ref/2.1.0/netstandard.dll",
                    "C:/Unity/UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll",
                    "C:/Unity/Managed/UnityEngine.dll",
                    "C:/Unity/NetStandard2/x.dll",
                });
            var system = new[] { "C:/Unity/NetStandard", "C:\\Unity\\UnityReferenceAssemblies\\" };

            string text = DocfxConfig.Render(new[] { unity }, system, ConfigDir);

            StringAssert.Contains("\"UnityEngine.dll\"", text);
            StringAssert.Contains("\"x.dll\"", text);
            StringAssert.DoesNotContain("netstandard.dll", text);
            StringAssert.DoesNotContain("mscorlib.dll", text);
        }

        [Test]
        public void Render_DestinationIsApiReference()
        {
            string text = DocfxConfig.Render(new[] { CoreAssembly() }, NoSystemDirectories, ConfigDir);

            StringAssert.Contains("\"dest\": \"../reference\"", text);
        }

        [Test]
        public void Render_SameInputTwice_SameText()
        {
            string first = DocfxConfig.Render(new[] { CoreAssembly(), TestsAssembly() }, NoSystemDirectories, ConfigDir);
            string second = DocfxConfig.Render(new[] { TestsAssembly(), CoreAssembly() }, NoSystemDirectories, ConfigDir);

            Assert.That(second, Is.EqualTo(first));
            StringAssert.DoesNotContain("\r", first);
        }

        [Test]
        public void Read_FromCompilationPipeline_FindsCoreSources()
        {
            DocfxAssembly core = DocfxConfig.Read().Single(a => a.Name == "SpellBook.Core");

            Assert.That(core.Sources.Select(s => s.Replace('\\', '/')),
                Has.Some.EndsWith("/Runtime/Core/DocUrls.cs"));
        }

        [Test]
        public void ReadSystemAssemblyDirectories_CoversCoreLibraryOfEveryPackageAssembly()
        {
            List<string> folders = DocfxConfig.ReadSystemAssemblyDirectories()
                .Select(d => Path.GetFullPath(d).Replace('\\', '/').TrimEnd('/') + "/").ToList();

            List<string> uncovered = CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                .Where(a => TypeCatalog.IsPackageAssembly(a.name))
                .SelectMany(a => a.allReferences)
                .Where(r => Path.GetFileName(r) == "mscorlib.dll" || Path.GetFileName(r) == "netstandard.dll")
                .Select(r => Path.GetFullPath(r).Replace('\\', '/'))
                .Where(r => !folders.Any(d => r.StartsWith(d, StringComparison.OrdinalIgnoreCase)))
                .Distinct()
                .ToList();

            Assert.That(uncovered, Is.Empty);
        }

        private static DocfxAssembly CoreAssembly()
        {
            return new DocfxAssembly("SpellBook.Core",
                new[]
                {
                    At("Packages", "com.spellbook", "Runtime", "Core", "DocUrls.cs"),
                    At("Packages", "com.spellbook", "Runtime", "Core", "Inspector", "TypePickerAttribute.cs"),
                },
                new[] { Unity("UnityEngine.CoreModule.dll"), Unity("UnityEngine.IMGUIModule.dll") });
        }

        private static DocfxAssembly TestsAssembly()
        {
            return new DocfxAssembly("SpellBook.Core.Tests.EditMode",
                new[] { At("Packages", "com.spellbook", "Tests", "Core", "EditMode", "ManualLinksTests.cs") },
                new[] { At("Library", "PackageCache", "nunit.framework.dll"), At("Library", "ScriptAssemblies", "SpellBook.Core.dll") });
        }

        private static string At(params string[] parts)
        {
            return Path.Combine(new[] { Root }.Concat(parts).ToArray());
        }

        private static string Unity(string dll)
        {
            return Path.Combine(Path.GetPathRoot(Root), "Unity", "Editor", "Data", "Managed", "UnityEngine", dll);
        }
    }
}
