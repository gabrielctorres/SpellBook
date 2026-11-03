using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>
    /// As guardas do manual e da referência ficam todas na categoria <see cref="TestCategories.Documentation"/>, e a guarda
    /// estrutural do manual fica fora dela (Spec <c>processo-doc-no-fim.md</c>, PD1).
    /// </summary>
    public class DocumentationCategoryTests
    {
        private static readonly (Type Type, string Method)[] Guards =
        {
            (typeof(ReferenceUpToDateTests), nameof(ReferenceUpToDateTests.CommittedCatalog_MatchesGenerated)),
            (typeof(ReferenceUpToDateTests), nameof(ReferenceUpToDateTests.CommittedFingerprint_MatchesSources)),
            (typeof(ReferenceUpToDateTests), nameof(ReferenceUpToDateTests.CommittedReference_HasNoMachinePathOrForeignBranch)),
            (typeof(ReferenceUpToDateTests), nameof(ReferenceUpToDateTests.CommittedXrefMap_MatchesCommittedReference)),
            (typeof(ReferenceUpToDateTests), nameof(ReferenceUpToDateTests.EveryUnityReference_HasXrefEntry)),
            (typeof(ManualCodeExamplesTests), nameof(ManualCodeExamplesTests.NoHandWrittenCSharpBlocks)),
            (typeof(ManualCodeExamplesTests), nameof(ManualCodeExamplesTests.SnippetsPointToExistingSections)),
            (typeof(ManualCodeExamplesTests), nameof(ManualCodeExamplesTests.SnippetSourcesAreCompiledCode)),
            (typeof(ManualLinksTests), nameof(ManualLinksTests.EveryHelpUrlHasPageAndAnchor)),
            (typeof(ManualLinksTests), nameof(ManualLinksTests.EveryDocUrlHasPage)),
            (typeof(ManualLinksTests), nameof(ManualLinksTests.NoStubPages)),
        };

        [Test]
        public void DocumentationCategory_HoldsEveryManualAndReferenceGuard()
        {
            List<string> outside = Guards
                .Where(g => !HasDocumentationCategory(g.Type, g.Method))
                .Select(g => $"{g.Type.Name}.{g.Method}")
                .ToList();

            bool structuralInside = HasDocumentationCategory(
                typeof(ManualLinksTests), nameof(ManualLinksTests.PackageFiles_RepositoryRoot_HasManual));

            Assert.That(outside, Is.Empty,
                $"guardas sem [Category(\"{TestCategories.Documentation}\")]:\n" + string.Join("\n", outside));
            Assert.That(structuralInside, Is.False,
                $"PackageFiles_RepositoryRoot_HasManual é estrutural e não pode ter a categoria {TestCategories.Documentation}");
        }

        private static bool HasDocumentationCategory(Type type, string methodName)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} não existe");
            Assert.That(method.GetCustomAttribute<TestAttribute>(), Is.Not.Null, $"{type.Name}.{methodName} não é [Test]");

            return method.GetCustomAttributes<CategoryAttribute>(true)
                .Concat(type.GetCustomAttributes<CategoryAttribute>(true))
                .Any(c => c.Name == TestCategories.Documentation);
        }
    }
}
