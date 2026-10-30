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

        [TestCase("SpellBook.Core.Void")]
        [TestCase("SpellBook.Core.RuntimeSet`1")]
        public void CoreUtility_IsInCoreNamespace(string fullName)
        {
            Type type = typeof(DocUrls).Assembly.GetType(fullName);

            Assert.That(type, Is.Not.Null, $"{fullName} não existe no assembly SpellBook.Core.");
        }
    }
}
