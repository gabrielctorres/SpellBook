using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Events.Editor
{
    /// <summary>Troca de canal de uma <see cref="EventEmission"/> num campo <c>[SerializeReference]</c> (ADR 0016, emenda).</summary>
    public static class EventEmissionEditing
    {
        /// <summary>
        /// Põe <paramref name="channel"/> na emissão de <paramref name="emission"/> e aplica. Com o mesmo tipo de payload,
        /// troca só o canal e mantém o payload; com outro tipo, recria a emissão por <see cref="EventEmission.CreateFor"/>.
        /// Com canal nulo, mantém a emissão (e o payload) sem canal.
        /// </summary>
        public static void SetChannel(SerializedProperty emission, EventChannel channel)
        {
            var current = emission.managedReferenceValue as EventEmission;
            if (channel == null)
            {
                if (current == null)
                {
                    return;
                }

                emission.FindPropertyRelative("channel").objectReferenceValue = null;
            }
            else if (current != null && current.PayloadType == channel.PayloadType)
            {
                emission.FindPropertyRelative("channel").objectReferenceValue = channel;
            }
            else
            {
                emission.managedReferenceValue = EventEmission.CreateFor(channel);
            }

            emission.serializedObject.ApplyModifiedProperties();
        }
    }

    /// <summary>
    /// Drawer de toda <see cref="EventEmission"/>: um campo de canal próprio, do tipo base <see cref="EventChannel"/>, e o
    /// payload da emissão atual. Escolher um canal de outro payload recria a emissão (<see cref="EventEmissionEditing"/>).
    /// </summary>
    [CustomPropertyDrawer(typeof(EventEmission), true)]
    public sealed class EventEmissionDrawer : PropertyDrawer
    {
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;

            var root = new VisualElement();
            var channelField = new ObjectField("Channel")
            {
                objectType = typeof(EventChannel),
                allowSceneObjects = false,
                tooltip = "Canal disparado. Um canal de outro payload recria a emissão.",
            };
            channelField.AddToClassList(ObjectField.alignedFieldUssClassName);
            var payload = new VisualElement();
            root.Add(channelField);
            root.Add(payload);
            string shownType = null;

            channelField.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                if (current != null)
                {
                    EventEmissionEditing.SetChannel(current, evt.newValue as EventChannel);
                    Refresh(current);
                }
            });

            void Refresh(SerializedProperty current)
            {
                var emission = current.managedReferenceValue as EventEmission;
                channelField.SetValueWithoutNotify(emission?.Channel);
                if (current.managedReferenceFullTypename == shownType)
                {
                    return;
                }

                shownType = current.managedReferenceFullTypename;
                payload.Clear();
                SerializedProperty payloadProperty = emission == null ? null : current.FindPropertyRelative("payload");
                bool empty = payloadProperty != null && payloadProperty.propertyType == SerializedPropertyType.Generic
                    && !payloadProperty.hasVisibleChildren;
                if (payloadProperty != null && !empty)
                {
                    var field = new PropertyField(payloadProperty.Copy());
                    payload.Add(field);
                    field.Bind(serializedObject);
                }
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
    }
}
