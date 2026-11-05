using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// A geração escreve só a página do catálogo, sem DocFX, API, mapa da Unity nem impressão digital
    /// (Spec migracao-gitbook, GB3; ADR 0044, itens 6 e 7).
    /// </summary>
    public class ReferenceGeneratorTests
    {
        private const string CatalogRelative = "manual/referencia/catalogo.md";

        private static readonly string Root = TestScratch.For("reference-generator");
        private static readonly string Docs = Path.Combine(Root, "Documentation~");

        [SetUp]
        public void SetUp()
        {
            TestScratch.Recreate(Root);
        }

        [TearDown]
        public void TearDown()
        {
            TestScratch.Delete(Root);
        }

        [Test]
        public void Generate_WritesCatalogFromTypePicker()
        {
            ReferenceGenerator.Generate(Docs);

            byte[] expected = new UTF8Encoding(false).GetBytes(TypeCatalogPage.Render(TypeCatalog.Build()));
            Assert.That(File.ReadAllBytes(Path.Combine(Docs, CatalogRelative)), Is.EqualTo(expected));
        }

        [Test]
        public void Generate_WritesOnlyTheCatalog()
        {
            ReferenceGenerator.Generate(Docs);

            string[] files = Directory.GetFiles(Docs, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Docs, f).Replace(Path.DirectorySeparatorChar, '/'))
                .ToArray();
            Assert.That(files, Is.EqualTo(new[] { CatalogRelative }));
        }
    }
}
