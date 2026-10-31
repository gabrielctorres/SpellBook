using System;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Events.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Void = SpellBook.Core.Void;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Filtro do seletor de canal (Spec 4.7, 0M-34; CA21 e CA58): campo <c>EventChannel&lt;T&gt;</c> → canais de T;
    /// RaiseChannel em ForwardPayload → payload da resposta; em RaiseVoid → Void; tipo base → todos. O
    /// <see cref="ChannelPickerDrawer"/> recusa o canal arrastado fora do filtro e mostra a descrição do canal.
    /// </summary>
    public class ChannelCatalogTests
    {
        private EventsFactory _events;
        private GameObject _gameObject;
        private FloatEventChannel _floatA;
        private FloatEventChannel _floatB;
        private IntEventChannel _int;
        private VoidEventChannel _void;

        [SetUp]
        public void SetUp()
        {
            _events = new EventsFactory();
            _floatA = _events.CreateChannel<FloatEventChannel>();
            _floatB = _events.CreateChannel<FloatEventChannel>();
            _int = _events.CreateChannel<IntEventChannel>();
            _void = _events.CreateChannel<VoidEventChannel>();
            _gameObject = new GameObject("Listener");
            _gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            _events.Dispose();
        }

        private EventChannel[] All => new EventChannel[] { _floatA, _int, _void, _floatB };

        [Test]
        public void Filter_FloatResponse_OnlyFloatChannels()
        {
            SerializedProperty response = AddResponse(new FloatEventResponse());
            SerializedProperty channel = response.FindPropertyRelative("channel");

            Type payload = ChannelCatalog.PayloadTypeFor(channel, FieldType(typeof(FloatEventResponse), "channel"));

            Assert.That(ChannelCatalog.Filter(All, payload), Is.EqualTo(new EventChannel[] { _floatA, _floatB }));
        }

        [Test]
        public void Filter_ForwardReaction_OnlySameType()
        {
            SerializedProperty response = AddResponse(new FloatEventResponse());
            SerializedProperty target = AddRaiseChannel(response, RaiseChannelReaction.Mode.ForwardPayload);

            Type payload = ChannelCatalog.PayloadTypeFor(target, FieldType(typeof(RaiseChannelReaction), "target"));

            Assert.That(ChannelCatalog.Filter(All, payload), Is.EqualTo(new EventChannel[] { _floatA, _floatB }));
        }

        [Test]
        public void Filter_ForwardReactionInsideDelayed_OnlySameType()
        {
            SerializedProperty response = AddResponse(new IntEventResponse());
            SerializedProperty delayed = AddReaction(response, new DelayedReaction());
            SerializedProperty target = AddRaiseChannel(delayed, RaiseChannelReaction.Mode.ForwardPayload);

            Type payload = ChannelCatalog.PayloadTypeFor(target, FieldType(typeof(RaiseChannelReaction), "target"));

            Assert.That(ChannelCatalog.Filter(All, payload), Is.EqualTo(new EventChannel[] { _int }));
        }

        [Test]
        public void Filter_RaiseVoidReaction_OnlyVoid()
        {
            SerializedProperty response = AddResponse(new FloatEventResponse());
            SerializedProperty target = AddRaiseChannel(response, RaiseChannelReaction.Mode.RaiseVoid);

            Type payload = ChannelCatalog.PayloadTypeFor(target, FieldType(typeof(RaiseChannelReaction), "target"));

            Assert.That(ChannelCatalog.Filter(All, payload), Is.EqualTo(new EventChannel[] { _void }));
        }

        [Test]
        public void Filter_Void_OnlyVoid()
        {
            Assert.That(ChannelCatalog.Filter(All, typeof(Void)), Is.EqualTo(new EventChannel[] { _void }));
        }

        [Test]
        public void Filter_BaseType_AllChannels()
        {
            Type payload = ChannelCatalog.PayloadTypeFor(null, typeof(EventChannel));

            Assert.That(payload, Is.Null);
            Assert.That(ChannelCatalog.Filter(All, payload), Is.EqualTo(All));
        }

        // A recusa do canal arrastado passa pelo Accepts; o teste não liga o drawer a um painel (Spec, risco 33).
        [Test]
        public void Picker_IntDraggedIntoFloatResponse_IsRefused()
        {
            SerializedProperty channel = AddResponse(new FloatEventResponse()).FindPropertyRelative("channel");

            Assert.That(ChannelPickerDrawer.Accepts(channel, FieldType(typeof(FloatEventResponse), "channel"), _int), Is.False);
        }

        [Test]
        public void Picker_IntDraggedIntoForwardReactionOfFloat_IsRefused()
        {
            // O campo target é do tipo base: o ObjectField aceita o Int e quem recusa é o filtro do drawer.
            SerializedProperty response = AddResponse(new FloatEventResponse());
            SerializedProperty target = AddRaiseChannel(response, RaiseChannelReaction.Mode.ForwardPayload);

            Assert.That(ChannelPickerDrawer.Accepts(target, FieldType(typeof(RaiseChannelReaction), "target"), _int), Is.False);
        }

        [Test]
        public void Picker_FloatDraggedIntoForwardReactionOfFloat_IsAccepted()
        {
            SerializedProperty response = AddResponse(new FloatEventResponse());
            SerializedProperty target = AddRaiseChannel(response, RaiseChannelReaction.Mode.ForwardPayload);

            Assert.That(ChannelPickerDrawer.Accepts(target, FieldType(typeof(RaiseChannelReaction), "target"), _floatA), Is.True);
        }

        [Test]
        public void Picker_ChannelWithDescription_ShowsHelpBox()
        {
            SetDescription(_floatA, "Dano recebido.");
            SerializedProperty channel = AddResponse(new FloatEventResponse()).FindPropertyRelative("channel");
            Assign(channel, _floatA);

            var description = Picker(channel, typeof(FloatEventResponse)).Q<HelpBox>(ChannelPickerDrawer.DescriptionName);

            Assert.That(description.text, Is.EqualTo("Dano recebido."));
            Assert.That(description.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void Picker_ChannelWithoutDescription_HidesHelpBox()
        {
            SerializedProperty channel = AddResponse(new FloatEventResponse()).FindPropertyRelative("channel");
            Assign(channel, _floatA);

            var description = Picker(channel, typeof(FloatEventResponse)).Q<HelpBox>(ChannelPickerDrawer.DescriptionName);

            Assert.That(description.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        // Drawer do campo declarado em owner, como a Unity o cria para a property.
        private static VisualElement Picker(SerializedProperty property, Type owner)
        {
            var drawer = new ChannelPickerDrawer();
            // PropertyDrawer.fieldInfo não tem setter público; a Unity o preenche ao escolher o drawer.
            FieldInfo backing = typeof(PropertyDrawer).GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(backing, Is.Not.Null, "PropertyDrawer.m_FieldInfo não existe nesta versão.");
            backing.SetValue(drawer, Field(owner, property.name));

            return drawer.CreatePropertyGUI(property);
        }

        private static void Assign(SerializedProperty property, EventChannel channel)
        {
            property.objectReferenceValue = channel;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDescription(EventChannel channel, string text)
        {
            var serialized = new SerializedObject(channel);
            serialized.FindProperty("description").stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Uma resposta nova no fim da lista do EventListener, como o Inspector faz.
        private SerializedProperty AddResponse(EventResponse value)
        {
            var serialized = new SerializedObject(_gameObject.AddComponent<EventListener>());
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            SerializedProperty response = responses.GetArrayElementAtIndex(0);
            response.managedReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return serialized.FindProperty(response.propertyPath);
        }

        private static SerializedProperty AddReaction(SerializedProperty owner, Reaction value)
        {
            SerializedProperty reactions = owner.FindPropertyRelative("reactions");
            reactions.arraySize++;
            SerializedProperty element = reactions.GetArrayElementAtIndex(reactions.arraySize - 1);
            element.managedReferenceValue = value;
            owner.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return owner.serializedObject.FindProperty(element.propertyPath);
        }

        private static SerializedProperty AddRaiseChannel(SerializedProperty owner, RaiseChannelReaction.Mode mode)
        {
            SerializedProperty reaction = AddReaction(owner, new RaiseChannelReaction());
            reaction.FindPropertyRelative("mode").intValue = (int)mode;
            reaction.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return reaction.FindPropertyRelative("target");
        }

        // Tipo declarado do campo, o mesmo que o drawer recebe em fieldInfo.
        private static Type FieldType(Type owner, string name) => Field(owner, name).FieldType;

        private static FieldInfo Field(Type owner, string name)
        {
            for (Type type = owner; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }

            throw new ArgumentException($"{owner.Name} não tem o campo {name}.");
        }
    }
}
