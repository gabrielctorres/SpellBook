using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.Teams.Editor
{
    /// <summary>Inspector do time: uma faixa com a cor do time antes dos campos.</summary>
    [CustomEditor(typeof(TeamDefinition))]
    [CanEditMultipleObjects]
    internal sealed class TeamDefinitionEditor : SpellBookEditor
    {
        protected override void BuildBeforeDefault(VisualElement root)
        {
            var swatch = new VisualElement { style = { height = 6, marginBottom = 4 } };
            swatch.style.backgroundColor = ((TeamDefinition)target).Color;
            swatch.TrackPropertyValue(serializedObject.FindProperty("color"),
                property => swatch.style.backgroundColor = property.colorValue);
            root.Add(swatch);
        }
    }

    /// <summary>Inspector do membro: o time e a matriz, com a validação do <see cref="SpellBookEditor"/>.</summary>
    [CustomEditor(typeof(TeamMember))]
    [CanEditMultipleObjects]
    internal sealed class TeamMemberEditor : SpellBookEditor { }
}
