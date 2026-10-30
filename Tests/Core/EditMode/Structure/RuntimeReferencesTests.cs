using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SpellBook.Core.Tests.Structure
{
    /// <summary>Nenhum assembly de runtime do SpellBook referencia o UnityEditor (CA3).</summary>
    public class RuntimeReferencesTests
    {
        [Test]
        public void RuntimeReferences_NoRuntimeAsmdefReferencesUnityEditor()
        {
            IReadOnlyList<AsmdefFile> asmdefs = PackageFiles.Asmdefs();
            HashSet<string> editorOnlyNames = new HashSet<string>(
                asmdefs.Where(a => a.Kind != AsmdefKind.Runtime).Select(a => a.Data.name));
            var violations = new List<string>();

            foreach (AsmdefFile asmdef in asmdefs.Where(a => a.Kind == AsmdefKind.Runtime))
            {
                foreach (string reference in asmdef.ReferenceNames)
                {
                    bool unityEditor = reference.StartsWith("UnityEditor", StringComparison.Ordinal);
                    bool editorAssembly = reference.EndsWith(".Editor", StringComparison.Ordinal)
                        || editorOnlyNames.Contains(reference);
                    if (unityEditor || editorAssembly)
                    {
                        violations.Add($"{asmdef.Path}: o runtime {asmdef.Data.name} referencia {reference}");
                    }
                }
            }

            Assert.That(violations, Is.Empty);
        }
    }
}
