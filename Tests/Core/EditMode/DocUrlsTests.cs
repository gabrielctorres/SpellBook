using System;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Tests.Structure;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// URL única do manual (ADR 0007 e 0025; CA37) e versão mínima da Unity no <c>package.json</c> (ADR 0010; CA4, CA42).
    /// </summary>
    public class DocUrlsTests
    {
        private const string SiteUrl = "https://gabrielctorres.github.io/SpellBook/";

        [Test]
        public void DocUrls_BaseMatchesSite()
        {
            string result = DocUrls.Base;

            Assert.That(result, Is.EqualTo(SiteUrl));
        }

        [TestCase("Install", "inicio/instalacao/")]
        [TestCase("Concepts", "inicio/conceitos/")]
        [TestCase("Migration", "regras/migracao-2-para-3/")]
        [TestCase("Time", "modulos/tempo/")]
        [TestCase("Events", "modulos/eventos/")]
        [TestCase("Pooling", "modulos/pooling/")]
        [TestCase("Tags", "modulos/tags/")]
        [TestCase("Gas", "modulos/gas/")]
        [TestCase("Validator", "ferramentas/validador/")]
        [TestCase("EventDebugger", "ferramentas/event-debugger/")]
        public void DocUrls_PageConstantPointsToPageUnderBase(string constantName, string pagePath)
        {
            FieldInfo field = typeof(DocUrls).GetField(constantName, BindingFlags.Public | BindingFlags.Static);

            Assert.That(field, Is.Not.Null, $"DocUrls.{constantName} não existe.");
            Assert.That(field.IsLiteral, Is.True, $"DocUrls.{constantName} deve ser const.");
            Assert.That(field.GetRawConstantValue(), Is.EqualTo(DocUrls.Base + pagePath));
        }

        [Test]
        public void DocUrls_BaseMatchesPackageDocumentationUrl()
        {
            PackageManifest manifest = ReadManifest();

            Assert.That(manifest.documentationUrl, Is.EqualTo(DocUrls.Base));
        }

        [Test]
        public void Package_DeclaresUnity6000_0()
        {
            PackageManifest manifest = ReadManifest();

            Assert.That(manifest.unity, Is.EqualTo("6000.0"));
        }

        private static PackageManifest ReadManifest()
        {
            return JsonUtility.FromJson<PackageManifest>(PackageFiles.ReadText("package.json"));
        }

        /// <summary>Campos do <c>package.json</c> lidos aqui (nomes iguais aos do JSON).</summary>
        [Serializable]
        private sealed class PackageManifest
        {
            public string unity = string.Empty;
            public string documentationUrl = string.Empty;
        }
    }
}
