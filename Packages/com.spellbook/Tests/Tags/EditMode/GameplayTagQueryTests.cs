using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Tags.Tests
{
    /// <summary>Consulta de tags: todas, alguma e nenhuma, com hierarquia (CA2, CA11 a CA15, CA22).</summary>
    public class GameplayTagQueryTests
    {
        private TagTestFactory _factory;
        private GameplayTag _state;
        private GameplayTag _stunned;
        private GameplayTag _element;
        private GameplayTag _fire;
        private GameplayTag _flame;
        private GameplayTag _ice;
        private GameplayTag _immune;

        [SetUp]
        public void SetUp()
        {
            _factory = new TagTestFactory();
            GameplayTagDefinition state = _factory.Create("State");
            GameplayTagDefinition element = _factory.Create("Elemento");
            GameplayTagDefinition fire = _factory.Create("Elemento.Fogo", element);
            _state = new GameplayTag(state);
            _stunned = new GameplayTag(_factory.Create("State.Stunned", state));
            _element = new GameplayTag(element);
            _fire = new GameplayTag(fire);
            _flame = new GameplayTag(_factory.Create("Elemento.Fogo.Chama", fire));
            _ice = new GameplayTag(_factory.Create("Elemento.Gelo", element));
            _immune = new GameplayTag(_factory.Create("State.Immune", state));
        }

        [TearDown]
        public void TearDown() => _factory.Dispose();

        private static GameplayTagQuery Query(GameplayTag[] all = null, GameplayTag[] any = null, GameplayTag[] none = null)
        {
            var query = new GameplayTagQuery();
            foreach (GameplayTag tag in all ?? new GameplayTag[0]) query.All.Add(tag);
            foreach (GameplayTag tag in any ?? new GameplayTag[0]) query.Any.Add(tag);
            foreach (GameplayTag tag in none ?? new GameplayTag[0]) query.None.Add(tag);
            return query;
        }

        // Add recusa tag inválida; uma linha vazia só entra por dados serializados (Inspector ou YAML).
        private static GameplayTagQuery QueryWithEmptyRowIn(string list)
        {
            var query = new GameplayTagQuery();
            JsonUtility.FromJsonOverwrite("{\"" + list + "\":{\"tags\":[{}]}}", query);
            return query;
        }

        [Test]
        public void ExampleC4_ThreeContainers()
        {
            GameplayTagQuery query = Query(all: new[] { _stunned }, any: new[] { _fire, _ice }, none: new[] { _immune });

            Assert.That(query.Matches(new GameplayTagContainer(_stunned, _ice)), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_stunned, _ice, _immune)), Is.False);
            Assert.That(query.Matches(new GameplayTagContainer(_stunned)), Is.False);
        }

        [Test]
        public void EmptyQuery_MatchesEmptyAndAny_IsEmptyTrue()
        {
            var query = new GameplayTagQuery();

            Assert.That(query.IsEmpty, Is.True);
            Assert.That(query.Matches(new GameplayTagContainer()), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_stunned, _flame)), Is.True);
            Assert.That(query.Matches((IReadOnlyList<GameplayTag>)null), Is.True);
            Assert.That(query.All, Is.Not.Null);
            Assert.That(query.Any, Is.Not.Null);
            Assert.That(query.None, Is.Not.Null);
        }

        [Test]
        public void IsEmpty_FalseWhenAnyListHasTag()
        {
            Assert.That(Query(all: new[] { _ice }).IsEmpty, Is.False);
            Assert.That(Query(any: new[] { _ice }).IsEmpty, Is.False);
            Assert.That(Query(none: new[] { _ice }).IsEmpty, Is.False);
        }

        [Test]
        public void OnlyAll()
        {
            GameplayTagQuery query = Query(all: new[] { _stunned, _ice });

            Assert.That(query.Matches(new GameplayTagContainer(_stunned, _ice, _flame)), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_stunned)), Is.False);
            Assert.That(query.Matches(new GameplayTagContainer()), Is.False);
        }

        [Test]
        public void OnlyAny()
        {
            GameplayTagQuery query = Query(any: new[] { _stunned, _ice });

            Assert.That(query.Matches(new GameplayTagContainer(_ice)), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_stunned, _ice)), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_flame)), Is.False);
            Assert.That(query.Matches(new GameplayTagContainer()), Is.False);
        }

        [Test]
        public void OnlyNone()
        {
            GameplayTagQuery query = Query(none: new[] { _stunned, _ice });

            Assert.That(query.Matches(new GameplayTagContainer(_flame)), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer()), Is.True);
            Assert.That(query.Matches(new GameplayTagContainer(_flame, _ice)), Is.False);
        }

        [Test]
        public void MatchesTag_EqualsMatchesSingleContainer()
        {
            var invalid = new GameplayTag(null);
            GameplayTagQuery[] queries =
            {
                new GameplayTagQuery(),
                Query(all: new[] { _fire }),
                Query(all: new[] { _fire, _ice }),
                Query(any: new[] { _ice, _fire }),
                Query(none: new[] { _element }),
                Query(none: new[] { _ice }),
                Query(all: new[] { _element }, any: new[] { _fire }, none: new[] { _flame }),
                Query(all: new[] { _stunned }, any: new[] { _fire, _ice }, none: new[] { _immune }),
                QueryWithEmptyRowIn("all"),
                QueryWithEmptyRowIn("any"),
                QueryWithEmptyRowIn("none"),
            };
            GameplayTag[] tags = { _state, _stunned, _element, _fire, _flame, _ice, _immune, invalid };

            foreach (GameplayTagQuery query in queries)
            {
                foreach (GameplayTag tag in tags)
                {
                    bool expected = query.Matches(new GameplayTagContainer(tag));
                    Assert.That(query.Matches(tag), Is.EqualTo(expected), $"tag {tag}");
                }
            }

            Assert.That(Query(all: new[] { _fire }).Matches(_flame), Is.True);
            Assert.That(Query(all: new[] { _fire }).Matches(_ice), Is.False);
        }

        [Test]
        public void Hierarchy_ChildSatisfiesAllAndAny_BlockedByNoneViaParent()
        {
            var flameOnly = new GameplayTagContainer(_flame);

            Assert.That(Query(all: new[] { _fire }).Matches(flameOnly), Is.True);
            Assert.That(Query(any: new[] { _element }).Matches(flameOnly), Is.True);
            Assert.That(Query(none: new[] { _fire }).Matches(flameOnly), Is.False);
            Assert.That(Query(all: new[] { _flame }).Matches(new GameplayTagContainer(_fire)), Is.False);
            Assert.That(Query(none: new[] { _flame }).Matches(new GameplayTagContainer(_fire)), Is.True);
        }

        [Test]
        public void InvalidTag_NeverMatches_InQuery()
        {
            var invalid = new GameplayTag(null);
            var withInvalid = new List<GameplayTag> { invalid };

            Assert.That(Query(all: new[] { _fire }).Matches(withInvalid), Is.False);
            Assert.That(Query(any: new[] { _fire }).Matches(withInvalid), Is.False);
            Assert.That(Query(none: new[] { _fire }).Matches(withInvalid), Is.True);
            Assert.That(Query(all: new[] { _fire }).Matches(invalid), Is.False);

            // Uma tag inválida guardada numa lista (como no YAML) também não casa com nada.
            GameplayTagQuery emptyRowInAll = QueryWithEmptyRowIn("all");
            GameplayTagQuery emptyRowInAny = QueryWithEmptyRowIn("any");
            GameplayTagQuery emptyRowInNone = QueryWithEmptyRowIn("none");
            Assert.That(emptyRowInAll.All.Count, Is.EqualTo(1));
            Assert.That(emptyRowInAll.Matches(withInvalid), Is.False);
            Assert.That(emptyRowInAll.Matches(new GameplayTagContainer(_fire)), Is.False);
            Assert.That(emptyRowInAny.Matches(withInvalid), Is.False);
            Assert.That(emptyRowInAny.Matches(new GameplayTagContainer(_fire)), Is.False);
            Assert.That(emptyRowInNone.Matches(withInvalid), Is.True);
            Assert.That(emptyRowInNone.Matches(new GameplayTagContainer(_fire)), Is.True);
        }

        [Test]
        public void Matches_AcceptsListAndArray_AsArgument()
        {
            GameplayTagQuery query = Query(all: new[] { _stunned }, any: new[] { _fire, _ice }, none: new[] { _immune });

            Assert.That(query.Matches(new List<GameplayTag> { _stunned, _flame }), Is.True);
            Assert.That(query.Matches(new[] { _stunned, _ice, _immune }), Is.False);
        }

        [Test]
        public void Matches_DoesNotAllocate_10000Times()
        {
            GameplayTagQuery query = Query(all: new[] { _stunned }, any: new[] { _fire, _ice }, none: new[] { _immune });
            var container = new GameplayTagContainer(_stunned, _flame);
            var list = new List<GameplayTag> { _stunned, _ice };
            var array = new[] { _stunned, _ice, _immune };
            bool sink = query.Matches(container) | query.Matches(list) | query.Matches(array) | query.Matches(_flame);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    sink |= query.Matches(container);
                    sink |= query.Matches(list);
                    sink |= query.Matches(array);
                    sink |= query.Matches(_flame);
                    sink |= query.IsEmpty;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sink, Is.True);
        }
    }
}
