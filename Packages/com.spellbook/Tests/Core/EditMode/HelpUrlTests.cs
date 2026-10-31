using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Todo <c>MonoBehaviour</c> e <c>ScriptableObject</c> público e concreto de runtime tem <c>[HelpURL]</c> iniciado por
    /// <see cref="DocUrls.Base"/> (CA36, CA37; ADR 0007).
    /// </summary>
    public class HelpUrlTests
    {
        [Test]
        public void AllPublicConcreteMbAndSo_HaveHelpUrl()
        {
            IReadOnlyList<HelpUrlViolation> violations = HelpUrlRule.FindViolations(HelpUrlRule.RuntimeAssemblies());

            List<string> missing = violations
                .Where(v => v.Reason == HelpUrlRule.MissingReason)
                .Select(v => v.Type.FullName)
                .ToList();

            Assert.That(missing, Is.Empty, "Tipos sem [HelpURL]:\n" + string.Join("\n", missing));
        }

        [Test]
        public void AllHelpUrls_StartWithBase()
        {
            IReadOnlyList<HelpUrlViolation> violations = HelpUrlRule.FindViolations(HelpUrlRule.RuntimeAssemblies());

            List<string> outside = violations
                .Where(v => v.Reason == HelpUrlRule.OutsideBaseReason)
                .Select(v => v.Type.FullName)
                .ToList();

            Assert.That(outside, Is.Empty, "[HelpURL] fora de DocUrls.Base:\n" + string.Join("\n", outside));
        }

        [Test]
        public void RuntimeAssemblies_AreTheRuntimeModulesOnly()
        {
            List<string> names = HelpUrlRule.RuntimeAssemblies().Select(a => a.GetName().Name).ToList();

            Assert.That(names, Does.Contain("SpellBook.Core").And.Contain("SpellBook.GAS"));
            Assert.That(names.Where(n => n.Contains(".Tests") || n.Contains(".Editor") || n == "SpellBook.Testing"),
                Is.Empty);
        }

        [Test]
        public void HelpUrlRule_TypeWithoutAttribute_IsViolation()
        {
            IReadOnlyList<HelpUrlViolation> violations =
                HelpUrlRule.FindViolations(new[] { typeof(HelpUrlTests).Assembly });

            HelpUrlViolation violation = violations.SingleOrDefault(v => v.Type == typeof(NoHelpUrlTestAsset));

            Assert.That(violation.Type, Is.EqualTo(typeof(NoHelpUrlTestAsset)));
            Assert.That(violation.Reason, Is.EqualTo(HelpUrlRule.MissingReason));
        }

        [Test]
        public void HelpUrlRule_UrlOutsideBase_IsViolation()
        {
            IReadOnlyList<HelpUrlViolation> violations =
                HelpUrlRule.FindViolations(new[] { typeof(HelpUrlTests).Assembly });

            HelpUrlViolation violation = violations.SingleOrDefault(v => v.Type == typeof(ForeignHelpUrlTestBehaviour));

            Assert.That(violation.Type, Is.EqualTo(typeof(ForeignHelpUrlTestBehaviour)));
            Assert.That(violation.Reason, Is.EqualTo(HelpUrlRule.OutsideBaseReason));
        }

        [Test]
        public void HelpUrlRule_AbstractOrCorrectTypes_AreNotViolations()
        {
            IReadOnlyList<HelpUrlViolation> violations =
                HelpUrlRule.FindViolations(new[] { typeof(HelpUrlTests).Assembly });

            Assert.That(violations.Select(v => v.Type),
                Has.None.EqualTo(typeof(AbstractTestAsset)).And.None.EqualTo(typeof(DocumentedTestAsset)));
        }
    }

    public class NoHelpUrlTestAsset : ScriptableObject
    {
    }

    [HelpURL("https://example.com/foreign")]
    public class ForeignHelpUrlTestBehaviour : MonoBehaviour
    {
    }

    public abstract class AbstractTestAsset : ScriptableObject
    {
    }

    [HelpURL(DocUrls.Base + "teste/")]
    public class DocumentedTestAsset : ScriptableObject
    {
    }
}
