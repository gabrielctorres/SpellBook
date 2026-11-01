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
    /// <see cref="PropertyField"/> por filho da instância, ou o corpo do drawer do tipo concreto, quando existe. O tipo
    /// ausente não aparece aqui: o campo mostra "(Nenhum)" (D6).
    /// </summary>
    [CustomPropertyDrawer(typeof(TypePickerAttribute))]
    public sealed class TypePickerDrawer : PropertyDrawer
    {
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return new HelpBox("[TypePicker] só vale em campo [SerializeReference].", HelpBoxMessageType.Error);
            }

            TypePickerCatalog.EnsureUniqueInArray(property);

            var field = new TypePickerField(property, TypePickerCatalog.ResolveFieldBaseType(property), preferredLabel);
            field.Refresh(property);
            field.TrackPropertyValue(property, field.Refresh);
            return field;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }
    }

    /// <summary>
    /// Elemento do <see cref="TypePickerDrawer"/>: cabeçalho com o rótulo e o botão do tipo, e o corpo da instância abaixo.
    /// O drawer liga <see cref="Refresh"/> ao <c>TrackPropertyValue</c>; os testes chamam direto, sem painel.
    /// </summary>
    internal sealed class TypePickerField : VisualElement
    {
        private const string NoneLabel = "(Nenhum)";

        private readonly SerializedObject _serializedObject;
        private readonly Type _baseType;
        private readonly Label _label;
        private readonly Button _button;
        private readonly VisualElement _children;
        private string _shownType;

        internal TypePickerField(SerializedProperty property, Type baseType, string preferredLabel)
        {
            _serializedObject = property.serializedObject;
            _baseType = baseType;
            string path = property.propertyPath;

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            _label = new Label(preferredLabel ?? property.displayName)
            {
                style = { minWidth = 120f, flexShrink = 0f },
            };
            _button = new Button { style = { flexGrow = 1f, unityTextAlign = TextAnchor.MiddleLeft } };
            _children = new VisualElement { style = { paddingLeft = 15f } };
            header.Add(_label);
            header.Add(_button);
            Add(header);
            Add(_children);

            _button.SetEnabled(baseType != null);
            _button.clicked += () => TypePickerPopup.Show(_button.worldBound, baseType, picked =>
            {
                SerializedProperty target = _serializedObject.FindProperty(path);
                if (target != null)
                {
                    TypePickerCatalog.Assign(target, picked);
                }
            });
        }

        /// <summary>Refaz o botão e o corpo quando o tipo da instância mudou; com o mesmo tipo, não faz nada.</summary>
        internal void Refresh(SerializedProperty current)
        {
            string typename = current.managedReferenceFullTypename;
            if (typename == _shownType)
            {
                return;
            }

            _shownType = typename;
            UpdateButton(_button, _label, current, _baseType);
            _children.Clear();
            VisualElement typeBody = CreateTypeBody(current);
            if (typeBody != null)
            {
                _children.Add(typeBody);
                _children.Bind(_serializedObject);
                return;
            }

            SerializedProperty child = current.Copy();
            SerializedProperty end = current.GetEndProperty();
            for (bool more = child.NextVisible(true); more && !SerializedProperty.EqualContents(child, end);
                 more = child.NextVisible(false))
            {
                _children.Add(new PropertyField(child.Copy()));
            }

            _children.Bind(_serializedObject);
        }

        /// <summary>
        /// Corpo vindo do drawer do tipo concreto (emenda de 2026-11-01 da ADR 0026), ou <c>null</c> se o tipo não tem drawer
        /// ou se o drawer é só IMGUI. Sob o seletor, o drawer do tipo recebe a propriedade sem <c>fieldInfo</c> nem <c>attribute</c>.
        /// </summary>
        private static VisualElement CreateTypeBody(SerializedProperty property)
        {
            Type type = property.managedReferenceValue?.GetType();
            Type drawerType = type == null ? null : TypePickerCatalog.FindTypeDrawer(type);
            if (drawerType == null)
            {
                return null;
            }

            var drawer = (PropertyDrawer)Activator.CreateInstance(drawerType, true);
            return drawer.CreatePropertyGUI(property.Copy());
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
