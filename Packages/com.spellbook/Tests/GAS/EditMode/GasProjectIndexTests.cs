using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using UnityEditor;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Índice do GAS lido do projeto (ME7, CA7): acompanha a criação, a remoção e a renomeação de assets sem ser
    /// recriado, e avisa pelo evento <c>Changed</c>.
    /// </summary>
    public class GasProjectIndexTests
    {
        private const string Folder = "Assets/__GasProjectIndexTests";

        private GasProjectIndex _index;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", "__GasProjectIndexTests");
            CreateAsset<GameplayAbility>("GA_Fireball");
            CreateAsset<GameplayTagDefinition>("Ability");
            _index = new GasProjectIndex(Folder);
        }

        [TearDown]
        public void TearDown()
        {
            _index.Dispose();
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
        }

        private static T CreateAsset<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{Folder}/{name}.asset");
            return asset;
        }

        private string[] Names() => _index.Index.Entries.Select(e => e.Name).ToArray();

        [Test]
        public void Entries_ReadAssetsInFolder()
        {
            Assert.That(Names(), Is.EqualTo(new[] { "GA_Fireball", "Ability" }));
        }

        [Test]
        public void CreatedAsset_AppearsAndRaisesChanged()
        {
            Assert.That(Names(), Has.Length.EqualTo(2));
            int changed = 0;
            _index.Changed += () => changed++;

            CreateAsset<GameplayEffect>("GE_Burn");

            Assert.That(changed, Is.GreaterThan(0), "o índice não avisou a criação");
            Assert.That(Names(), Is.EqualTo(new[] { "GA_Fireball", "GE_Burn", "Ability" }));
        }

        [Test]
        public void DeletedAsset_Disappears()
        {
            Assert.That(Names(), Has.Length.EqualTo(2));

            AssetDatabase.DeleteAsset($"{Folder}/Ability.asset");

            Assert.That(Names(), Is.EqualTo(new[] { "GA_Fireball" }));
        }

        [Test]
        public void RenamedAsset_ShowsNewName()
        {
            Assert.That(Names(), Has.Length.EqualTo(2));

            string error = AssetDatabase.RenameAsset($"{Folder}/GA_Fireball.asset", "GA_Firebolt");

            Assert.That(error, Is.Empty);
            Assert.That(Names(), Is.EqualTo(new[] { "GA_Firebolt", "Ability" }));
        }
    }
}
