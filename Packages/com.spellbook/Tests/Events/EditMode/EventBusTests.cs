using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Events.Tests
{
    /// <summary>Barramento de código por tipo de struct (ADR 0016; Spec 4.7; CA15, CA16, CA17, CA19).</summary>
    public class EventBusTests
    {
        private struct Hit : IEvent
        {
            public int Damage;
        }

        [TearDown]
        public void TearDown()
        {
            EventRegistry.ResetAllOnSessionStart();
        }

        [Test]
        public void Bus_DeliversToAll()
        {
            var first = new List<int>();
            var second = new List<int>();
            EventBus<Hit>.Subscribe(e => first.Add(e.Damage));
            EventBus<Hit>.Subscribe(e => second.Add(e.Damage));

            EventBus<Hit>.Raise(new Hit { Damage = 7 });

            Assert.That(first, Is.EqualTo(new[] { 7 }));
            Assert.That(second, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void Bus_SubscribeTwice_CalledOnce()
        {
            int calls = 0;
            Action<Hit> listener = _ => calls++;
            EventBus<Hit>.Subscribe(listener);
            EventBus<Hit>.Subscribe(listener);

            EventBus<Hit>.Raise(new Hit());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Bus_UnsubscribeOtherDuringRaise_NoSkipNoDouble()
        {
            int a = 0, b = 0, c = 0, d = 0;
            Action<Hit> listenerA = _ => a++;
            Action<Hit> listenerC = _ => c++;
            Action<Hit> listenerB = _ =>
            {
                b++;
                EventBus<Hit>.Unsubscribe(listenerA);
                EventBus<Hit>.Unsubscribe(listenerC);
            };
            EventBus<Hit>.Subscribe(listenerA);
            EventBus<Hit>.Subscribe(listenerB);
            EventBus<Hit>.Subscribe(listenerC);
            EventBus<Hit>.Subscribe(_ => d++);

            EventBus<Hit>.Raise(new Hit());
            EventBus<Hit>.Raise(new Hit());

            Assert.That(new[] { a, b, c, d }, Is.EqualTo(new[] { 1, 2, 0, 2 }));
        }

        [Test]
        public void Bus_SubscribeDuringRaise_NotCalledThisRaise()
        {
            int late = 0;
            Action<Hit> lateListener = _ => late++;
            EventBus<Hit>.Subscribe(_ => EventBus<Hit>.Subscribe(lateListener));

            EventBus<Hit>.Raise(new Hit());
            int afterFirstRaise = late;
            EventBus<Hit>.Raise(new Hit());

            Assert.That(afterFirstRaise, Is.EqualTo(0));
            Assert.That(late, Is.EqualTo(1));
        }

        [Test]
        public void Bus_ExceptionInSecond_FirstAndThirdReceive_LogsException()
        {
            int first = 0, third = 0;
            EventBus<Hit>.Subscribe(_ => first++);
            EventBus<Hit>.Subscribe(_ => throw new InvalidOperationException("ouvinte quebrado"));
            EventBus<Hit>.Subscribe(_ => third++);
            LogAssert.Expect(LogType.Exception, new Regex("ouvinte quebrado"));

            EventBus<Hit>.Raise(new Hit());

            Assert.That(first, Is.EqualTo(1));
            Assert.That(third, Is.EqualTo(1));
        }

        [Test]
        public void Bus_ThousandRaises_ZeroAlloc()
        {
            int total = 0;
            EventBus<Hit>.Subscribe(e => total += e.Damage);
            EventBus<Hit>.Subscribe(e => total -= e.Damage);
            var hit = new Hit { Damage = 3 };
            EventBus<Hit>.Raise(hit);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    EventBus<Hit>.Raise(hit);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Bus_AfterSessionReset_NoListeners()
        {
            int calls = 0;
            EventBus<Hit>.Subscribe(_ => calls++);

            EventRegistry.ResetAllOnSessionStart();
            EventBus<Hit>.Raise(new Hit());

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Bus_SecondSessionReset_StillClears()
        {
            int calls = 0;
            EventBus<Hit>.Subscribe(_ => calls++);
            EventRegistry.ResetAllOnSessionStart();
            EventBus<Hit>.Subscribe(_ => calls++);

            EventRegistry.ResetAllOnSessionStart();
            EventBus<Hit>.Raise(new Hit());

            Assert.That(calls, Is.EqualTo(0));
        }
    }
}
