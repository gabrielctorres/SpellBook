using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Tags.Editor
{
    /// <summary>
    /// Drawer de <see cref="GameplayTagContainer"/> (R15): cabeçalho com o rótulo, a contagem e o botão de ajuda, e um
    /// <see cref="ListView"/> reordenável de <see cref="GameplayTagDefinition"/>. O "+" acrescenta uma linha vazia, não
    /// uma cópia da última. Não valida nem colore (ADR 0026); quem aponta problemas é o validador.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayTagContainer))]
    public sealed class GameplayTagContainerDrawer : PropertyDrawer
    {
        /// <summary>Classe USS do botão "?" que abre o <c>[HelpURL]</c> do tipo desenhado.</summary>
        public const string HelpButtonClass = "spellbook-help-button";

        internal const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedObject serializedObject = property.serializedObject;
            string tagsPath = property.FindPropertyRelative("tags").propertyPath;

            var root = new VisualElement();
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            var title = new Label(preferredLabel ?? property.displayName) { style = { flexGrow = 1 } };
            var count = new Label { tooltip = "Quantidade de linhas do conjunto." };
            header.Add(title);
            header.Add(count);
            header.Add(CreateHelpButton(typeof(GameplayTagContainer)));
            root.Add(header);

            var list = new ListView
            {
                bindingPath = tagsPath,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showAddRemoveFooter = true,
                showBorder = true,
                showBoundCollectionSize = false,
                showFoldoutHeader = false,
                makeItem = () => new ObjectField { objectType = typeof(GameplayTagDefinition), allowSceneObjects = false },
            };
            list.bindItem = (element, index) =>
            {
                SerializedProperty tags = serializedObject.FindProperty(tagsPath);
                if (tags != null && index < tags.arraySize)
                {
                    ((ObjectField)element).BindProperty(tags.GetArrayElementAtIndex(index).FindPropertyRelative("definition"));
                }
            };
            list.unbindItem = (element, _) => ((ObjectField)element).Unbind();
            list.onAdd = _ => AddEmptyRow(serializedObject.FindProperty(tagsPath));
            root.Add(list);

            SerializedProperty tracked = serializedObject.FindProperty(tagsPath);
            count.text = tracked.arraySize.ToString();
            root.TrackPropertyValue(tracked, current => count.text = current.arraySize.ToString());
            return root;
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        /// <summary>Botão "?" que abre o <c>[HelpURL]</c> de <paramref name="drawnType"/>; a URL fica em <c>userData</c>.</summary>
        internal static Button CreateHelpButton(Type drawnType)
        {
            string url = drawnType.GetCustomAttribute<HelpURLAttribute>()?.URL;
            var button = new Button(() => Application.OpenURL(url))
            {
                text = "?",
                tooltip = "Abre a página do manual deste tipo.",
                userData = url,
            };
            button.AddToClassList(HelpButtonClass);
            button.SetEnabled(!string.IsNullOrEmpty(url));
            return button;
        }

        // Acrescenta uma linha sem tag: o padrão do array copiaria a última e criaria uma repetida.
        private static void AddEmptyRow(SerializedProperty tags)
        {
            int index = tags.arraySize;
            tags.arraySize = index + 1;
            tags.GetArrayElementAtIndex(index).FindPropertyRelative("definition").objectReferenceValue = null;
            tags.serializedObject.ApplyModifiedProperties();
        }
    }
}
