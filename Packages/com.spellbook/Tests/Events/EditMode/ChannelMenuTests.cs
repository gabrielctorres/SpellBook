using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Events.Tests
{
    /// <summary>Canais concretos do menu Create (Spec 4.7; CA12).</summary>
    public class ChannelMenuTests
    {
        private const string MenuPrefix = "SpellBook/Events/";

        [Test]
        public void Menu_HasExactlySevenChannels_NoComponent()
        {
            var expected = new Dictionary<string, Type>
            {
                { MenuPrefix + "Void Channel", typeof(Core.Void) },
                { MenuPrefix + "Int Channel", typeof(int) },
                { MenuPrefix + "Float Channel", typeof(float) },
                { MenuPrefix + "Bool Channel", typeof(bool) },
                { MenuPrefix + "String Channel", typeof(string) },
                { MenuPrefix + "Vector3 Channel", typeof(Vector3) },
                { MenuPrefix + "GameObject Channel", typeof(GameObject) },
            };

            Dictionary<string, Type> actual = EventsMenuPayloads();

            CollectionAssert.AreEquivalent(expected, actual);
            Assert.That(actual.Values.Where(payload => typeof(Component).IsAssignableFrom(payload)), Is.Empty);
        }

        private static Dictionary<string, Type> EventsMenuPayloads()
        {
            return TypeCache.GetTypesWithAttribute<CreateAssetMenuAttribute>()
                .Select(type => (Type: type, Menu: type.GetCustomAttribute<CreateAssetMenuAttribute>(false)))
                .Where(entry => entry.Menu != null && entry.Menu.menuName.StartsWith(MenuPrefix, StringComparison.Ordinal))
                .ToDictionary(entry => entry.Menu.menuName, entry => PayloadOf(entry.Type));
        }

        private static Type PayloadOf(Type channelType)
        {
            for (Type type = channelType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventChannel<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }

            Assert.Fail($"{channelType.Name} está no menu de eventos mas não herda de EventChannel<T>.");
            return null;
        }
    }
}
