using System;
using System.Linq;
using SpellBook.Core.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>
    /// Seletor de tipo de um campo <c>[SerializeReference]</c> marcado com <see cref="TypePickerAttribute"/> (ADR 0026, item 4;
    /// Spec, seção 4.10). Cabeçalho com o rótulo e o botão do tipo atual, que abre o <see cref="TypePickerPopup"/>; abaixo, um
    /// <see cref="PropertyField"/> por filho da instância. O tipo ausente não aparece aqui: o campo mostra "(Nenhum)" (D6).
    /// </summary>
    [CustomPropertyDrawer(typeof(TypePickerAttribute))]
    public sealed class TypePickerDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(Nenhum)";
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return new HelpBox("[TypePicker] só vale em campo [SerializeReference].", HelpBoxMessageType.Error);
            }

            TypePickerCatalog.EnsureUniqueInArray(property);

            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;
            Type baseType = TypePickerCatalog.ResolveFieldBaseType(property);

            var root = new VisualElement();
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var label = new Label(preferredLabel ?? property.displayName)
            {
                style = { minWidth = 120f, flexShrink = 0f },
            };
            var button = new Button { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleLeft } };
            var children = new VisualElement { style = { paddingLeft = 15f } };
            header.Add(label);
            header.Add(button);
            root.Add(header);
            root.Add(children);

            button.SetEnabled(baseType != null);
            button.clicked += () => TypePickerPopup.Show(button.worldBound, baseType, picked =>
            {
                SerializedProperty target = serializedObject.FindProperty(path);
                if (target != null)
                {
                    TypePickerCatalog.Assign(target, picked);
                }
            });

            string shownType = null;
            void Rebuild(SerializedProperty current)
            {
                string typename = current.managedReferenceFullTypename;
                if (typename == shownType)
                {
                    return;
                }

                shownType = typename;
                UpdateButton(button, label, current, baseType);
                children.Clear();
                SerializedProperty child = current.Copy();
                SerializedProperty end = current.GetEndProperty();
                for (bool more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end);
                     more = child.NextVisible(false))
                {
                    children.Add(new PropertyField(child.Copy()));
                }

                children.Bind(serializedObject);
            }

            Rebuild(property);
            root.TrackPropertyValue(property, Rebuild);
            return root;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        private static void UpdateButton(Button button, Label label, SerializedProperty property, Type baseType)
        {
            Type type = property.managedReferenceValue?.GetType();
            if (type == null)
            {
                button.text = NoneLabel;
                button.tooltip = label.tooltip = string.Empty;
                return;
            }

            TypePickerEntry entry = baseType == null ? default
                : TypePickerCatalog.GetCandidates(baseType).FirstOrDefault(e => e.Type == type);
            button.text = entry.Type != null ? entry.DisplayName : ObjectNames.NicifyVariableName(type.Name);
            button.tooltip = label.tooltip = entry.Summary ?? string.Empty;
        }
    }
}
