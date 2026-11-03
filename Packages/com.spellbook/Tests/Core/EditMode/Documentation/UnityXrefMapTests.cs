using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor.Documentation;
using SpellBook.Core.Tests.Structure;

namespace SpellBook.Core.Tests.Documentation
{
    /// <summary>Mapa de referências cruzadas da API para a documentação da Unity (Spec referencia-catalogo-e-api, D8; XR1).</summary>
    public class UnityXrefMapTests
    {
        private const string ScriptReference = "https://docs.unity3d.com/ScriptReference/";

        private static readonly string Root = TestScratch.For("unity-xrefmap");

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
        public void Read_OnlyExternalReferences()
        {
            WriteYaml("A.yml",
                "### YamlMime:ManagedReference\n" +
                "items:\n" +
                "- uid: SpellBook.A\n" +
                "  commentId: T:SpellBook.A\n" +
                "  name: A\n" +
                "references:\n" +
                "- uid: SpellBook.B\n" +
                "  commentId: T:SpellBook.B\n" +
                "  name: B\n" +
                "  spec.csharp:\n" +
                "  - uid: UnityEngine.Object\n" +
                "    name: Object\n" +
                "    isExternal: true\n" +
                "- uid: UnityEngine\n" +
                "  commentId: N:UnityEngine\n" +
                "  isExternal: true\n" +
                "  name: UnityEngine\n" +
                "- uid: UnityEngine.GameObject\n" +
                "  commentId: T:UnityEngine.GameObject\n" +
                "  parent: UnityEngine\n" +
                "  isExternal: true\n" +
                "  name: GameObject\n");

            var references = UnityXrefMap.ReadExternalReferences(Root);

            Assert.That(references.Select(r => r.Uid), Is.EqualTo(new[] { "UnityEngine.GameObject" }));
            Assert.That(references[0].CommentId, Is.EqualTo("T:UnityEngine.GameObject"));
            Assert.That(references[0].Name, Is.EqualTo("GameObject"));
        }

        [Test]
        public void Href_EngineType_LatestScriptReference()
        {
            Assert.That(Href("T:UnityEngine.GameObject"), Is.EqualTo(ScriptReference + "GameObject.html"));
        }

        [Test]
        public void Href_EditorType_DropsUnityEditorPrefix()
        {
            Assert.That(Href("T:UnityEditor.Editor"), Is.EqualTo(ScriptReference + "Editor.html"));
        }

        [Test]
        public void Href_NestedNamespace_KeepsSubNamespace()
        {
            Assert.That(Href("T:UnityEngine.UIElements.VisualElement"), Is.EqualTo(ScriptReference + "UIElements.VisualElement.html"));
        }

        [Test]
        public void Href_NestedType_UsesDot()
        {
            Assert.That(Href("T:UnityEngine.ParticleSystem.MainModule"), Is.EqualTo(ScriptReference + "ParticleSystem.MainModule.html"));
        }

        [Test]
        public void Href_GenericType_UsesUnderscoreArity()
        {
            Assert.That(Href("T:UnityEngine.Events.UnityEvent`1"), Is.EqualTo(ScriptReference + "Events.UnityEvent_1.html"));
        }

        [Test]
        public void Href_Method_UsesDot()
        {
            Assert.That(Href("M:UnityEngine.Component.GetComponents(System.Type,System.Collections.Generic.List{UnityEngine.Component})"),
                Is.EqualTo(ScriptReference + "Component.GetComponents.html"));
            Assert.That(Href("M:UnityEditor.EditorWindow.GetWindow``1(System.Boolean)"),
                Is.EqualTo(ScriptReference + "EditorWindow.GetWindow.html"));
        }

        [Test]
        public void Href_Property_UsesHyphen()
        {
            Assert.That(Href("P:UnityEngine.GameObject.transform"), Is.EqualTo(ScriptReference + "GameObject-transform.html"));
            Assert.That(Href("E:UnityEditor.EditorWindow.windowFocusChanged"),
                Is.EqualTo(ScriptReference + "EditorWindow-windowFocusChanged.html"));
        }

        [Test]
        public void Href_Constructor_UsesCtor()
        {
            Assert.That(Href("M:UnityEngine.GameObject.#ctor(System.String)"), Is.EqualTo(ScriptReference + "GameObject-ctor.html"));
        }

        [Test]
        public void Href_IndexerAndEnumValue_PointToType()
        {
            Assert.That(Href("P:UnityEngine.UIElements.VisualElement.Item(System.Int32)"),
                Is.EqualTo(ScriptReference + "UIElements.VisualElement.html"));
            Assert.That(Href("F:UnityEngine.HideFlags.HideAndDontSave"), Is.EqualTo(ScriptReference + "HideFlags.html"));
        }

        [Test]
        public void PackageApiHref_UsesMajorMinor()
        {
            Assert.That(UnityXrefMap.PackageApiHref("com.unity.ugui", "2.0.0", "UnityEngine.UI.Button"),
                Is.EqualTo("https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.Button.html"));
        }

        [Test]
        public void PackageApiHref_DropsPrereleaseAndPatch()
        {
            const string expected = "https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/api/A.B.html";

            Assert.That(UnityXrefMap.PackageApiHref("com.unity.test-framework", "1.6.0-pre.1", "A.B"), Is.EqualTo(expected));
            Assert.That(UnityXrefMap.PackageApiHref("com.unity.test-framework", "1.6.12", "A.B"), Is.EqualTo(expected));
        }

