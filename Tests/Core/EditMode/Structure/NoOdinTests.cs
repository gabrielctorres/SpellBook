using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>
    /// Nenhum asmdef nem arquivo do pacote cita o Odin (ADR 0026, item 7; CA65).
    /// Regra de busca na Spec (0M-07): cada termo só conta no início de um identificador,
    /// sensível a maiúsculas e sem borda à direita.
    /// </summary>
    public class NoOdinTests
    {
        // Montados por partes: nenhum literal deste arquivo pode casar com a regra, que também o lê.
        private const string VendorTerm = "Sire" + "nix";
        private const string DefineTerm = "ODIN_" + "INSPECTOR";

        private static readonly Regex Forbidden = new Regex(
            @"(?<![A-Za-z0-9_])(" + VendorTerm + "|" + DefineTerm + ")");

        private static readonly string[] ScannedFolders = { "Runtime", "Editor", "Tests" };
        private static readonly string[] ScannedExtensions = { ".cs", ".asmdef", ".asmref", ".uxml", ".uss" };

        internal static bool MentionsOdin(string text)
        {
            return Forbidden.IsMatch(text);
        }

        [TestCase("using " + VendorTerm + ".OdinInspector;")]
        [TestCase(VendorTerm + ".Serialization.dll")]
        [TestCase("\"Assets/Plugins/" + VendorTerm + "/\"")]
        [TestCase("#if " + DefineTerm)]
        [TestCase(DefineTerm + "_3_1")]
        public void MentionsOdin_TermAtIdentifierStart_Matches(string text)
        {
            bool result = MentionsOdin(text);

            Assert.That(result, Is.True);
        }

        [TestCase("NoOdin_NoSourceOrPackageFileMentions" + VendorTerm)]
        [TestCase("NoOdin_NoAsmdefReferences" + VendorTerm + "OrOdinSymbol")]
        public void MentionsOdin_TermInsideIdentifier_DoesNotMatch(string text)
        {
            bool result = MentionsOdin(text);

            Assert.That(result, Is.False);
        }

        [Test]
        public void NoOdin_NoAsmdefReferencesSirenixOrOdinSymbol()
        {
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in PackageFiles.Asmdefs())
            {
                IEnumerable<string> values = asmdef.Data.references
                    .Concat(asmdef.ReferenceNames)
                    .Concat(asmdef.Data.precompiledReferences)
                    .Concat(asmdef.Data.defineConstraints)
                    .Concat(asmdef.Data.versionDefines.SelectMany(v => new[] { v.name, v.expression, v.define }));

                foreach (string value in values.Where(MentionsOdin).Distinct())
                {
                    violations.Add($"{asmdef.Path}: {value}");
                }
            }

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void NoOdin_NoSourceOrPackageFileMentionsSirenix()
        {
            IEnumerable<string> files = ScannedFolders
                .SelectMany(folder => PackageFiles.Find(folder, ScannedExtensions))
                .Append("package.json");

            List<string> violations = files
                .Where(path => MentionsOdin(PackageFiles.ReadText(path)))
                .ToList();

            Assert.That(violations, Is.Empty);
        }
    }
}
