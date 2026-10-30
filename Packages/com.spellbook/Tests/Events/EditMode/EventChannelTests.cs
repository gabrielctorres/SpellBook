using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>Canais de evento em asset (ADR 0016; Spec 4.7; CA15, CA16, CA18, CA19, CA33).</summary>
    public class EventChannelTests
    {
        private EventsFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
        }

        [Test]
        public void Channel_RaiseDeliversExactValue()
        {
            var channel = _factory.CreateChannel<FloatEventChannel>();
            var received = new List<float>();
            channel.Raised += received.Add;

            channel.Raise(0.1f);

            Assert.That(received, Is.EqualTo(new[] { 0.1f }));
        }

        [Test]
        public void Channel_LastPayload()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();

            channel.Raise(5);
            channel.Raise(9, channel);

            Assert.That(channel.LastPayload, Is.EqualTo(9));
        }

        [Test]
        public void Channel_ReentrancySafe()
        {
            var channel = _factory.CreateChannel<VoidEventChannel>();
            int a = 0, b = 0, late = 0;
            Action<Core.Void> listenerA = _ => a++;
            Action<Core.Void> lateListener = _ => late++;
            Action<Core.Void> listenerB = _ =>
            {
                b++;
                channel.Raised -= listenerA;
                channel.Raised += lateListener;
            };
            channel.Raised += listenerB;
            channel.Raised += listenerA;

            channel.Raise();

            Assert.That(new[] { a, b, late }, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [Test]
        public void Channel_ExceptionIsolatedInEditor()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            int first = 0, third = 0;
            channel.Raised += _ => first++;
            channel.Raised += _ => throw new InvalidOperationException("ouvinte quebrado");
            channel.Raised += _ => third++;
            LogAssert.Expect(LogType.Exception, new Regex("ouvinte quebrado"));

            channel.Raise(1);

            Assert.That(new[] { first, third }, Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public void Channel_ThousandRaises_ZeroAlloc()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            int total = 0;
            channel.Raised += value => total += value;
            channel.Raised += value => total -= value;
            channel.Raise(3);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    channel.Raise(3);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Channel_AfterSessionReset_NoListeners()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            int calls = 0;
            channel.Raised += _ => calls++;

            EventRegistry.ResetAllOnSessionStart();
            channel.Raise(1);

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Channel_LogRaises_WritesToConsole()
        {
            var channel = _factory.CreateChannel<IntEventChannel>(logRaises: true);
            LogAssert.Expect(LogType.Log, new Regex(Regex.Escape(channel.name) + ".*42"));

            channel.Raise(42);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Channel_ListenerCount_IgnoresRemovedDuringRaise()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            int countDuringRaise = -1;
            Action<int> removed = _ => { };
            channel.Raised += _ =>
            {
                channel.Raised -= removed;
                countDuringRaise = channel.ListenerCount;
            };
            channel.Raised += removed;
            channel.Raised += _ => { };

            channel.Raise(1);

            Assert.That(countDuringRaise, Is.EqualTo(2));
        }

        [Test]
        public void Channel_GetListeners_ReturnsTargets()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            var receiver = new Receiver();
            Action<int> staticListener = StaticListener;
            channel.Raised += receiver.OnRaised;
            channel.Raised += staticListener;
            var targets = new List<object> { "sobra" };

            channel.GetListeners(targets);

            Assert.That(targets, Is.EquivalentTo(new object[] { receiver, staticListener }));
        }

        [Test]
        public void Channel_DestroyedBeforeSessionReset_NoError()
        {
            var channel = _factory.CreateChannel<IntEventChannel>();
            channel.Raised += _ => { };

            Object.DestroyImmediate(channel);
            EventRegistry.ResetAllOnSessionStart();

            Assert.That(EventChannel.LiveChannels.Any(live => ReferenceEquals(live, channel)), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static void StaticListener(int value)
        {
        }

        private sealed class Receiver
        {
            public void OnRaised(int value)
            {
            }
        }
    }
}
