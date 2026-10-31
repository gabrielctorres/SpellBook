using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Seletor de todo campo <see cref="EventChannel"/> (Spec 4.7; CA21 e CA58): o campo do canal, um botão que abre a
    /// lista de canais do projeto filtrada pelo <see cref="ChannelCatalog"/> e a descrição do canal num
    /// <see cref="HelpBox"/>. Um canal arrastado que o filtro não aceita é recusado.
    /// </summary>
    [CustomPropertyDrawer(typeof(EventChannel), true)]
    public sealed class ChannelPickerDrawer : PropertyDrawer
    {
        public const string PickButtonName = "spellbook-channel-pick";
        public const string DescriptionName = "spellbook-channel-description";

        private const string NoneLabel = "(Nenhum)";
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            Type fieldType = ChannelCatalog.ElementType(fieldInfo.FieldType);

            var root = new VisualElement();
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var field = new ObjectField(preferredLabel ?? property.displayName)
            {
                objectType = fieldType,
                allowSceneObjects = false,
                tooltip = property.tooltip,
                style = { flexGrow = 1f, flexShrink = 1f },
            };
            field.AddToClassList(ObjectField.alignedFieldUssClassName);
            var pick = new Button { name = PickButtonName, text = "Escolher", tooltip = "Lista os canais que este campo aceita." };
            var description = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = DescriptionName };
            row.Add(field);
            row.Add(pick);
            root.Add(row);
            root.Add(description);

            pick.clicked += () => ShowMenu(pick, serializedObject.FindProperty(path), fieldType, Assign);
            field.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                var channel = evt.newValue as EventChannel;
                if (current != null && channel != null && !Accepts(current, fieldType, channel))
                {
                    Refresh(current);
                    return;
                }

                Assign(channel);
            });

            void Assign(EventChannel channel)
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                if (current == null) return;
                current.objectReferenceValue = channel;
                serializedObject.ApplyModifiedProperties();
                Refresh(current);
            }

            void Refresh(SerializedProperty current)
            {
                var channel = current.objectReferenceValue as EventChannel;
                field.SetValueWithoutNotify(channel);
                string text = DescriptionOf(channel);
                description.text = text;
                description.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            Refresh(property);
            root.TrackPropertyValue(property, Refresh);
            return root;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        // Canais de source que o campo aceita: a regra do ChannelCatalog.Filter e o tipo declarado do campo. Internal
        // para os testes, que conferem a recusa sem ligar o drawer a um painel (Spec, risco 33).
        internal static List<EventChannel> Candidates(SerializedProperty property, Type fieldType, IEnumerable<EventChannel> source)
        {
            List<EventChannel> channels = ChannelCatalog.Filter(source, ChannelCatalog.PayloadTypeFor(property, fieldType));
            channels.RemoveAll(channel => !fieldType.IsInstanceOfType(channel));
            return channels;
        }

        internal static bool Accepts(SerializedProperty property, Type fieldType, EventChannel channel)
        {
            return Candidates(property, fieldType, new[] { channel }).Count == 1;
        }

        private static void ShowMenu(VisualElement anchor, SerializedProperty property, Type fieldType, Action<EventChannel> assign)
        {
            if (property == null) return;
            var current = property.objectReferenceValue as EventChannel;
            List<EventChannel> channels = Candidates(property, fieldType, ChannelCatalog.FindAll());

            var menu = new GenericDropdownMenu();
            menu.AddItem(NoneLabel, current == null, () => assign(null));
            menu.AddSeparator(string.Empty);
            if (channels.Count == 0)
            {
                Type payload = ChannelCatalog.PayloadTypeFor(property, fieldType);
                menu.AddDisabledItem(payload == null ? "Nenhum canal no projeto" : $"Nenhum canal de {payload.Name}", false);
            }

            foreach (EventChannel channel in channels)
            {
                EventChannel picked = channel;
                menu.AddItem(channel.name, channel == current, () => assign(picked));
            }

            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }

        private static string DescriptionOf(EventChannel channel)
        {
            if (channel == null) return null;
            using var serialized = new SerializedObject(channel);
            return serialized.FindProperty("description")?.stringValue;
        }
    }
}
