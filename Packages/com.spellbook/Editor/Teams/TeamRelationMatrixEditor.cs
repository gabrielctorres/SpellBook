using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Teams.Editor
{
    /// <summary>
    /// Inspector da matriz: depois dos campos, uma tabela com uma linha por time de origem e uma coluna por time de
    /// destino. A célula vazia mostra em cinza o padrão que vale para o par.
    /// </summary>
    [CustomEditor(typeof(TeamRelationMatrix))]
    public sealed class TeamRelationMatrixEditor : SpellBookEditor
    {
        public const string CellClass = "spellbook-team-cell";
        public const string DefaultCellClass = "spellbook-team-cell-default";
        public const string MirrorName = "spellbook-team-mirror";
        public const string MirrorAllName = "spellbook-team-mirror-all";

        private const float CellWidth = 110;
        private const float HeaderWidth = 100;

        private VisualElement _table;

        protected override void BuildAfterDefault(VisualElement root)
        {
            root.Add(new Label("Relações (linha → coluna)") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8 } });
            _table = new VisualElement();
            root.Add(_table);
            root.Add(new Button(() => TeamMatrixEditing.MirrorAll(Matrix)) { name = MirrorAllName, text = "Espelhar tudo" });

            RebuildTable();
            root.TrackSerializedObjectValue(serializedObject, _ => RebuildTable());
        }

        private TeamRelationMatrix Matrix => (TeamRelationMatrix)target;

        private void RebuildTable()
        {
            serializedObject.Update();
            _table.Clear();
            List<TeamDefinition> teams = Teams();

            var header = Row();
            header.Add(new VisualElement { style = { width = HeaderWidth } });
            foreach (TeamDefinition to in teams)
            {
                header.Add(TeamLabel(to, CellWidth));
            }

            _table.Add(header);
            foreach (TeamDefinition from in teams)
            {
                var row = Row();
                row.Add(TeamLabel(from, HeaderWidth));
                foreach (TeamDefinition to in teams)
                {
                    row.Add(Cell(from, to));
                }

                _table.Add(row);
            }
        }

        private VisualElement Cell(TeamDefinition from, TeamDefinition to)
        {
            bool hasCell = TeamMatrixEditing.TryGet(Matrix, from, to, out TeamRelation relation);
            var cell = new VisualElement { style = { width = CellWidth, flexDirection = FlexDirection.Row } };
            cell.AddToClassList(CellClass);

            var field = new EnumField(hasCell ? relation : Matrix.GetRelation(from, to)) { style = { flexGrow = 1 } };
            field.RegisterValueChangedCallback(evt => TeamMatrixEditing.Set(Matrix, from, to, (TeamRelation)evt.newValue));
            cell.Add(field);

            if (!hasCell)
            {
                cell.AddToClassList(DefaultCellClass);
                field.style.opacity = 0.5f;
                field.tooltip = "Sem célula: vale o padrão da matriz.";
            }

            cell.Add(new Button(() => TeamMatrixEditing.Mirror(Matrix, from, to))
            {
                name = MirrorName, text = "⇄", tooltip = $"Espelhar: copia {from.name} → {to.name} em {to.name} → {from.name}.",
            });
            var clear = new Button(() => TeamMatrixEditing.Clear(Matrix, from, to)) { text = "×", tooltip = "Limpar a célula." };
            clear.SetEnabled(hasCell);
            cell.Add(clear);
            return cell;
        }

        private List<TeamDefinition> Teams()
        {
            SerializedProperty list = serializedObject.FindProperty("teams");
            var teams = new List<TeamDefinition>();
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is TeamDefinition team && !teams.Contains(team))
                {
                    teams.Add(team);
                }
            }

            return teams;
        }

        private static VisualElement Row()
        {
            return new VisualElement { style = { flexDirection = FlexDirection.Row } };
        }

        private static Label TeamLabel(TeamDefinition team, float width)
        {
            string text = string.IsNullOrWhiteSpace(team.DisplayName) ? team.name : team.DisplayName;
            var label = new Label(text) { style = { width = width, color = team.Color } };
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }
    }
}
