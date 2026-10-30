using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpellBook.Core.Editor.Inspector;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>Tipo <c>[SerializeReference]</c> ausente, relatado por objeto (Spec, seção 4.10 e D6; risco 28).</summary>
    public class MissingReferenceTypesTests
    {
        [TearDown]
        public void TearDown()
        {
            MissingTypeAsset.Delete();
        }

        [Test]
        public void Get_AssetWithMissingType_ReturnsAssemblyAndFullName()
        {
            PickHolder holder = MissingTypeAsset.Create();

            IReadOnlyList<MissingTypeEntry> entries = MissingReferenceTypes.Get(holder);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].AssemblyName, Is.EqualTo(MissingTypeAsset.Assembly));
            Assert.That(entries[0].FullTypeName, Is.EqualTo(MissingTypeAsset.Namespace + "." + MissingTypeAsset.GhostClass));
        }

        [Test]
        public void Get_AfterReimport_DataStillThere()
        {
            PickHolder holder = MissingTypeAsset.Reload(MissingTypeAsset.Create());

            IReadOnlyList<MissingTypeEntry> entries = MissingReferenceTypes.Get(holder);

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].FullTypeName, Does.EndWith(MissingTypeAsset.GhostClass));
            Assert.That(MissingTypeAsset.ReadFile(), Does.Contain("class: " + MissingTypeAsset.GhostClass + ",")
                .And.Contain("Value: " + MissingTypeAsset.GhostValue));
        }

        [Test]
        public void Clear_RemovesOnlyThatReference_AndReturnsTrue()
        {
            PickHolder holder = MissingTypeAsset.Create();
            long referenceId = MissingReferenceTypes.Get(holder)[0].ReferenceId;

            bool cleared = MissingReferenceTypes.Clear(holder, referenceId);
            AssetDatabase.SaveAssets();

            Assert.That(cleared, Is.True);
            Assert.That(MissingReferenceTypes.Get(holder), Is.Empty);
            string file = MissingTypeAsset.ReadFile();
            Assert.That(file, Does.Not.Contain(MissingTypeAsset.GhostClass));
            Assert.That(file, Does.Contain("class: PickCategorized,").And.Contain("Value: " + MissingTypeAsset.KeptValue));
        }

        [Test]
        public void Get_ObjectWithoutMissingType_ReturnsEmpty()
        {
            PickHolder holder = ScriptableObject.CreateInstance<PickHolder>();
            holder.single = new PickConcrete { Value = 1 };

            try
            {
                Assert.That(MissingReferenceTypes.Get(holder), Is.Empty);
                Assert.That(MissingReferenceTypes.Get(null), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void FormatMessage_HasAssemblyAndFullName_NamespaceOptional()
        {
            var withNamespace = new MissingTypeEntry(5, "Jogo.Runtime", "Jogo.Acoes", "Espera");
            var withoutNamespace = new MissingTypeEntry(6, "Jogo.Runtime", "", "Espera");

            Assert.That(withNamespace.FullTypeName, Is.EqualTo("Jogo.Acoes.Espera"));
            Assert.That(withoutNamespace.FullTypeName, Is.EqualTo("Espera"));
            Assert.That(MissingReferenceTypes.FormatMessage(withNamespace), Is.EqualTo(
                "Tipo não encontrado: Jogo.Runtime Jogo.Acoes.Espera. Restaure o tipo para recuperar os dados, ou limpe a referência."));
            Assert.That(MissingReferenceTypes.FormatMessage(withoutNamespace), Does.StartWith("Tipo não encontrado: Jogo.Runtime Espera."));
        }
    }

    /// <summary>
    /// Asset de <see cref="PickHolder"/> em disco com uma referência cujo tipo não existe: o YAML é editado para
    /// apontar o campo <c>single</c> para <see cref="GhostClass"/>. A lista <c>items</c> fica com um tipo válido.
    /// </summary>
    internal static class MissingTypeAsset
    {
        internal const string Folder = "Assets/__SpellBookTestTemp";
        internal const string AssetPath = Folder + "/MissingTypeHolder.asset";
        internal const string GhostClass = "PickGhost";
        internal const string Namespace = "SpellBook.Core.Tests.Inspector";
        internal const string Assembly = "SpellBook.Core.Tests.EditMode";
        internal const int GhostValue = 42;
        internal const int KeptValue = 7;

        internal static PickHolder Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__SpellBookTestTemp");
            }

            PickHolder holder = ScriptableObject.CreateInstance<PickHolder>();
            holder.single = new PickConcrete { Value = GhostValue };
            holder.items.Add(new PickCategorized { Value = KeptValue });
            AssetDatabase.CreateAsset(holder, AssetPath);
            AssetDatabase.SaveAssets();

            string yaml = ReadFile();
            Assert.That(yaml, Does.Contain("class: PickConcrete,"), "O YAML não tem a referência esperada.");
            File.WriteAllText(AssetPath, yaml.Replace("class: PickConcrete,", "class: " + GhostClass + ","));
            return Reload(holder);
        }

        /// <summary>Descarrega o objeto em memória e relê o YAML; sem o unload, o load devolve o objeto antigo.</summary>
        internal static PickHolder Reload(PickHolder holder)
        {
            Resources.UnloadAsset(holder);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<PickHolder>(AssetPath);
        }

        internal static string ReadFile()
        {
            return File.ReadAllText(AssetPath);
        }

        internal static void Delete()
        {
            AssetDatabase.DeleteAsset(Folder);
        }
    }
}
