using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.Events.Editor;
using SpellBook.Testing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>Modelo do Event Debugger (Spec 4.7, 0M-35; R20, I-d): grava, busca, limpa e guarda os últimos 256 disparos.</summary>
    public class EventDebuggerModelTests
    {
        private static readonly DateTime FixedNow = new DateTime(2026, 10, 31, 14, 30, 5, 250);

        private EventsFactory _events;
        private EventDebuggerModel _model;
        private GameObject _sender;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _model = new EventDebuggerModel(() => 42, () => FixedNow);
        }

        [TearDown]
        public void TearDown()
        {
            _model.Stop();
            _events.Dispose();
            if (_sender != null)
            {
                Object.DestroyImmediate(_sender);
            }
        }

        [Test]
        public void Model_RecordsFrameTimePayloadSender()
        {
            var channel = Channel<IntEventChannel>("ScoreChanged");
            _sender = new GameObject("Button");
            _model.Start();

            channel.Raise(7, _sender);

            List<EventDebuggerModel.Entry> entries = Entries(null);
            Assert.That(entries.Count, Is.EqualTo(1));
            EventDebuggerModel.Entry entry = entries[0];
            Assert.That(entry.Frame, Is.EqualTo(42));
            Assert.That(entry.Time, Is.EqualTo(FixedNow));
            Assert.That(entry.Channel, Is.SameAs(channel));
            Assert.That(entry.ChannelName, Is.EqualTo("ScoreChanged"));
            Assert.That(entry.Payload, Is.EqualTo("7"));
            Assert.That(entry.Sender, Is.SameAs(_sender));
            Assert.That(entry.SenderName, Is.EqualTo("Button"));
        }

        [Test]
        public void Model_SearchByName()
        {
            var died = Channel<VoidEventChannel>("PlayerDied");
            var score = Channel<IntEventChannel>("ScoreChanged");
            _model.Start();

            died.Raise(default);
            score.Raise(1);
            died.Raise(default);

            List<EventDebuggerModel.Entry> found = Entries("player");
            Assert.That(found.Count, Is.EqualTo(2));
            Assert.That(found.TrueForAll(e => e.Channel == died), Is.True);
            Assert.That(Entries("").Count, Is.EqualTo(3));
            Assert.That(Entries("missing").Count, Is.EqualTo(0));
        }

        [Test]
        public void Model_Clear()
        {
            var channel = Channel<IntEventChannel>("ScoreChanged");
            _model.Start();
            channel.Raise(1);
            channel.Raise(2);

            _model.Clear();

            Assert.That(_model.Count, Is.EqualTo(0));
            Assert.That(Entries(null), Is.Empty);
            channel.Raise(3);
            Assert.That(Entries(null)[0].Payload, Is.EqualTo("3"));
        }

        [Test]
        public void Model_RingBufferKeepsLast256()
        {
            var channel = Channel<IntEventChannel>("ScoreChanged");
            _model.Start();

            for (int i = 0; i < 300; i++)
            {
                channel.Raise(i);
            }

            List<EventDebuggerModel.Entry> entries = Entries(null);
            Assert.That(EventDebuggerModel.Capacity, Is.EqualTo(256));
            Assert.That(_model.Count, Is.EqualTo(256));
            Assert.That(entries.Count, Is.EqualTo(256));
            Assert.That(entries[0].Payload, Is.EqualTo("299"), "o mais recente vem primeiro");
            Assert.That(entries[255].Payload, Is.EqualTo("44"), "os 44 mais antigos saíram");
        }

        [Test]
        public void Model_NoRecordingWhenClosed()
        {
            var channel = Channel<IntEventChannel>("ScoreChanged");
            channel.Raise(1);
            _model.Start();
            _model.Stop();

            channel.Raise(2);

            Assert.That(_model.IsRecording, Is.False);
            Assert.That(_model.Count, Is.EqualTo(0));
        }

        private TChannel Channel<TChannel>(string name) where TChannel : EventChannel
        {
            var channel = _events.CreateChannel<TChannel>();
            channel.name = name;
            return channel;
        }

        private List<EventDebuggerModel.Entry> Entries(string search)
        {
            var results = new List<EventDebuggerModel.Entry>();
            _model.GetEntries(search, results);
            return results;
        }
    }
}
