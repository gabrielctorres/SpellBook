using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Geração do catálogo, da configuração do DocFX, do mapa da Unity e da impressão digital
    /// (Spec referencia-catalogo-e-api, D2 e D8; RC4 e XR2).
    /// </summary>
    public class ReferenceGeneratorTests
    {
        private static readonly string Root = TestScratch.For("reference-generator");
        private static readonly string Docs = Path.Combine(Root, "Documentation~");
        private static readonly string Catalog = Path.Combine(Docs, "manual", "referencia", "catalogo.md");
        private static readonly string Config = Path.Combine(Docs, "api", "obj", "docfx.metadata.json");
        private static readonly string Fingerprint = Path.Combine(Docs, "api", "reference.sha256");
        private static readonly string XrefMap = Path.Combine(Docs, "api", "unity.xrefmap.yml");
        private static readonly string ReferenceDir = Path.Combine(Docs, "api", "reference");

        [SetUp]
        public void SetUp()
        {
            TestScratch.Recreate(Root);
            Directory.CreateDirectory(Path.Combine(Docs, ".config"));
            File.WriteAllText(Path.Combine(Docs, ".config", "dotnet-tools.json"), "{ \"tools\": {} }\n");
        }

        [TearDown]
        public void TearDown()
        {
            TestScratch.Delete(Root);
        }

        [Test]
        public void Generate_WritesCatalogConfigAndFingerprint()
        {
            var runner = new FakeRunner();

            ReferenceGenerator.Generate(Docs, runner);

            Assert.That(File.ReadAllText(Catalog), Is.EqualTo(TypeCatalogPage.Render(TypeCatalog.Build())));
            StringAssert.Contains("\"dest\": \"../reference\"", File.ReadAllText(Config));
            Assert.That(File.ReadAllText(Fingerprint), Is.EqualTo(ReferenceFingerprint.Compute(Root) + "\n"));
            Assert.That(runner.Calls, Is.EqualTo(new[] { Docs + " | " + Config }));
        }

        [Test]
        public void Generate_RunnerFails_NoFingerprint()
        {
            var runner = new FakeRunner { Failure = new InvalidOperationException("docfx saiu com 1") };

            var thrown = Assert.Throws<InvalidOperationException>(() => ReferenceGenerator.Generate(Docs, runner));

            Assert.That(thrown.Message, Is.EqualTo("docfx saiu com 1"));
            Assert.That(File.Exists(Fingerprint), Is.False);
        }

        [Test]
        public void Generate_WritesUnityXrefMap()
        {
            ReferenceGenerator.Generate(Docs, new FakeRunner());

            string map = File.ReadAllText(XrefMap);
            Assert.That(map, Is.EqualTo(UnityXrefMap.Render(ReferenceDir)));
            StringAssert.Contains("- uid: UnityEngine.GameObject\n", map);
            StringAssert.Contains("  href: https://docs.unity3d.com/ScriptReference/GameObject.html\n", map);
            StringAssert.DoesNotContain("System.String", map);
        }

        [Test]
        public void Generate_RunnerFails_NoXrefMap()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(XrefMap));
            File.WriteAllText(XrefMap, "mapa da geração anterior\n");
            var runner = new FakeRunner { Failure = new InvalidOperationException("docfx saiu com 1") };

            Assert.Throws<InvalidOperationException>(() => ReferenceGenerator.Generate(Docs, runner));

            Assert.That(File.Exists(XrefMap), Is.False);
        }

        [Test]
        public void Generate_Twice_SameBytes()
        {
            ReferenceGenerator.Generate(Docs, new FakeRunner());
            byte[][] first =
            {
                File.ReadAllBytes(Catalog), File.ReadAllBytes(Config), File.ReadAllBytes(Fingerprint), File.ReadAllBytes(XrefMap),
            };

            ReferenceGenerator.Generate(Docs, new FakeRunner());

            Assert.That(File.ReadAllBytes(Catalog), Is.EqualTo(first[0]));
            Assert.That(File.ReadAllBytes(Config), Is.EqualTo(first[1]));
            Assert.That(File.ReadAllBytes(Fingerprint), Is.EqualTo(first[2]));
            Assert.That(File.ReadAllBytes(XrefMap), Is.EqualTo(first[3]));
        }

        [Test]
        public void Fingerprint_ChangesWhenSourceChanges()
        {
            string source = WriteSource("class A { }\n");
            string before = ReferenceFingerprint.Compute(Root, new[] { source }, "6000.3.13f1");

            File.WriteAllText(source, "class A { int b; }\n");
            string after = ReferenceFingerprint.Compute(Root, new[] { source }, "6000.3.13f1");

            Assert.That(before, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(after, Is.Not.EqualTo(before));
        }

        [Test]
        public void Fingerprint_IgnoresLineEndings()
        {
            string source = WriteSource("class A\n{\n}\n");
            string lf = ReferenceFingerprint.Compute(Root, new[] { source }, "6000.3.13f1");

            File.WriteAllText(source, "class A\r\n{\r\n}\r\n");
            string crlf = ReferenceFingerprint.Compute(Root, new[] { source }, "6000.3.13f1");

            Assert.That(crlf, Is.EqualTo(lf));
        }

        private static string WriteSource(string content)
        {
            string path = Path.Combine(Root, "Packages", "com.spellbook", "Runtime", "A.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
            return path;
        }

        /// <summary>Faz o papel do <c>docfx metadata</c>: escreve um YAML com uma referência da Unity e uma do .NET.</summary>
        private sealed class FakeRunner : IDocfxMetadataRunner
        {
            private const string Yaml =
                "### YamlMime:ManagedReference\n" +
                "items:\n" +
                "- uid: SpellBook.A\n" +
                "  commentId: T:SpellBook.A\n" +
                "references:\n" +
                "- uid: UnityEngine.GameObject\n" +
                "  commentId: T:UnityEngine.GameObject\n" +
                "  isExternal: true\n" +
                "  name: GameObject\n" +
                "- uid: System.String\n" +
                "  commentId: T:System.String\n" +
                "  isExternal: true\n" +
                "  href: https://learn.microsoft.com/dotnet/api/system.string\n" +
                "  name: string\n";

            public List<string> Calls { get; } = new List<string>();

            public Exception Failure { get; set; }

            public void Run(string documentationRoot, string metadataConfigPath)
            {
                Calls.Add(documentationRoot + " | " + metadataConfigPath);
                if (Failure != null)
                {
                    throw Failure;
                }

                string folder = Path.Combine(documentationRoot, "api", "reference");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "SpellBook.A.yml"), Yaml);
            }
        }
    }
}
