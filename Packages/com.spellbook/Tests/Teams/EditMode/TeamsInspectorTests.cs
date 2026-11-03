using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Teams.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Teams.Tests
{
    /// <summary>Inspector do Teams: tabela da matriz (Design 3, CA17).</summary>
    public class TeamsInspectorTests
    {
        private TeamsFactory factory;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            factory = new TeamsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created.Where(o => o != null))
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
            factory.Dispose();
            Undo.ClearAll();
        }

        [Test]
        public void MatrixEditor_TableHasCellPerPair_EmptyCellShowsDefault()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(matrix);
            created.Add(editor);

            VisualElement root = editor.CreateInspectorGUI();
            List<VisualElement> cells = root.Query(className: TeamRelationMatrixEditor.CellClass).ToList();
            List<Button> mirrorAll = root.Query<Button>(TeamRelationMatrixEditor.MirrorAllName).ToList();

            Assert.That(cells, Has.Count.EqualTo(4));
            AssertCell(cells[0], TeamRelation.Allied, isDefault: true);
            AssertCell(cells[1], TeamRelation.Hostile, isDefault: false);
            AssertCell(cells[2], TeamRelation.Neutral, isDefault: true);
            AssertCell(cells[3], TeamRelation.Allied, isDefault: true);
            Assert.That(mirrorAll, Has.Count.EqualTo(1));
            Assert.That(cells.All(c => c.Q<Button>(TeamRelationMatrixEditor.MirrorName) != null), Is.True);
        }

        private static void AssertCell(VisualElement cell, TeamRelation expected, bool isDefault)
        {
            EnumField field = cell.Q<EnumField>();
            Assert.That(field.value, Is.EqualTo(expected));
            Assert.That(cell.ClassListContains(TeamRelationMatrixEditor.DefaultCellClass), Is.EqualTo(isDefault));
        }
    }
}