        [Test]
        public void PackageApiHref_GenericType_UsesHyphenArity()
        {
            Assert.That(UnityXrefMap.PackageApiHref("com.unity.inputsystem", "1.14.0", "UnityEngine.InputSystem.InputControl`1"),
                Is.EqualTo("https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/api/UnityEngine.InputSystem.InputControl-1.html"));
        }

        [TestCase("latest")]
        [TestCase("")]
        public void PackageApiHref_InvalidVersion_Throws(string version)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => UnityXrefMap.PackageApiHref("com.unity.ugui", version, "UnityEngine.UI.Button"));

            StringAssert.Contains("UnityEngine.UI.Button", exception.Message);
            StringAssert.Contains($"'{version}'", exception.Message);
        }

        [Test]
        public void Href_PackageType_UsesInstalledVersion()
        {
            Assert.That(Href("T:UnityEngine.TestTools.UnityTestAttribute"), Does.Match(
                @"^https://docs\.unity3d\.com/Packages/com\.unity\.test-framework@\d+\.\d+/api/UnityEngine\.TestTools\.UnityTestAttribute\.html$"));
        }

        [Test]
        public void Href_BuiltInModuleType_UsesScriptReference()
        {
            Assert.That(Href("T:UnityEngine.Rigidbody"), Is.EqualTo(ScriptReference + "Rigidbody.html"));
        }

        [TestCase("T:System.String")]
        [TestCase("T:System.Collections.Generic.List`1")]
        public void Href_SystemType_IsNull(string commentId)
        {
            Assert.That(Href(commentId), Is.Null);
        }

        [Test]
        public void Render_SystemReference_NotInMap()
        {
            WriteYaml("A.yml",
                "references:\n" +
                External("System.Object", "T:System.Object", "object", "https://learn.microsoft.com/dotnet/api/system.object") +
                External("UnityEngine.GameObject", "T:UnityEngine.GameObject", "GameObject"));

            string map = UnityXrefMap.Render(Root);

            StringAssert.DoesNotContain("System.Object", map);
            StringAssert.Contains("- uid: UnityEngine.GameObject\n", map);
        }

        [Test]
        public void Render_UnresolvedUnityReference_Throws()
        {
            WriteYaml("A.yml",
                "references:\n" +
                External("UnityEngine.TipoQueNaoExiste", "T:UnityEngine.TipoQueNaoExiste", "TipoQueNaoExiste") +
                External("UnityEngine.GameObject", "T:UnityEngine.GameObject", "GameObject"));

            var exception = Assert.Throws<InvalidOperationException>(() => UnityXrefMap.Render(Root));

            StringAssert.Contains("UnityEngine.TipoQueNaoExiste", exception.Message);
            StringAssert.DoesNotContain("UnityEngine.GameObject", exception.Message);
        }

        [Test]
        public void Render_SameInputTwice_SameText()
        {
            WriteYaml("B.yml", "references:\n" + External("UnityEngine.Transform", "T:UnityEngine.Transform", "Transform"));
            WriteYaml("A.yml", "references:\n" + External("UnityEngine.GameObject", "T:UnityEngine.GameObject", "GameObject"));

            Assert.That(UnityXrefMap.Render(Root), Is.EqualTo(UnityXrefMap.Render(Root)));
        }

        [Test]
        public void Render_SortedByUidWithXrefHeader()
        {
            WriteYaml("A.yml",
                "references:\n" +
                External("UnityEngine.Transform", "T:UnityEngine.Transform", "Transform") +
                External("UnityEngine.GameObject.#ctor(System.String)", "M:UnityEngine.GameObject.#ctor(System.String)", "GameObject(string)"));
            WriteYaml("B.yml",
                "references:\n" +
                External("UnityEngine.GameObject", "T:UnityEngine.GameObject", "GameObject") +
                External("UnityEngine.Transform", "T:UnityEngine.Transform", "Transform"));

            string map = UnityXrefMap.Render(Root);

            Assert.That(map, Is.EqualTo(
                "### YamlMime:XRefMap\n" +
                "sorted: true\n" +
                "references:\n" +
                "- uid: UnityEngine.GameObject\n" +
                "  name: GameObject\n" +
                "  href: " + ScriptReference + "GameObject.html\n" +
                "- uid: UnityEngine.GameObject.#ctor(System.String)\n" +
                "  name: GameObject(string)\n" +
                "  href: " + ScriptReference + "GameObject-ctor.html\n" +
                "- uid: UnityEngine.Transform\n" +
                "  name: Transform\n" +
                "  href: " + ScriptReference + "Transform.html\n"));
        }

        private static string Href(string commentId)
        {
            string uid = commentId.Substring(2);
            return UnityXrefMap.ResolveHref(new ExternalReference(uid, commentId, uid));
        }

        private static string External(string uid, string commentId, string name, string href = null)
        {
            return $"- uid: {uid}\n  commentId: {commentId}\n  isExternal: true\n" +
                (href == null ? string.Empty : $"  href: {href}\n") +
                $"  name: {name}\n";
        }

        private static void WriteYaml(string fileName, string text)
        {
            File.WriteAllText(Path.Combine(Root, fileName), text);
        }
    }
}
