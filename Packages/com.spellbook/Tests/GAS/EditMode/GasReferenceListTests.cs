using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Editor;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Linhas do painel "Usa" e "Usado por" do gerenciador (ME8, CA9): uma por objeto, com a localização e o objeto que
    /// o clique destaca. Os assets ficam numa pasta temporária apagada no TearDown.
    /// </summary>
    public class GasReferenceListTests
    {
        private const string Folder = "Assets/__GasReferenceListTests";

        private GameplayTagDefinition _ability;
        private GameObject _sceneObject;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "__GasReferenceListTests");
            _ability = CreateTag("Ability", null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_sceneObject != null) Object.DestroyImmediate(_sceneObject);
            AssetDatabase.DeleteAsset(Folder);
        }

        private static GameplayTagDefinition CreateTag(string name, GameplayTagDefinition parent)
        {
            var tag = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            AssetDatabase.CreateAsset(tag, $"{Folder}/{name}.asset");
            using var serialized = new SerializedObject(tag);
            serialized.FindProperty("parent").objectReferenceValue = parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return tag;
        }

        [Test]
        public void UsedBy_AssetAndPrefab_LocationAndRevealTarget()
        {
            GameplayTagDefinition fireball = CreateTag("Ability.Fireball", _ability);
            var root = new GameObject("Holder");
            root.AddComponent<ReferenceHolderComponent>().Target = _ability;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Holder.prefab");
            Object.DestroyImmediate(root);

            List<GasReferenceRow> rows = GasReferenceList.UsedBy(_ability);

            Assert.That(rows.Select(r => (r.Target, r.Name, r.Location)), Is.EquivalentTo(new[]
            {
                ((Object)fireball, "Ability.Fireball", "Asset"),
                ((Object)prefab, "Holder", "Prefab")
            }));
        }

        [Test]
        public void UsedBy_OpenScene_LocationNamesScene()
        {
            _sceneObject = new GameObject("NaCena");
            _sceneObject.AddComponent<ReferenceHolderComponent>().Target = _ability;
            string scene = _sceneObject.scene.name.Length > 0 ? _sceneObject.scene.name : "sem título";

            List<GasReferenceRow> rows = GasReferenceList.UsedBy(_ability);

            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Target, Is.SameAs(_sceneObject));
            Assert.That(rows[0].Location, Is.EqualTo("Cena aberta: " + scene));
        }

        [Test]
        public void Uses_ListsReferencedAsset()
        {
            GameplayTagDefinition fireball = CreateTag("Ability.Fireball", _ability);

            List<GasReferenceRow> rows = GasReferenceList.Uses(fireball);

            Assert.That(rows.Select(r => (r.Target, r.Name, r.Location)), Is.EqualTo(new[]
            {
                ((Object)_ability, "Ability", "Asset")
            }));
        }
    }
}
