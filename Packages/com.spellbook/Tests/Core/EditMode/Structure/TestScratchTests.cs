using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Pasta de trabalho dos testes dentro do <c>Temp/</c> do projeto host, e guarda contra as pastas
    /// do sistema (Spec referencia-catalogo-e-api, emenda de 2026-11-02; XR3).
    /// </summary>
    public class TestScratchTests
    {
        // Montados por partes: nenhum literal deste arquivo pode casar com a guarda, que também o lê.
        private static readonly string[] SystemFolderApis =
        {
            "Path.GetTemp" + "Path",
            "Path.GetTemp" + "FileName",
            "Environment.GetFolder" + "Path",
            "Application.temporary" + "CachePath",
            "Application.persistent" + "DataPath",
        };

        private static readonly string HostA = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string HostB => TestScratch.For(HostA, "disputa-host-b");
        private static readonly string Outside = Path.Combine(HostA, "Temp", "SpellBookTests-fora");

        [TearDown]
        public void TearDown()
        {
            TestScratch.Delete(TestScratch.For(HostA, "disputa"));
            TestScratch.Delete(HostB);
            if (Directory.Exists(Outside))
            {
                Directory.Delete(Outside, true);
            }
        }

        [Test]
        public void NoTestUsesSystemTempFolders()
        {
            List<string> violations = PackageFiles.Find("Tests", ".cs")
                .Where(path => SystemFolderApis.Any(PackageFiles.ReadText(path).Contains))
                .ToList();

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void Recreate_OtherHostSameName_KeepsThisHostFiles()
        {
            string mine = TestScratch.Recreate(TestScratch.For(HostA, "disputa"));
            string file = Path.Combine(mine, "a.txt");
            File.WriteAllText(file, "host A\n");

            TestScratch.Recreate(TestScratch.For(HostB, "disputa"));

            Assert.That(File.ReadAllText(file), Is.EqualTo("host A\n"));
        }

        [Test]
        public void For_SameNameInTwoHosts_DifferentFolders()
        {
            string a = TestScratch.For("C:/wt-a", "x");
            string b = TestScratch.For("C:/wt-b", "x");

            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a, Does.StartWith(Path.GetFullPath("C:/wt-a/Temp/SpellBookTests/")));
            Assert.That(b, Does.StartWith(Path.GetFullPath("C:/wt-b/Temp/SpellBookTests/")));
        }

        [TestCase("")]
        [TestCase("..")]
        [TestCase("a/b")]
        public void For_InvalidName_Throws(string name)
        {
            Assert.Throws<ArgumentException>(() => TestScratch.For(HostA, name));
        }

        [Test]
        public void Recreate_PathOutsideScratch_Throws()
        {
            Directory.CreateDirectory(Outside);
            string file = Path.Combine(Outside, "a.txt");
            File.WriteAllText(file, "fora\n");

            Assert.Throws<InvalidOperationException>(() => TestScratch.Recreate(Outside));

            Assert.That(File.Exists(file), Is.True);
        }
    }
}
