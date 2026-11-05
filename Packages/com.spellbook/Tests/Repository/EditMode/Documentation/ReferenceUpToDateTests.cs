using System.IO;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// Guarda do catálogo commitado: a página está em dia com o código, byte a byte (ADR 0040, item 4; ADR 0044,
    /// item 7).
    /// </summary>
    [Category(TestCategories.Documentation)]
    public class ReferenceUpToDateTests
    {
        private const string Instruction = "rode SpellBook > Documentação > Gerar catálogo e exemplos e commite o resultado";

        [Test]
        public void CommittedCatalog_MatchesGenerated()
        {
            string path = Path.Combine(PackageFiles.RepositoryRoot, "Documentation~", "manual", "referencia", "catalogo.md");
            Assert.That(File.Exists(path), Is.True, $"{path} não existe; {Instruction}");

            // O git pode trocar \n por \r\n na cópia de trabalho (core.autocrlf); o gerador escreve só \n.
            string committed = File.ReadAllText(path).Replace("\r\n", "\n");

            Assert.That(committed, Is.EqualTo(TypeCatalogPage.Render(TypeCatalog.Build())), $"catálogo desatualizado; {Instruction}");
        }
    }
}
