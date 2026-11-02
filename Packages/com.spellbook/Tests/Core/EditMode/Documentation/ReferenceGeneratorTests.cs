using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>Geração do catálogo, da configuração do DocFX e da impressão digital (Spec referencia-catalogo-e-api, D2; RC4).</summary>
    public class ReferenceGeneratorTests
    {
        private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "spellbook-reference-generator"));
        private static readonly string Docs = Path.Combine(Root, "Documentation~");
        private static readonly string Catalog = Path.Combine(Docs, "manual", "referencia", "catalogo.md");
        private static readonly string Config = Path.Combine(Docs, "api", "obj", "docfx.metadata.json");
        private static readonly string Fingerprint = Path.Combine(Docs, "api", "reference.sha256");

        [SetUp]
        public void SetUp()
        {
            DeleteRoot();
            Directory.CreateDirectory(Path.Combine(Docs, ".config"));
            File.WriteAllText(Path.Combine(Docs, ".config", "dotnet-tools.json"), "{ \"tools\": {} }\n");
        }

        [TearDown]
        public void TearDown()
        {
            DeleteRoot();
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
        public void Generate_Twice_SameBytes()
        {
            ReferenceGenerator.Generate(Docs, new FakeRunner());
            byte[][] first = { File.ReadAllBytes(Catalog), File.ReadAllBytes(Config), File.ReadAllBytes(Fingerprint) };

            ReferenceGenerator.Generate(Docs, new FakeRunner());

            Assert.That(File.ReadAllBytes(Catalog), Is.EqualTo(first[0]));
            Assert.That(File.ReadAllBytes(Config), Is.EqualTo(first[1]));
            Assert.That(File.ReadAllBytes(Fingerprint), Is.EqualTo(first[2]));
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

        private static void DeleteRoot()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        private sealed class FakeRunner : IDocfxMetadataRunner
        {
            public List<string> Calls { get; } = new List<string>();

            public Exception Failure { get; set; }

            public void Run(string documentationRoot, string metadataConfigPath)
            {
                Calls.Add(documentationRoot + " | " + metadataConfigPath);
                if (Failure != null)
                {
                    throw Failure;
                }
            }
        }
    }
}
