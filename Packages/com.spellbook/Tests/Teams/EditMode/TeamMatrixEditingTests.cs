using NUnit.Framework;
using SpellBook.Teams.Editor;
using SpellBook.Testing;
using UnityEditor;

namespace SpellBook.Teams.Tests
{
    /// <summary>Edição da matriz pelo Inspector: célula, "Espelhar", "Espelhar tudo" e limpar, com Undo (Design 3).</summary>
    public class TeamMatrixEditingTests
    {
        private TeamsFactory factory;
        private TeamDefinition red;
        private TeamDefinition blue;
        private TeamDefinition green;
        private TeamRelationMatrix matrix;

        [SetUp]
        public void SetUp()
        {
            factory = new TeamsFactory();
            red = factory.CreateTeam("Red");
            blue = factory.CreateTeam("Blue");
            green = factory.CreateTeam("Green");
            matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue, green);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            factory.Dispose();
        }

        [Test]
        public void Set_CreatesThenUpdatesCell()
        {
            TeamMatrixEditing.Set(matrix, red, blue, TeamRelation.Hostile);
            TeamMatrixEditing.Set(matrix, red, blue, TeamRelation.Allied);

            Assert.That(TeamMatrixEditing.TryGet(matrix, red, blue, out TeamRelation relation), Is.True);
            Assert.That(relation, Is.EqualTo(TeamRelation.Allied));
            Assert.That(new SerializedObject(matrix).FindProperty("entries").arraySize, Is.EqualTo(1));
            Assert.That(matrix.GetRelation(red, blue), Is.EqualTo(TeamRelation.Allied));
            Assert.That(TeamMatrixEditing.TryGet(matrix, blue, red, out _), Is.False);
        }

        [Test]
        public void Mirror_CopiesAtoBIntoBtoA_Undoable()
        {
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            Undo.IncrementCurrentGroup();

            TeamMatrixEditing.Mirror(matrix, red, blue);
            TeamRelation mirrored = matrix.GetRelation(blue, red);
            Undo.PerformUndo();

            Assert.That(mirrored, Is.EqualTo(TeamRelation.Hostile));
            Assert.That(TeamMatrixEditing.TryGet(matrix, blue, red, out _), Is.False, "o Undo não desfez o Espelhar");
            Assert.That(matrix.GetRelation(blue, red), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(matrix.GetRelation(red, blue), Is.EqualTo(TeamRelation.Hostile));
        }

        [Test]
        public void Mirror_EmptySource_ClearsTarget()
        {
            factory.SetCell(matrix, blue, red, TeamRelation.Allied);

            TeamMatrixEditing.Mirror(matrix, red, blue);

            Assert.That(TeamMatrixEditing.TryGet(matrix, blue, red, out _), Is.False);
            Assert.That(matrix.GetRelation(blue, red), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void MirrorAll_MakesMatrixSymmetric()
        {
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            factory.SetCell(matrix, blue, red, TeamRelation.Allied);
            factory.SetCell(matrix, green, red, TeamRelation.Hostile);
            factory.SetCell(matrix, red, red, TeamRelation.Hostile);

            TeamMatrixEditing.MirrorAll(matrix);

            TeamDefinition[] teams = { red, blue, green };
            foreach (TeamDefinition from in teams)
            {
                foreach (TeamDefinition to in teams)
                {
                    Assert.That(matrix.GetRelation(from, to), Is.EqualTo(matrix.GetRelation(to, from)),
                        $"{from.name} → {to.name}");
                }
            }

            Assert.That(matrix.GetRelation(blue, red), Is.EqualTo(TeamRelation.Hostile), "vale a célula do time que vem antes");
            Assert.That(matrix.GetRelation(red, green), Is.EqualTo(TeamRelation.Hostile), "sem a célula de cima, vale a outra");
            Assert.That(matrix.GetRelation(red, red), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(blue, green), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void Clear_FallsBackToDefault()
        {
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            factory.SetCell(matrix, red, blue, TeamRelation.Allied);
            factory.SetCell(matrix, red, red, TeamRelation.Hostile);

            TeamMatrixEditing.Clear(matrix, red, blue);
            TeamMatrixEditing.Clear(matrix, red, red);

            Assert.That(TeamMatrixEditing.TryGet(matrix, red, blue, out _), Is.False);
            Assert.That(new SerializedObject(matrix).FindProperty("entries").arraySize, Is.EqualTo(0));
            Assert.That(matrix.GetRelation(red, blue), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(matrix.GetRelation(red, red), Is.EqualTo(TeamRelation.Allied));
        }
    }
}
