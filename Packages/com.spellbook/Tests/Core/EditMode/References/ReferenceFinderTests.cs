using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellBook.Core.Tests.References
{
    /// <summary>
    /// Busca de referências genérica (CA9 do menu de Editor do GAS): quem usa um objeto nos assets, nos prefabs e nas
    /// cenas abertas, e o que ele usa. Os assets ficam numa pasta temporária apagada no TearDown.
    /// </summary>
    public class ReferenceFinderTests
    {
        private const string Folder = "Assets/__SpellBookReferenceTemp";
        private const string TargetPath = Folder + "/Alvo.asset";

        private readonly List<Object> _sceneObjects = new List<Object>();
        private ReferenceHolderAsset _target;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__SpellBookReferenceTemp");
            }

            _target = ScriptableObject.CreateInstance<ReferenceHolderAsset>();
            AssetDatabase.CreateAsset(_target, TargetPath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _sceneObjects.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _sceneObjects.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void FindUsers_AssetAndPrefab_OnePerField_WithLocation()
        {
            const string assetPath = Folder + "/Usuario.asset";
            const string prefabPath = Folder + "/Usuario.prefab";
            ReferenceHolderAsset holder = CreateHolder(assetPath, _target);
            var prefabRoot = new GameObject("Usuario");
            prefabRoot.AddComponent<ReferenceHolderComponent>().Target = _target;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Object.DestroyImmediate(prefabRoot);
            AssetDatabase.SaveAssets();

            List<ReferenceUser> users = ReferenceFinder.FindUsers(_target);

            Assert.That(users, Has.Count.EqualTo(2));
            ReferenceUser asset = users.Single(u => u.Location == assetPath);
            Assert.That(asset.Owner, Is.SameAs(holder));
            Assert.That(asset.PropertyPath, Is.EqualTo("target"));
            ReferenceUser prefab = users.Single(u => u.Location == prefabPath);
            Assert.That(prefab.Owner, Is.InstanceOf<ReferenceHolderComponent>());
            Assert.That(prefab.PropertyPath, Is.EqualTo("target"));
        }

        [Test]
        public void FindUsers_InsideSerializeReference_ReportsPath()
        {
            const string assetPath = Folder + "/Lista.asset";
            ReferenceHolderAsset holder = CreateHolder(assetPath, null);
            holder.slot = new ReferenceSlot { target = _target };
            EditorUtility.SetDirty(holder);
            AssetDatabase.SaveAssets();

            List<ReferenceUser> users = ReferenceFinder.FindUsers(_target);

            Assert.That(users, Has.Count.EqualTo(1));
            Assert.That(users[0].Owner, Is.SameAs(holder));
            Assert.That(users[0].PropertyPath, Is.EqualTo("slot.target"));
            Assert.That(users[0].Location, Is.EqualTo(assetPath));
        }

        [Test]
        public void FindUsers_OpenScene_LocationIsScene()
        {
            var owner = new GameObject("Painel");
            _sceneObjects.Add(owner);
            var component = owner.AddComponent<ReferenceHolderComponent>();
            component.Target = _target;

            List<ReferenceUser> users = ReferenceFinder.FindUsers(_target);

            Assert.That(users, Has.Count.EqualTo(1));
            Assert.That(users[0].Owner, Is.SameAs(component));
            Assert.That(users[0].Location, Is.EqualTo(owner.scene.path.Length > 0 ? owner.scene.path : owner.scene.name));
        }

        [Test]
        public void FindUsers_ClosedScene_NotIncluded()
        {
            const string scenePath = Folder + "/Fechada.unity";
            // Cópia da cena aberta com o usuário, salva no disco e não aberta; depois o usuário sai da cena aberta.
            var owner = new GameObject("Painel");
            _sceneObjects.Add(owner);
            owner.AddComponent<ReferenceHolderComponent>().Target = _target;
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath, true), Is.True);
            Object.DestroyImmediate(owner);
            Assert.That(AssetDatabase.GetDependencies(scenePath, false), Does.Contain(TargetPath), "A cena fechada não referencia o alvo.");

            List<ReferenceUser> users = ReferenceFinder.FindUsers(_target);

            Assert.That(users, Is.Empty);
        }

        [Test]
        public void FindUsers_Null_Empty()
        {
            Assert.That(ReferenceFinder.FindUsers(null), Is.Empty);
        }

        [Test]
        public void FindUses_ReferencedObjects_OnceEach_WithoutScriptOrSelf()
        {
            ReferenceHolderAsset other = CreateHolder(Folder + "/Outro.asset", null);
            ReferenceHolderAsset holder = CreateHolder(Folder + "/Usa.asset", other);
            holder.slot = new ReferenceSlot { target = holder };
            EditorUtility.SetDirty(holder);
            ReferenceHolderAsset twice = CreateHolder(Folder + "/Duas.asset", other);
            twice.slot = new ReferenceSlot { target = other };
            EditorUtility.SetDirty(twice);
            AssetDatabase.SaveAssets();

            Assert.That(ReferenceFinder.FindUses(holder), Is.EqualTo(new Object[] { other }));
            Assert.That(ReferenceFinder.FindUses(twice), Is.EqualTo(new Object[] { other }));
            Assert.That(ReferenceFinder.FindUses(null), Is.Empty);
        }

        [Test]
        public void Reveal_SelectsTarget()
        {
            Object previous = Selection.activeObject;
            try
            {
                Selection.activeObject = null;

                ReferenceFinder.Reveal(_target);

                Assert.That(Selection.activeObject, Is.SameAs(_target));
            }
            finally
            {
                Selection.activeObject = previous;
            }
        }

        private static ReferenceHolderAsset CreateHolder(string path, Object target)
        {
            var holder = ScriptableObject.CreateInstance<ReferenceHolderAsset>();
            holder.target = target;
            AssetDatabase.CreateAsset(holder, path);
            return holder;
        }
    }
}
