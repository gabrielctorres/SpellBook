using System;
using System.Collections.Generic;
using SpellBook.Events;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Bridges.Editor
{
    /// <summary>
    /// Drawer da <see cref="RaiseEventAction"/> (Spec do relay, Design 5): a emissão pelo drawer dela, um popup só com as
    /// <see cref="GasPayloadSource"/> que servem para o tipo do canal, o atributo só com <c>AttributeValue</c> e um aviso de
    /// que o payload fixo é ignorado fora de <c>Fixed</c>. Sob o <c>[TypePicker]</c> da lista de ações, o seletor usa este
    /// drawer (ADR 0026, emenda de 2026-11-01); ele não usa <c>fieldInfo</c> nem <c>attribute</c>.
    /// </summary>
    [CustomPropertyDrawer(typeof(RaiseEventAction))]
    public sealed class RaiseEventDrawer : PropertyDrawer
    {
        public const string SourceName = "raise-event-payload-source";
        public const string AttributeName = "raise-event-attribute";
        public const string NoticeName = "raise-event-fixed-ignored";

        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";
        private const string UnsupportedSuffix = " (não serve para este canal)";

        private static readonly GasPayloadSource[] AllSources = (GasPayloadSource[])Enum.GetValues(typeof(GasPayloadSource));

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            Type payloadType = null;

            var root = new VisualElement();
            var popup = new PopupField<GasPayloadSource>("Payload Source")
            {
                name = SourceName,
                tooltip = "De onde vem o payload. Só aparecem as fontes que servem para o tipo do canal.",
                formatListItemCallback = source => Format(source, payloadType),
                formatSelectedValueCallback = source => Format(source, payloadType),
            };
            popup.AddToClassList(BaseField<GasPayloadSource>.alignedFieldUssClassName);
            var attributeField = new PropertyField(property.FindPropertyRelative("attribute")) { name = AttributeName };
            var notice = new HelpBox("O payload fixo da emissão é ignorado: o valor vem da fonte escolhida.",
                HelpBoxMessageType.Info) { name = NoticeName };

            root.Add(new PropertyField(property.FindPropertyRelative("emission")));
            root.Add(popup);
            root.Add(attributeField);
            root.Add(notice);

            popup.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                if (current == null)
                {
                    return;
                }

                current.FindPropertyRelative("payloadSource").enumValueIndex = (int)evt.newValue;
                serializedObject.ApplyModifiedProperties();
                Refresh(current);
            });

            void Refresh(SerializedProperty current)
            {
                var emission = current.FindPropertyRelative("emission").managedReferenceValue as EventEmission;
                var source = (GasPayloadSource)current.FindPropertyRelative("payloadSource").enumValueIndex;
                payloadType = emission?.PayloadType;
                popup.choices = Choices(payloadType, source);
                popup.SetValueWithoutNotify(source);
                attributeField.style.display = source == GasPayloadSource.AttributeValue ? DisplayStyle.Flex : DisplayStyle.None;
                notice.style.display = source == GasPayloadSource.Fixed ? DisplayStyle.None : DisplayStyle.Flex;
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

        // As fontes que servem para o canal, na ordem do enum. A fonte atual entra mesmo sem servir, para o popup mostrá-la
        // marcada; o validador acusa o erro.
        private static List<GasPayloadSource> Choices(Type payloadType, GasPayloadSource current)
        {
            var choices = new List<GasPayloadSource>();
            foreach (GasPayloadSource source in AllSources)
            {
                if (source == current || GasPayloadSourceUtility.Supports(source, payloadType))
                {
                    choices.Add(source);
                }
            }

            return choices;
        }

        private static string Format(GasPayloadSource source, Type payloadType)
        {
            string text = ObjectNames.NicifyVariableName(source.ToString());
            return GasPayloadSourceUtility.Supports(source, payloadType) ? text : text + UnsupportedSuffix;
        }
    }
}
