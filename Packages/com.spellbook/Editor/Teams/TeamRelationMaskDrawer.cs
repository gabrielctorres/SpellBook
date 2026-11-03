using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Teams.Editor
{
    /// <summary>
    /// Drawer de todo <see cref="TeamRelationMask"/>: o rótulo do campo e quatro caixas (Self, Aliado, Hostil e Neutro).
    /// </summary>
    [CustomPropertyDrawer(typeof(TeamRelationMask))]
    public sealed class TeamRelationMaskDrawer : PropertyDrawer
    {
        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        private static readonly (TeamRelationMask Flag, string Label)[] Flags =
        {
            (TeamRelationMask.Self, "Self"),
            (TeamRelationMask.Allied, "Aliado"),
            (TeamRelationMask.Hostile, "Hostil"),
            (TeamRelationMask.Neutral, "Neutro"),
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty target = property.Copy();
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Label(preferredLabel ?? property.displayName) { style = { minWidth = 120 } });

            foreach ((TeamRelationMask flag, string label) in Flags)
            {
                var toggle = new Toggle(label) { userData = flag };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    SetFlag(target, flag, evt.newValue);
                    Refresh(row, target);
                });
                row.Add(toggle);
            }

            Refresh(row, target);
            row.TrackPropertyValue(target, current => Refresh(row, current));
            return row;
        }

        // Lê a máscara de cada objeto selecionado e acerta as caixas. A caixa fica mista quando os objetos discordam
        // naquela relação. Roda ao criar a linha e a cada mudança da propriedade (Undo, outro Inspector, multisseleção).
        private static void Refresh(VisualElement row, SerializedProperty property)
        {
            Object[] targets = property.serializedObject.targetObjects;
            row.Query<Toggle>().ForEach(toggle =>
            {
                var flag = (TeamRelationMask)toggle.userData;
                bool first = Has(targets[0], property.propertyPath, flag);
                bool mixed = false;
                for (int i = 1; i < targets.Length && !mixed; i++)
                {
                    mixed = Has(targets[i], property.propertyPath, flag) != first;
                }

                toggle.SetValueWithoutNotify(first);
                toggle.showMixedValue = mixed;
            });
        }

        // Liga ou desliga uma relação na máscara de cada objeto selecionado, sem tocar nas outras relações de cada um,
        // num único passo de Undo.
        private static void SetFlag(SerializedProperty property, TeamRelationMask flag, bool on)
        {
            SerializedObject owner = property.serializedObject;
            int group = Undo.GetCurrentGroup();
            foreach (Object target in owner.targetObjects)
            {
                using var single = new SerializedObject(target);
                SerializedProperty one = single.FindProperty(property.propertyPath);
                var mask = (TeamRelationMask)one.intValue;
                one.intValue = (int)(on ? mask | flag : mask & ~flag);
                single.ApplyModifiedProperties();
            }

            Undo.CollapseUndoOperations(group);
            owner.Update();
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        private static bool Has(Object target, string path, TeamRelationMask flag)
        {
            using var single = new SerializedObject(target);
            return ((TeamRelationMask)single.FindProperty(path).intValue & flag) != 0;
        }
    }
}
