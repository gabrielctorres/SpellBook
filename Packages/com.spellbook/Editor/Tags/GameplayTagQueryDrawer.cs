using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Tags.Editor
{
    /// <summary>
    /// Drawer de <see cref="GameplayTagQuery"/> (R15): um <see cref="Foldout"/> com o rótulo do campo e o botão de
    /// ajuda, e as listas "Todas", "Alguma" e "Nenhuma" desenhadas pelo <see cref="GameplayTagContainerDrawer"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTagQuery))]
    public sealed class GameplayTagQueryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty expanded = property.Copy();
            var root = new VisualElement();
            var foldout = new Foldout { text = preferredLabel ?? property.displayName, value = property.isExpanded };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout)
                {
                    expanded.isExpanded = evt.newValue;
                }
            });
            foldout.Add(new PropertyField(property.FindPropertyRelative("all"), "Todas"));
            foldout.Add(new PropertyField(property.FindPropertyRelative("any"), "Alguma"));
            foldout.Add(new PropertyField(property.FindPropertyRelative("none"), "Nenhuma"));
            root.Add(foldout);

            Button help = GameplayTagContainerDrawer.CreateHelpButton(typeof(GameplayTagQuery));
            help.style.position = Position.Absolute;
            help.style.top = 0;
            help.style.right = 0;
            root.Add(help);
            return root;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, GameplayTagContainerDrawer.NeedsUIToolkit, MessageType.Warning);
        }
    }
}
