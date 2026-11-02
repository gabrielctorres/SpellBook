using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Events;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Payloads, canais e respostas tipados do GAS (Spec do relay, RL2; CA20, N2). Os canais ficam sob
    /// <c>SpellBook/GAS/Events/</c>, fora do conjunto fechado do módulo Events.
    /// </summary>
    public class GasChannelsTests
    {
        private const string GasMenuPrefix = "SpellBook/GAS/Events/";
        private const string EventsMenuPrefix = "SpellBook/Events/";

        private static readonly Dictionary<Type, Type> ChannelPayloads = new Dictionary<Type, Type>
        {
            { typeof(EffectEventChannel), typeof(EffectEventData) },
            { typeof(AbilityEventChannel), typeof(AbilityEventData) },
            { typeof(AttributeChangedEventChannel), typeof(AttributeChangedData) },
            { typeof(TagChangedEventChannel), typeof(TagChangedData) },
            { typeof(BlockedEventChannel), typeof(BlockedEventData) },
            { typeof(AttributeLimitEventChannel), typeof(AttributeLimitEventData) },
        };

        private EventsFactory _events;
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _events.Dispose();
            _gas.Dispose();
        }

        [Test]
        public void GasChannels_HaveCreateMenuAndHelpUrl()
        {
            var expected = new Dictionary<string, Type>
            {
                { GasMenuPrefix + "Effect Channel", typeof(EffectEventData) },
                { GasMenuPrefix + "Ability Channel", typeof(AbilityEventData) },
                { GasMenuPrefix + "Attribute Changed Channel", typeof(AttributeChangedData) },
                { GasMenuPrefix + "Tag Changed Channel", typeof(TagChangedData) },
                { GasMenuPrefix + "Blocked Channel", typeof(BlockedEventData) },
                { GasMenuPrefix + "Attribute Limit Channel", typeof(AttributeLimitEventData) },
            };

            Dictionary<string, Type> actual = MenuPayloads(GasMenuPrefix);

            CollectionAssert.AreEquivalent(expected, actual);
            foreach (Type channel in ChannelPayloads.Keys)
            {
                var help = channel.GetCustomAttribute<HelpURLAttribute>(false);
                Assert.That(help, Is.Not.Null, channel.Name);
                Assert.That(help.URL, Is.EqualTo(DocUrls.Events + "#canais-do-gas"), channel.Name);
            }

            Assembly bridges = typeof(EffectEventChannel).Assembly;
            Assert.That(MenuTypes(EventsMenuPrefix).Where(type => type.Assembly == bridges), Is.Empty);
        }

        [Test]
        public void AttributeLimitChannel_HasMenuAndHelpUrl()
        {
            Type channel = typeof(AttributeLimitEventChannel);

            var menu = channel.GetCustomAttribute<CreateAssetMenuAttribute>(false);
            var help = channel.GetCustomAttribute<HelpURLAttribute>(false);
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.menuName, Is.EqualTo(GasMenuPrefix + "Attribute Limit Channel"));
            Assert.That(ChannelPayloadOf(channel), Is.EqualTo(typeof(AttributeLimitEventData)));
            Assert.That(help, Is.Not.Null);
            Assert.That(help.URL, Is.EqualTo(DocUrls.Events + "#canais-do-gas"));
            Assert.That(typeof(AttributeLimitEventResponse).GetCustomAttribute<TypeCategoryAttribute>(false).Path, Is.EqualTo("Respostas"));
        }

        [Test]
        public void GasResponses_ListedForMatchingChannelType()
        {
            List<Type> responses = TypeCache.GetTypesDerivedFrom<EventResponse>()
                .Where(type => !type.IsAbstract && !type.IsGenericType && type.IsSerializable
                    && type.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            foreach (KeyValuePair<Type, Type> pair in ChannelPayloads)
            {
                List<Type> matching = responses.Where(type => ResponsePayloadOf(type) == pair.Value).ToList();

                Assert.That(matching, Has.Count.EqualTo(1), pair.Key.Name);
                Assert.That(matching[0].Assembly, Is.EqualTo(pair.Key.Assembly), matching[0].Name);
                var category = matching[0].GetCustomAttribute<TypeCategoryAttribute>(false);
                Assert.That(category, Is.Not.Null, matching[0].Name);
                Assert.That(category.Path, Is.EqualTo("Respostas"), matching[0].Name);
            }
        }

        [Test]
        public void EffectEventData_RaiseDoesNotAllocate_AfterWarmup()
        {
            var channel = _events.CreateChannel<EffectEventChannel>();
            var effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            int stacks = 0;
            channel.Raised += data => stacks += data.Stacks;
            var payload = new EffectEventData
            {
                Effect = effect,
                Stacks = 2,
                PreviousStacks = 1,
                RemovalReason = EffectRemovalReason.Expired,
                Result = EffectApplicationResult.Stacked,
            };
            channel.Raise(payload);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    channel.Raise(payload);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(stacks, Is.EqualTo(2 * 1001));
            Assert.That(channel.LastPayload.Effect, Is.SameAs(effect));
        }

        [Test]
        public void AttributeChangedData_Delta_IsNewMinusOld()
        {
            var data = new AttributeChangedData(null, null, 10f, 4f);

            Assert.That(data.OldValue, Is.EqualTo(10f));
            Assert.That(data.NewValue, Is.EqualTo(4f));
            Assert.That(data.Delta, Is.EqualTo(-6f));
        }

        private static Dictionary<string, Type> MenuPayloads(string prefix)
        {
            return MenuTypes(prefix).ToDictionary(
                type => type.GetCustomAttribute<CreateAssetMenuAttribute>(false).menuName,
                ChannelPayloadOf);
        }

        private static IEnumerable<Type> MenuTypes(string prefix)
        {
            return TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>()
                .Where(type =>
                {
                    var menu = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);
                    return menu != null && menu.menuName.StartsWith(prefix, StringComparison.Ordinal);
                });
        }

        private static Type ChannelPayloadOf(Type channelType)
        {
            return GenericArgumentOf(channelType, typeof(EventChannel<>));
        }

        private static Type ResponsePayloadOf(Type responseType)
        {
            return GenericArgumentOf(responseType, typeof(EventResponse<>));
        }

        private static Type GenericArgumentOf(Type type, Type openGeneric)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == openGeneric)
                {
                    return current.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }
}
