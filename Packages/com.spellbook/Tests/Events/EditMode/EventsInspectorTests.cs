using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Events.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Inspectors do canal e do emissor e troca de canal da emissão (Spec, 0M-45). Conferem a estrutura e as funções
    /// puras, sem bind (risco 33).
    /// </summary>
    public class EventsInspectorTests
    {
        private readonly List<Object> _created = new List<Object>();
        private EventsFactory _events;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
            _events.Dispose();
        }

        [Test]
        public void ChannelInspector_IsSpellBookEditor()
        {
            UnityEditor.Editor editor = MakeEditor(_events.CreateChannel<IntEventChannel>());

            Assert.That(editor, Is.InstanceOf<EventChannelEditor>());
            Assert.That(editor, Is.InstanceOf<SpellBookEditor>());
        }

        [Test]
        public void ChannelInspector_HasRaiseButton_DisabledOutsidePlay()
        {
            VisualElement root = MakeEditor(_events.CreateChannel<IntEventChannel>()).CreateInspectorGUI();

            var button = root.Q<Button>(EventChannelEditor.RaiseButtonName);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.False);
        }

        [Test]
        public void EmitterInspector_HasRaiseButton_DisabledOutsidePlay()
        {
            var gameObject = new GameObject("Emitter");
            _created.Add(gameObject);
            UnityEditor.Editor editor = MakeEditor(gameObject.AddComponent<EventEmitter>());

            var button = editor.CreateInspectorGUI().Q<Button>(EventEmitterEditor.RaiseButtonName);

            Assert.That(editor, Is.InstanceOf<SpellBookEditor>());
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledSelf, Is.False);
        }

        [Test]
        public void ChannelInspector_Raise_UsesDebugPayload()
        {
            var channel = _events.CreateChannel<IntEventChannel>();
            var serialized = new SerializedObject(channel);
            serialized.FindProperty("debugPayload").intValue = 7;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var received = new List<int>();
            channel.Raised += received.Add;

            channel.RaiseDebugPayload();

            Assert.That(received, Is.EqualTo(new[] { 7 }));
            Assert.That(channel.BoxedLastPayload, Is.EqualTo(7));
        }

        [Test]
        public void SetChannel_DifferentPayloadType_RecreatesAsTypedEmission()
        {
            EmissionHolder holder = MakeHolder(_events.CreateChannel<FloatEventChannel>());
            var intChannel = _events.CreateChannel<IntEventChannel>();

            EventEmissionEditing.SetChannel(EmissionProperty(holder), intChannel);

            Assert.That(holder.emission, Is.TypeOf<EventEmission<int>>());
            Assert.That(holder.emission.Channel, Is.SameAs(intChannel));
        }

        [Test]
        public void SetChannel_SamePayloadType_KeepsPayload()
        {
            EmissionHolder holder = MakeHolder(_events.CreateChannel<FloatEventChannel>());
            SetPayload(holder, 2.5f);
            EventEmission before = holder.emission;
            var other = _events.CreateChannel<FloatEventChannel>();

            EventEmissionEditing.SetChannel(EmissionProperty(holder), other);

            Assert.That(holder.emission, Is.SameAs(before));
            Assert.That(holder.emission.Channel, Is.SameAs(other));
            Assert.That(EmissionProperty(holder).FindPropertyRelative("payload").floatValue, Is.EqualTo(2.5f));
        }

        [Test]
        public void SetChannel_Null_KeepsEmissionWithoutChannel()
        {
            EmissionHolder holder = MakeHolder(_events.CreateChannel<FloatEventChannel>());
            SetPayload(holder, 2.5f);

            EventEmissionEditing.SetChannel(EmissionProperty(holder), null);

            Assert.That(holder.emission, Is.TypeOf<EventEmission<float>>());
            Assert.That(holder.emission.Channel, Is.Null);
            Assert.That(EmissionProperty(holder).FindPropertyRelative("payload").floatValue, Is.EqualTo(2.5f));
        }

        [TestCase(typeof(EventChannelEditor))]
        [TestCase(typeof(EventEmitterEditor))]
        public void Inspector_DeclaresCanEditMultipleObjects(Type editorType)
        {
            // Declarado na própria classe: a seleção de vários canais ou emissores mostra o Raise para todos.
            Assert.That(Attribute.IsDefined(editorType, typeof(CanEditMultipleObjects), inherit: false), Is.True);
        }

        [Test]
        public void EmissionDrawer_VoidChannel_HasChannelFieldWithoutPayload()
        {
            var channel = _events.CreateChannel<VoidEventChannel>();
            EmissionHolder holder = MakeHolder(channel);

            VisualElement root = new EventEmissionDrawer().CreatePropertyGUI(EmissionProperty(holder));

            var channelField = root.Q<ObjectField>();
            Assert.That(channelField, Is.Not.Null);
            Assert.That(channelField.label, Is.EqualTo("Channel"));
            Assert.That(channelField.objectType, Is.EqualTo(typeof(EventChannel)));
            Assert.That(channelField.value, Is.SameAs(channel));
            Assert.That(root.Q<PropertyField>(), Is.Null);
        }

        [Test]
        public void EmissionDrawer_FloatChannel_HasPayloadField()
        {
            EmissionHolder holder = MakeHolder(_events.CreateChannel<FloatEventChannel>());

            VisualElement root = new EventEmissionDrawer().CreatePropertyGUI(EmissionProperty(holder));

            Assert.That(root.Q<PropertyField>(), Is.Not.Null);
        }

        [Test]
        public void EmissionDrawer_NullField_IsChosenByUnity()
        {
            // Risco 27: a Unity escolhe o drawer de tipo para o campo [SerializeReference] abstrato, também nulo. Não
            // há API pública para isso; o teste lê o PropertyHandler interno (ScriptAttributeUtility.GetHandler).
            var holder = ScriptableObject.CreateInstance<EmissionHolder>();
            _created.Add(holder);
            SerializedProperty property = EmissionProperty(holder);
            Assert.That(property.managedReferenceValue, Is.Null);

            Assert.That(ChosenDrawer(property), Is.InstanceOf<EventEmissionDrawer>());
        }

        private UnityEditor.Editor MakeEditor(Object target)
        {
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(target);
            _created.Insert(0, editor);
            return editor;
        }

        private EmissionHolder MakeHolder(EventChannel channel)
        {
            var holder = ScriptableObject.CreateInstance<EmissionHolder>();
            holder.emission = EventEmission.CreateFor(channel);
            _created.Add(holder);
            return holder;
        }

        private static SerializedProperty EmissionProperty(EmissionHolder holder)
        {
            return new SerializedObject(holder).FindProperty("emission");
        }

        private static object ChosenDrawer(SerializedProperty property)
        {
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic;
            Type utility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility", throwOnError: true);
            MethodInfo getHandler = utility.GetMethod("GetHandler", Any | BindingFlags.Static, null, new[] { typeof(SerializedProperty) }, null);
            Assert.That(getHandler, Is.Not.Null, "ScriptAttributeUtility.GetHandler(SerializedProperty) não existe nesta versão.");
            object handler = getHandler.Invoke(null, new object[] { property });
            PropertyInfo drawer = handler.GetType().GetProperty("propertyDrawer", Any | BindingFlags.Instance);
            Assert.That(drawer, Is.Not.Null, "PropertyHandler.propertyDrawer não existe nesta versão.");
            return drawer.GetValue(handler);
        }

        private static void SetPayload(EmissionHolder holder, float value)
        {
            SerializedProperty emission = EmissionProperty(holder);
            emission.FindPropertyRelative("payload").floatValue = value;
            emission.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
