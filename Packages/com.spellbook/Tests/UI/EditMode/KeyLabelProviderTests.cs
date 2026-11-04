using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary><c>KeyLabelProvider</c> e <c>FixedKeyLabelProvider</c> (Spec do B-016, Design 2 e P5; prepara o CA10).</summary>
    public class KeyLabelProviderTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Fixed_LabelForKey_MissingKeyFalse()
        {
            ScriptableObject slotQ = Key();
            ScriptableObject slotE = Key();
            FixedKeyLabelProvider labels = Labels(slotQ, new LocalizedText(null, "Q"));
            var buffer = new TextBuffer();
            buffer.Append("x");

            int start = buffer.Length;
            bool found = labels.TryWriteLabel(slotQ, null, buffer);
            string label = buffer.ToString(buffer.SpanFrom(start));

            int beforeMissing = buffer.Length;
            bool missing = labels.TryWriteLabel(slotE, null, buffer);
            bool nullKey = labels.TryWriteLabel(null, null, buffer);

            Assert.That(found, Is.True);
            Assert.That(label, Is.EqualTo("Q"));
            Assert.That(missing, Is.False);
            Assert.That(nullKey, Is.False);
            Assert.That(buffer.Length, Is.EqualTo(beforeMissing), "chave ausente não escreve");
        }

        [Test]
        public void Fixed_LocalizedThroughProvider()
        {
            ScriptableObject slotQ = Key();
            FixedKeyLabelProvider labels = Labels(slotQ, new LocalizedText("ui.key.q", "Q"));
            var provider = new TestTextProvider("ui.key.q", "Botão A");
            var buffer = new TextBuffer();

            bool withProvider = labels.TryWriteLabel(slotQ, provider, buffer);
            string translated = buffer.ToString();
            buffer.Clear();
            labels.TryWriteLabel(slotQ, null, buffer);
            string fallback = buffer.ToString();

            Assert.That(withProvider, Is.True);
            Assert.That(translated, Is.EqualTo("Botão A"));
            Assert.That(fallback, Is.EqualTo("Q"));
        }

        [Test]
        public void Changed_RaisedOnEdit()
        {
            ScriptableObject slotQ = Key();
            FixedKeyLabelProvider labels = Labels(slotQ, new LocalizedText(null, "Q"));
            int changed = 0;
            labels.Changed += () => changed++;

            var serialized = new SerializedObject(labels);
            serialized.FindProperty("entries").GetArrayElementAtIndex(0).FindPropertyRelative("label.text").stringValue = "E";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var buffer = new TextBuffer();
            labels.TryWriteLabel(slotQ, null, buffer);

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(buffer.ToString(), Is.EqualTo("E"));
        }

        [Test]
        public void WriteLabel_DoesNotAllocate()
        {
            ScriptableObject slotQ = Key();
            ScriptableObject slotE = Key();
            ScriptableObject absent = Key();
            FixedKeyLabelProvider labels = Labels(slotQ, new LocalizedText("ui.key.q", "Q"),
                slotE, new LocalizedText(null, "E"));
            var provider = new TestTextProvider("ui.key.q", "Botão A");
            var buffer = new TextBuffer();
            int written = 0;
            TestDelegate write10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    buffer.Clear();
                    if (labels.TryWriteLabel(slotQ, provider, buffer)) written++;
                    if (labels.TryWriteLabel(slotE, null, buffer)) written++;
                    if (labels.TryWriteLabel(absent, provider, buffer)) written++;
                }
            };
            write10000(); // aquecimento

            Assert.That(write10000, Is.Not.AllocatingGCMemory());
            Assert.That(written, Is.EqualTo(40000));
            Assert.That(buffer.ToString(), Is.EqualTo("Botão AE"));
        }

        private ScriptableObject Key()
        {
            // Chave de slot de teste: na barra é o asset da tag, visto como ScriptableObject
            var key = ScriptableObject.CreateInstance<TextTable>();
            _created.Add(key);
            return key;
        }

        /// <summary>Provedor com pares chave, rótulo, gravados pelo <see cref="SerializedObject"/>.</summary>
        private FixedKeyLabelProvider Labels(params object[] keyLabelPairs)
        {
            var labels = ScriptableObject.CreateInstance<FixedKeyLabelProvider>();
            _created.Add(labels);
            var serialized = new SerializedObject(labels);
            SerializedProperty entries = serialized.FindProperty("entries");
            for (int i = 0; i < keyLabelPairs.Length / 2; i++)
            {
                var label = (LocalizedText)keyLabelPairs[i * 2 + 1];
                entries.InsertArrayElementAtIndex(i);
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").objectReferenceValue = (Object)keyLabelPairs[i * 2];
                entry.FindPropertyRelative("label.key").stringValue = label.Key ?? string.Empty;
                entry.FindPropertyRelative("label.text").stringValue = label.Text;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return labels;
        }

        /// <summary>Provedor de teste: uma chave fixa, cultura invariante.</summary>
        private sealed class TestTextProvider : ITextProvider
        {
            private readonly string _key;
            private readonly string _text;

            public TestTextProvider(string key, string text)
            {
                _key = key;
                _text = text;
            }

            public IFormatProvider Culture => CultureInfo.InvariantCulture;

            public event Action Changed
            {
                add { }
                remove { }
            }

            public string GetText(string key, string fallback) => key == _key ? _text : fallback;
        }
    }
}
