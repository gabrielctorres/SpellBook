using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SpellBook.Core.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>Regra de visibilidade do <see cref="ShowIfAttribute"/> (ADR 0026, item 5; Spec, seção 4.10).</summary>
    public static class ShowIfRule
    {
        private const string ArrayElementSuffix = ".Array.data[";

        private static readonly ConditionalWeakTable<ShowIfAttribute, object> Warned =
            new ConditionalWeakTable<ShowIfAttribute, object>();

        /// <summary>
        /// Verdadeiro quando o irmão do campo é igual ao valor do atributo: irmão <c>bool</c> compara com <c>bool</c>; irmão
        /// <c>enum</c> compara <c>intValue</c> com <c>Convert.ToInt32(value)</c>. Irmão inexistente ou de outro tipo deixa o
        /// campo visível e avisa uma vez por campo.
        /// </summary>
        public static bool IsVisible(SerializedProperty field, ShowIfAttribute attr)
        {
            SerializedProperty sibling = FindSibling(field, attr);
            if (sibling != null && sibling.propertyType == SerializedPropertyType.Boolean && attr.Value is bool expected)
            {
                return sibling.boolValue == expected;
            }

            if (sibling != null && sibling.propertyType == SerializedPropertyType.Enum && (attr.Value is Enum || attr.Value is int))
            {
                return sibling.intValue == Convert.ToInt32(attr.Value);
            }

            WarnOnce(field, attr);
            return true;
        }

        /// <summary>Irmão no mesmo objeto ou struct; para um elemento de lista, irmão da lista. Nulo se não existir.</summary>
        internal static SerializedProperty FindSibling(SerializedProperty field, ShowIfAttribute attr)
        {
            string path = field.propertyPath;
            int element = path.LastIndexOf(ArrayElementSuffix, StringComparison.Ordinal);
            if (element >= 0 && path.EndsWith("]", StringComparison.Ordinal) && path.IndexOf('.', element + ArrayElementSuffix.Length) < 0)
            {
                path = path.Substring(0, element);
            }

            int dot = path.LastIndexOf('.');
            string siblingPath = dot < 0 ? attr.SiblingField : path.Substring(0, dot + 1) + attr.SiblingField;
            return field.serializedObject.FindProperty(siblingPath);
        }

        private static void WarnOnce(SerializedProperty field, ShowIfAttribute attr)
        {
            if (Warned.TryGetValue(attr, out _))
            {
                return;
            }

            Warned.Add(attr, null);
            Object context = field.serializedObject.targetObject;
            Debug.LogWarning($"[ShowIf] O campo irmão '{attr.SiblingField}' de '{field.propertyPath}' não existe ou não é " +
                             "bool/enum compatível com o valor. O campo fica sempre visível.", context);
        }
    }

    /// <summary>
    /// Drawer do <see cref="ShowIfAttribute"/>. Monta o campo pelo tipo da propriedade (forma (b) da Spec, 4.10), porque a
    /// <see cref="PropertyField"/> pública não contorna o drawer e cairia de novo aqui (risco 25). A visibilidade segue o
    /// irmão por <c>TrackPropertyValue</c>.
    /// </summary>
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public sealed class ShowIfDrawer : PropertyDrawer
    {
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (ShowIfAttribute)attribute;
            SerializedObject serializedObject = property.serializedObject;
            string path = property.propertyPath;

            var root = new VisualElement();
            root.Add(BuildField(property, preferredLabel ?? property.displayName));

            void Refresh()
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                if (current != null)
                {
                    root.style.display = ShowIfRule.IsVisible(current, attr) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            Refresh();
            SerializedProperty sibling = ShowIfRule.FindSibling(property, attr);
            if (sibling != null)
            {
                root.TrackPropertyValue(sibling, _ => Refresh());
            }

            return root;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        private VisualElement BuildField(SerializedProperty property, string label)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return Bound(new FloatField(label), property);
                case SerializedPropertyType.Integer:
                    return Bound(new IntegerField(label), property);
                case SerializedPropertyType.Boolean:
                    return Bound(new Toggle(label), property);
                case SerializedPropertyType.String:
                    return Bound(new TextField(label), property);
                case SerializedPropertyType.Color:
                    return Bound(new ColorField(label), property);
                case SerializedPropertyType.Enum:
                    return Bound(new EnumField(label, (Enum)Enum.ToObject(ElementType(), 0)), property);
                case SerializedPropertyType.ObjectReference:
                    var objectField = new ObjectField(label)
                    {
                        objectType = ElementType(),
                        allowSceneObjects = !EditorUtility.IsPersistent(property.serializedObject.targetObject),
                    };
                    return Bound(objectField, property);
                case SerializedPropertyType.Generic when !property.isArray:
                    return BuildChildren(property, label);
                default:
                    return new HelpBox($"[ShowIf] não desenha campos do tipo {property.propertyType}.", HelpBoxMessageType.Warning);
            }
        }

        private static VisualElement BuildChildren(SerializedProperty property, string label)
        {
            var foldout = new Foldout { text = label, value = property.isExpanded, tooltip = property.tooltip };
            string path = property.propertyPath;
            SerializedObject serializedObject = property.serializedObject;
            foldout.RegisterValueChangedCallback(evt =>
            {
                SerializedProperty current = serializedObject.FindProperty(path);
                if (evt.target == foldout && current != null)
                {
                    current.isExpanded = evt.newValue;
                }
            });

            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            for (bool more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end); more = child.NextVisible(false))
            {
                foldout.Add(new PropertyField(child.Copy()));
            }

            foldout.Bind(serializedObject);
            return foldout;
        }

        private static VisualElement Bound<T>(BaseField<T> field, SerializedProperty property)
        {
            field.tooltip = property.tooltip;
            field.AddToClassList(BaseField<T>.alignedFieldUssClassName);
            field.BindProperty(property);
            return field;
        }

        private Type ElementType()
        {
            Type type = fieldInfo.FieldType;
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : type;
        }
    }
}
