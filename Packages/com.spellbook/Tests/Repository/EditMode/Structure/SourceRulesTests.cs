using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Regras sobre os fontes e os tipos do pacote. Os três sistemas de eventos da 2.x
    /// foram removidos (CA25); <c>Void</c> e <c>RuntimeSet&lt;T&gt;</c> ficam no namespace do Core.
    /// Fora do módulo Pooling, ninguém usa o singleton do pool (CA30; ADR 0015).
    /// </summary>
    public class SourceRulesTests
    {
        private static readonly string[] OldEventFolders =
        {
            "Runtime/Core/Events",
            "Runtime/Core/NewEventSystem",
            "Runtime/Systems/Events",
        };

        private static readonly string[] OldEventTypeNames = { "GameEventListner", "EventManager", "GameEvent" };

        // Montado por partes: este arquivo também é lido pela regra.
        private const string PoolSingleton = "PoolManager" + ".Instance";

        private static readonly string[] ScannedFolders = { "Runtime", "Editor", "Tests" };

        [Test]
        public void OldEventFoldersDoNotExist()
        {
            string root = PackageFiles.Root;

            List<string> existing = OldEventFolders
                .Where(folder => Directory.Exists(Path.Combine(root, folder)))
                .ToList();

            Assert.That(existing, Is.Empty);
        }

        [Test]
        public void NoTypeNamed_GameEventListner_EventManager_GameEvent()
        {
            List<string> found = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("SpellBook.", StringComparison.Ordinal))
                .SelectMany(a => a.GetTypes())
                .Where(t => OldEventTypeNames.Contains(t.Name))
                .Select(t => t.AssemblyQualifiedName)
                .ToList();

            Assert.That(found, Is.Empty);
        }

        [Test]
        public void NoPoolManagerInstanceOutsidePooling()
        {
            List<string> violations = ScannedFolders
                .SelectMany(folder => PackageFiles.Find(folder, ".cs"))
                .Where(path => !IsInPoolingModule(path))
                .Where(path => PackageFiles.ReadText(path).Contains(PoolSingleton))
                .ToList();

            Assert.That(violations, Is.Empty);
        }

        [TestCase("SpellBook.Core.Void")]
        [TestCase("SpellBook.Core.RuntimeSet`1")]
        public void CoreUtility_IsInCoreNamespace(string fullName)
        {
            Type type = typeof(DocUrls).Assembly.GetType(fullName);

            Assert.That(type, Is.Not.Null, $"{fullName} não existe no assembly SpellBook.Core.");
        }

        /// <summary>Uma pasta <c>Pooling</c> no caminho: hoje <c>Runtime/Systems/Pooling</c>, a partir do 0M-23 <c>Runtime/Pooling</c>.</summary>
        private static bool IsInPoolingModule(string relativePath)
        {
            return relativePath.Split('/').Contains("Pooling");
        }
    }
}
