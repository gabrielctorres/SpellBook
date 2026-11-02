using System.Reflection;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Events;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Bridges.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Bridges.Tests
{
    /// <summary>
    /// Drawer da <see cref="RaiseEventAction"/> (Spec do relay, RL7; Design 5): popup só com as fontes que servem para o
    /// canal, campo de atributo só com <c>AttributeValue</c> e aviso do payload fixo ignorado fora de <c>Fixed</c>. Confere a
    /// estrutura, sem painel. O <see cref="SerializedObject"/> fica vivo durante cada teste.
    /// </summary>
    public class RaiseEventDrawerTests
    {
        private GasFactory _gas;
        private EventsFactory _events;
        private SerializedObject _serializedObject;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _events = new EventsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _serializedObject?.Dispose();
            _gas.Dispose();
            _events.Dispose();
        }

        [Test]
        public void Drawer_ListsOnlySourcesSupportedByChannelType()
        {
            VisualElement floatRoot = Draw(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.Fixed);
            Assert.That(Popup(floatRoot).choices,
                Is.EqualTo(new[] { GasPayloadSource.Fixed, GasPayloadSource.AttributeValue }));

            VisualElement objectRoot = Draw(_events.CreateChannel<GameObjectEventChannel>(), GasPayloadSource.Fixed);
            Assert.That(Popup(objectRoot).choices,
                Is.EqualTo(new[] { GasPayloadSource.Fixed, GasPayloadSource.SourceObject, GasPayloadSource.TargetObject }));

            VisualElement abilityRoot = Draw(_events.CreateChannel<AbilityEventChannel>(), GasPayloadSource.EventData);
            Assert.That(Popup(abilityRoot).choices, Is.EqualTo(new[] { GasPayloadSource.Fixed, GasPayloadSource.EventData }));
            Assert.That(Popup(abilityRoot).value, Is.EqualTo(GasPayloadSource.EventData));
        }

        [Test]
        public void Drawer_ShowsAttributeFieldOnlyForAttributeValue()
        {
            VisualElement withAttribute = Draw(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.AttributeValue);
            Assert.That(Element(withAttribute, RaiseEventDrawer.AttributeName).style.display.value, Is.EqualTo(DisplayStyle.Flex));

            VisualElement fixedSource = Draw(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.Fixed);
            Assert.That(Element(fixedSource, RaiseEventDrawer.AttributeName).style.display.value, Is.EqualTo(DisplayStyle.None));

            VisualElement otherSource = Draw(_events.CreateChannel<StringEventChannel>(), GasPayloadSource.AbilityName);
            Assert.That(Element(otherSource, RaiseEventDrawer.AttributeName).style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void Drawer_FixedHidesNotice()
        {
            VisualElement fixedSource = Draw(_events.CreateChannel<StringEventChannel>(), GasPayloadSource.Fixed);
            Assert.That(Element(fixedSource, RaiseEventDrawer.NoticeName).style.display.value, Is.EqualTo(DisplayStyle.None));

            VisualElement dynamicSource = Draw(_events.CreateChannel<StringEventChannel>(), GasPayloadSource.AbilityName);
            Assert.That(Element(dynamicSource, RaiseEventDrawer.NoticeName), Is.InstanceOf<HelpBox>());
            Assert.That(Element(dynamicSource, RaiseEventDrawer.NoticeName).style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void Drawer_UsedInsideTypePicker_OnAbilityActions()
        {
            SerializedProperty action = CreateActionProperty(_events.CreateChannel<FloatEventChannel>(), GasPayloadSource.Fixed);

            VisualElement root = new TypePickerDrawer().CreatePropertyGUI(action);

            Assert.That(Popup(root).choices, Is.EqualTo(new[] { GasPayloadSource.Fixed, GasPayloadSource.AttributeValue }));
        }

        [Test]
        public void RaiseEventAction_HasHelpUrl()
        {
            var help = typeof(RaiseEventAction).GetCustomAttribute<HelpURLAttribute>();

            Assert.That(help, Is.Not.Null, "RaiseEventAction sem [HelpURL]");
            Assert.That(help.URL, Is.EqualTo(DocUrls.Gas + "#raiseeventaction"));
        }

        private VisualElement Draw(EventChannel channel, GasPayloadSource source)
        {
            return new RaiseEventDrawer().CreatePropertyGUI(CreateActionProperty(channel, source));
        }

        // Ação dentro de uma habilidade, configurada pelo SerializedProperty, como o Inspector faz.
        private SerializedProperty CreateActionProperty(EventChannel channel, GasPayloadSource source)
        {
            GameplayAbility ability = _gas.CreateAbility(new RaiseEventAction());
            _serializedObject?.Dispose();
            _serializedObject = new SerializedObject(ability);
            SerializedProperty action = _serializedObject.FindProperty(nameof(GameplayAbility.OnActivate)).GetArrayElementAtIndex(0);
            action.FindPropertyRelative("emission").managedReferenceValue = EventEmission.CreateFor(channel);
            action.FindPropertyRelative("payloadSource").enumValueIndex = (int)source;
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return action;
        }

        private static PopupField<GasPayloadSource> Popup(VisualElement root)
        {
            var popup = root.Q<PopupField<GasPayloadSource>>(RaiseEventDrawer.SourceName);
            Assert.That(popup, Is.Not.Null, "falta o popup de fontes");
            return popup;
        }

        private static VisualElement Element(VisualElement root, string name)
        {
            VisualElement element = root.Q(name);
            Assert.That(element, Is.Not.Null, $"falta o elemento {name}");
            return element;
        }
    }
}
