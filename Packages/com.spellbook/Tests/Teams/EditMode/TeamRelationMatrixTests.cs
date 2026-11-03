using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Teams.Tests
{
    /// <summary>Matriz de relações e máscara (CA1, CA3 em parte, CA4, CA5, CA7 e CA16 em parte).</summary>
    public class TeamRelationMatrixTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        [Test]
        public void Matrix_Hostile_Allied_Neutral_PerCell()
        {
            TeamDefinition red = Team("Red");
            TeamDefinition blue = Team("Blue");
            TeamDefinition green = Team("Green");
            TeamRelationMatrix matrix = Matrix(new[] { red, blue, green }, TeamRelation.Hostile);
            AddCell(matrix, red, blue, TeamRelation.Hostile);
            AddCell(matrix, red, green, TeamRelation.Allied);
            AddCell(matrix, blue, green, TeamRelation.Neutral);

            Assert.That(matrix.GetRelation(red, blue), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(red, green), Is.EqualTo(TeamRelation.Allied));
            Assert.That(matrix.GetRelation(blue, green), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void Matrix_SameTeamWithoutCell_IsAllied_DefaultChangeable()
        {
            TeamDefinition red = Team("Red");
            TeamRelationMatrix allied = Matrix(new[] { red }, TeamRelation.Hostile);
            TeamRelationMatrix changed = Matrix(new[] { red }, TeamRelation.Neutral);
            SetEnum(changed, "sameTeamRelation", TeamRelation.Hostile);

            Assert.That(allied.GetRelation(red, red), Is.EqualTo(TeamRelation.Allied));
            Assert.That(changed.GetRelation(red, red), Is.EqualTo(TeamRelation.Hostile));
        }

        [Test]
        public void Matrix_MissingPair_UsesDefault()
        {
            TeamDefinition red = Team("Red");
            TeamDefinition blue = Team("Blue");
            TeamRelationMatrix hostileDefault = Matrix(new[] { red, blue }, TeamRelation.Hostile);
            TeamRelationMatrix alliedDefault = Matrix(new[] { red, blue }, TeamRelation.Allied);

            Assert.That(hostileDefault.GetRelation(red, blue), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(alliedDefault.GetRelation(red, blue), Is.EqualTo(TeamRelation.Allied));
        }

        [Test]
        public void Matrix_IsDirectional()
        {
            TeamDefinition red = Team("Red");
            TeamDefinition blue = Team("Blue");
            TeamRelationMatrix matrix = Matrix(new[] { red, blue }, TeamRelation.Neutral);
            AddCell(matrix, red, blue, TeamRelation.Hostile);

            Assert.That(matrix.GetRelation(red, blue), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(blue, red), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void Matrix_NullOrUnknownTeam_UsesNoTeamRelation()
        {
            TeamDefinition red = Team("Red");
            TeamDefinition outsider = Team("Outsider");
            TeamRelationMatrix matrix = Matrix(new[] { red }, TeamRelation.Allied);
            SetEnum(matrix, "noTeamRelation", TeamRelation.Hostile);

            Assert.That(matrix.GetRelation(null, red), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(red, null), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(null, null), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(red, outsider), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(matrix.GetRelation(outsider, outsider), Is.EqualTo(TeamRelation.Hostile));
        }

        [Test]
        public void Mask_EachCombination_SelfOnlyWhenSame_EmptyMatchesNothing()
        {
            var relations = new[] { TeamRelation.Allied, TeamRelation.Hostile, TeamRelation.Neutral };
            var flags = new[] { TeamRelationMask.Allied, TeamRelationMask.Hostile, TeamRelationMask.Neutral };

            for (int bits = 0; bits < 16; bits++)
            {
                var mask = (TeamRelationMask)bits;
                bool hasSelf = (bits & (int)TeamRelationMask.Self) != 0;
                for (int i = 0; i < relations.Length; i++)
                {
                    bool hasRelation = (bits & (int)flags[i]) != 0;

                    Assert.That(mask.Matches(relations[i], isSelf: true), Is.EqualTo(hasSelf), $"{mask}, {relations[i]}, self");
                    Assert.That(mask.Matches(relations[i], isSelf: false), Is.EqualTo(hasRelation), $"{mask}, {relations[i]}");
                }
            }

            Assert.That(TeamRelationMask.None.Matches(TeamRelation.Hostile, isSelf: false), Is.False);
            Assert.That(TeamRelationMask.None.Matches(TeamRelation.Allied, isSelf: true), Is.False);
        }

        [Test]
        public void GetRelation_DoesNotAllocate_AfterFirstCall()
        {
            TeamDefinition red = Team("Red");
            TeamDefinition blue = Team("Blue");
            TeamDefinition outsider = Team("Outsider");
            TeamRelationMatrix matrix = Matrix(new[] { red, blue }, TeamRelation.Neutral);
            AddCell(matrix, red, blue, TeamRelation.Hostile);
            matrix.GetRelation(red, blue);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    matrix.GetRelation(red, blue);
                    matrix.GetRelation(blue, blue);
                    matrix.GetRelation(red, outsider);
                    matrix.GetRelation(null, red);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        private TeamDefinition Team(string displayName)
        {
            var team = ScriptableObject.CreateInstance<TeamDefinition>();
            team.name = displayName;
            created.Add(team);
            var serialized = new SerializedObject(team);
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return team;
        }

        private TeamRelationMatrix Matrix(TeamDefinition[] teams, TeamRelation defaultRelation)
        {
            var matrix = ScriptableObject.CreateInstance<TeamRelationMatrix>();
            created.Add(matrix);
            var serialized = new SerializedObject(matrix);
            SerializedProperty list = serialized.FindProperty("teams");
            list.arraySize = teams.Length;
            for (int i = 0; i < teams.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = teams[i];
            }

            serialized.FindProperty("defaultRelation").enumValueIndex = (int)defaultRelation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return matrix;
        }

        private static void AddCell(TeamRelationMatrix matrix, TeamDefinition from, TeamDefinition to, TeamRelation relation)
        {
            var serialized = new SerializedObject(matrix);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("from").objectReferenceValue = from;
            entry.FindPropertyRelative("to").objectReferenceValue = to;
            entry.FindPropertyRelative("relation").enumValueIndex = (int)relation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(TeamRelationMatrix matrix, string field, TeamRelation relation)
        {
            var serialized = new SerializedObject(matrix);
            serialized.FindProperty(field).enumValueIndex = (int)relation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
