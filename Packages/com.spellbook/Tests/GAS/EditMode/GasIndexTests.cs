using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Editor;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Índice do gerenciador do GAS sem UI (ME7, CA7 e CA8): lista habilidades, efeitos, atributos e tags, conta por
    /// tipo e filtra por tipo, por tag (com a hierarquia) e por texto, com os filtros combinados.
    /// </summary>
    public class GasIndexTests
    {
        private GasFactory _factory;
        private GameplayTag _ability;
        private GameplayTag _fireball;
        private GameplayTag _burning;
        private GameplayAbility _gaFireball;
        private GameplayAbility _gaIceBolt;
        private GameplayEffect _geBurn;
        private AttributeDefinition _health;

        [SetUp]
        public void SetUp()
        {
            _factory = new GasFactory();
            _ability = _factory.CreateTag("Ability");
            _fireball = _factory.CreateTag("Ability.Fireball", _ability);
            _burning = _factory.CreateTag("Status.Burning");
            _gaFireball = _factory.CreateAbility(_fireball);
            _gaFireball.name = "GA_Fireball";
            _gaIceBolt = _factory.CreateAbility();
            _gaIceBolt.name = "GA_IceBolt";
            _geBurn = _factory.CreateDurationEffect(3f, _burning);
            _geBurn.name = "GE_Burn";
            _health = _factory.CreateAttribute("Health");
        }

        [TearDown]
        public void TearDown() => _factory.Dispose();

        private GasIndex BuildIndex()
        {
            return GasIndex.Build(new Object[]
            {
                _geBurn, _health, _gaIceBolt, _fireball.GetDefinition(), _gaFireball, _ability.GetDefinition(),
                _burning.GetDefinition()
            });
        }

        private static string[] Names(IEnumerable<GasIndexEntry> entries) => entries.Select(e => e.Name).ToArray();

        [Test]
        public void Build_ListsFourKinds_SortedByKindThenName()
        {
            GasIndex index = BuildIndex();

            Assert.That(Names(index.Entries), Is.EqualTo(new[]
            {
                "GA_Fireball", "GA_IceBolt", "GE_Burn", "Health", "Ability", "Ability.Fireball", "Status.Burning"
            }));
            Assert.That(index.Entries.Select(e => e.Kind), Is.EqualTo(new[]
            {
                GasIndexKind.Ability, GasIndexKind.Ability, GasIndexKind.Effect, GasIndexKind.Attribute,
                GasIndexKind.Tag, GasIndexKind.Tag, GasIndexKind.Tag
            }));
            Assert.That(index.Entries[0].Asset, Is.SameAs(_gaFireball));
        }

        [Test]
        public void Counts_PerKind()
        {
            GasIndex index = BuildIndex();

            Assert.That(index.Count(GasIndexKind.Ability), Is.EqualTo(2));
            Assert.That(index.Count(GasIndexKind.Effect), Is.EqualTo(1));
            Assert.That(index.Count(GasIndexKind.Attribute), Is.EqualTo(1));
            Assert.That(index.Count(GasIndexKind.Tag), Is.EqualTo(3));
        }

        [Test]
        public void KindFilter_KeepsOnlyKind()
        {
            List<GasIndexEntry> result = BuildIndex().Query(new GasIndexFilter { Kind = GasIndexKind.Effect });

            Assert.That(Names(result), Is.EqualTo(new[] { "GE_Burn" }));
        }

        [Test]
        public void TextFilter_IgnoresCase()
        {
            List<GasIndexEntry> result = BuildIndex().Query(new GasIndexFilter { Text = "fire" });

            Assert.That(Names(result), Is.EqualTo(new[] { "GA_Fireball", "Ability.Fireball" }));
        }

        [Test]
        public void TagFilter_IncludesChildTags()
        {
            List<GasIndexEntry> result = BuildIndex().Query(new GasIndexFilter
            {
                Kind = GasIndexKind.Ability, Tag = _ability.GetDefinition()
            });

            Assert.That(Names(result), Is.EqualTo(new[] { "GA_Fireball" }));
        }

        [Test]
        public void TagFilter_IgnoresTagTheAssetOnlyRequires()
        {
            _gaIceBolt.RequiredTags.Add(_burning);
            _gaIceBolt.OnActivate.Add(new SendGameplayEventAction { Tag = _burning });

            List<GasIndexEntry> result = BuildIndex().Query(new GasIndexFilter { Tag = _burning.GetDefinition() });

            Assert.That(Names(result), Is.EqualTo(new[] { "GE_Burn", "Status.Burning" }));
        }

        [Test]
        public void TagFilter_OnTags_IncludesTagAndChildren()
        {
            List<GasIndexEntry> result = BuildIndex().Query(new GasIndexFilter
            {
                Kind = GasIndexKind.Tag, Tag = _ability.GetDefinition()
            });

            Assert.That(Names(result), Is.EqualTo(new[] { "Ability", "Ability.Fireball" }));
        }

        [Test]
        public void Filters_Combine()
        {
            GasIndex index = BuildIndex();

            List<GasIndexEntry> match = index.Query(new GasIndexFilter
            {
                Kind = GasIndexKind.Effect, Tag = _burning.GetDefinition(), Text = "burn"
            });
            List<GasIndexEntry> noMatch = index.Query(new GasIndexFilter
            {
                Kind = GasIndexKind.Effect, Tag = _burning.GetDefinition(), Text = "fire"
            });

            Assert.That(Names(match), Is.EqualTo(new[] { "GE_Burn" }));
            Assert.That(noMatch, Is.Empty);
        }

        [Test]
        public void EmptyFilter_ReturnsAll()
        {
            GasIndex index = BuildIndex();

            List<GasIndexEntry> result = index.Query(new GasIndexFilter { Text = "" });

            Assert.That(result, Has.Count.EqualTo(7));
            Assert.That(result, Is.EqualTo(index.Entries));
        }
    }
}
