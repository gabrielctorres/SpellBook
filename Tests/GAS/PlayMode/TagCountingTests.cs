using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>
    /// Caracterização das tags do ASC (contagem de referência e match hierárquico), verde antes e depois do 0M-14.
    /// </summary>
    public class TagCountingTests
    {
        private GasFactory _gas;
        private AbilitySystemComponent _asc;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _asc = _gas.CreateAsc();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        [Test]
        public void AddTagTwice_RemoveOnce_KeepsTag()
        {
            GameplayTag stunned = _gas.CreateTag("State.Stunned");

            _asc.AddTag(stunned);
            _asc.AddTag(stunned);
            _asc.RemoveTag(stunned);

            Assert.That(_asc.HasTag(stunned), Is.True);
        }

        [Test]
        public void AddTagTwice_RemoveTwice_RemovesTag()
        {
            GameplayTag stunned = _gas.CreateTag("State.Stunned");

            _asc.AddTag(stunned);
            _asc.AddTag(stunned);
            _asc.RemoveTag(stunned);
            _asc.RemoveTag(stunned);

            Assert.That(_asc.HasTag(stunned), Is.False);
        }

        [Test]
        public void ChildTag_GrantsHasTagOfParent()
        {
            GameplayTag state = _gas.CreateTag("State");
            GameplayTag stunned = _gas.CreateTag("State.Stunned", state);

            _asc.AddTag(stunned);

            Assert.That(_asc.HasTag(state), Is.True);
        }

        [Test]
        public void ParentTag_DoesNotGrantHasTagOfChild()
        {
            GameplayTag state = _gas.CreateTag("State");
            GameplayTag stunned = _gas.CreateTag("State.Stunned", state);

            _asc.AddTag(state);

            Assert.That(_asc.HasTag(stunned), Is.False);
        }

        [Test]
        public void OnTagChanged_FiresOnlyOnFirstAddAndLastRemove()
        {
            GameplayTag stunned = _gas.CreateTag("State.Stunned");
            var changes = new List<(GameplayTag Tag, bool Added)>();
            _asc.OnTagChanged += (tag, added) => changes.Add((tag, added));

            _asc.AddTag(stunned);
            _asc.AddTag(stunned);
            _asc.RemoveTag(stunned);
            _asc.RemoveTag(stunned);

            Assert.That(changes, Is.EqualTo(new[] { (stunned, true), (stunned, false) }));
        }
    }
}
