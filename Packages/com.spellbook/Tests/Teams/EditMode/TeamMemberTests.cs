using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SpellBook.Teams.Tests
{
    /// <summary>Membros, consultas e troca de time em runtime (CA2, CA3, CA6, CA8, CA16 em parte e CA19).</summary>
    public class TeamMemberTests
    {
        // Busca por cena (Find, FindObjectOfType, FindAnyObjectByType...) e campo ou propriedade estática de instância única.
        private static readonly Regex Forbidden =
            new Regex(@"\bFind\w*\s*[(<]|\bstatic\b.*\b(s_|_)?[Ii]nstance\b");

        private TeamsFactory factory;

        [SetUp]
        public void SetUp()
        {
            factory = new TeamsFactory();
        }

        [TearDown]
        public void TearDown()
        {
            factory.Dispose();
        }

        [Test]
        public void IsAllied_IsHostile_IsNeutral_BetweenMembers()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamDefinition green = factory.CreateTeam("Green");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue, green);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            TeamMember redA = factory.CreateMember(red, matrix);
            TeamMember redB = factory.CreateMember(red, matrix);
            TeamMember blueMember = factory.CreateMember(blue, matrix);
            TeamMember greenMember = factory.CreateMember(green, matrix);

            Assert.That(redA.IsAllied(redB), Is.True);
            Assert.That(redA.IsHostile(blueMember), Is.True);
            Assert.That(redA.IsNeutral(greenMember), Is.True);
            Assert.That(redA.IsHostile(redB), Is.False);
            Assert.That(redA.IsAllied(blueMember), Is.False);
            Assert.That(redA.IsNeutral(blueMember), Is.False);
        }

        [Test]
        public void SameMember_ReturnsSameTeamRelation()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Hostile, red, blue);
            factory.SetCell(matrix, blue, blue, TeamRelation.Neutral);
            TeamMember redMember = factory.CreateMember(red, matrix);
            TeamMember blueMember = factory.CreateMember(blue, matrix);

            Assert.That(redMember.GetRelation(redMember), Is.EqualTo(TeamRelation.Allied));
            Assert.That(blueMember.GetRelation(blueMember), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(TeamQueries.Matches(TeamRelationMask.Self, redMember, redMember), Is.True);
            Assert.That(TeamQueries.Matches(TeamRelationMask.Allied, redMember, redMember), Is.False);
        }

        [Test]
        public void NoMemberOrNoTeam_UsesNoTeamRelation_NoException()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Allied, red);
            factory.SetNoTeamRelation(matrix, TeamRelation.Hostile);
            TeamMember redMember = factory.CreateMember(red, matrix);
            TeamMember noTeam = factory.CreateMember(null, matrix);

            Assert.That(redMember.GetRelation(null), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(((ITeamMember)null).GetRelation(redMember), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(redMember.GetRelation(noTeam), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(noTeam.GetRelation(redMember), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(((ITeamMember)null).GetRelation(null), Is.EqualTo(TeamRelation.Neutral));
            Assert.That(TeamQueries.Matches(TeamRelationMask.Self, null, null), Is.False);
        }

        [Test]
        public void MatrixFromAsker_ThenOther_ThenNeutral()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix hostile = factory.CreateMatrix(TeamRelation.Hostile, red, blue);
            TeamRelationMatrix allied = factory.CreateMatrix(TeamRelation.Allied, red, blue);
            TeamMember redHostile = factory.CreateMember(red, hostile);
            TeamMember blueAllied = factory.CreateMember(blue, allied);
            TeamMember redNoMatrix = factory.CreateMember(red, null);
            TeamMember blueNoMatrix = factory.CreateMember(blue, null);

            Assert.That(redHostile.GetRelation(blueAllied), Is.EqualTo(TeamRelation.Hostile));
            Assert.That(blueAllied.GetRelation(redHostile), Is.EqualTo(TeamRelation.Allied));
            Assert.That(redNoMatrix.GetRelation(blueAllied), Is.EqualTo(TeamRelation.Allied));
            Assert.That(redNoMatrix.GetRelation(blueNoMatrix), Is.EqualTo(TeamRelation.Neutral));
        }

        [Test]
        public void SetTeam_FiresOnce_SameTeamDoesNot_ListenerChangingAgainIsSafe()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamDefinition green = factory.CreateTeam("Green");
            TeamMember member = factory.CreateMember(red, null);
            var calls = new List<(TeamMember member, TeamDefinition before, TeamDefinition after, TeamDefinition current)>();
            member.OnTeamChanged += (m, before, after) => calls.Add((m, before, after, m.Team));

            member.SetTeam(blue);
            member.SetTeam(blue);

            Assert.That(calls.Count, Is.EqualTo(1));
            Assert.That(calls[0], Is.EqualTo((member, red, blue, blue)));

            calls.Clear();
            member.OnTeamChanged += (m, before, after) =>
            {
                if (after == green) m.SetTeam(red);
            };
            var late = new List<(TeamDefinition before, TeamDefinition after)>();
            member.OnTeamChanged += (m, before, after) => late.Add((before, after));

            Assert.That(() => member.SetTeam(green), Throws.Nothing);
            Assert.That(member.Team, Is.EqualTo(red));
            Assert.That(calls, Is.EqualTo(new[] { (member, blue, green, green), (member, green, red, red) }));
            Assert.That(late, Is.EquivalentTo(new[] { (blue, green), (green, red) }));
        }

        [Test]
        public void CustomITeamMember_ParticipatesInQueries()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            factory.SetCell(matrix, blue, red, TeamRelation.Hostile);
            var custom = new CustomMember(red, matrix);
            TeamMember blueMember = factory.CreateMember(blue, null);

            Assert.That(custom.IsHostile(blueMember), Is.True);
            Assert.That(blueMember.IsHostile(custom), Is.True);
            Assert.That(TeamQueries.Matches(TeamRelationMask.Hostile, custom, blueMember), Is.True);
            Assert.That(TeamQueries.Matches(TeamRelationMask.Self, custom, custom), Is.True);
        }

        [Test]
        public void Queries_DoNotAllocate()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            TeamMember redMember = factory.CreateMember(red, matrix);
            TeamMember blueMember = factory.CreateMember(blue, null);
            ITeamMember custom = new CustomMember(blue, matrix);
            redMember.GetRelation(blueMember);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    redMember.GetRelation(blueMember);
                    blueMember.IsAllied(redMember);
                    redMember.IsHostile(custom);
                    redMember.IsNeutral(null);
                    TeamQueries.Matches(TeamRelationMask.Hostile | TeamRelationMask.Self, redMember, blueMember);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TeamsSources_NoSingletonOrFind()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(TeamMemberTests).Assembly);
            Assert.That(info, Is.Not.Null, "rode os testes no host com o pacote em file: e testables");
            string root = info.resolvedPath;
            string folder = Path.Combine(root, "Runtime", "Teams");
            Assert.That(Directory.Exists(folder), Is.True, $"pasta não encontrada: {folder}");
            var violations = new List<string>();

            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (Forbidden.IsMatch(lines[i]))
                    {
                        string relative = file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
                        violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }

            Assert.That(violations, Is.Empty);
        }

        /// <summary>Membro próprio, sem <c>MonoBehaviour</c> do pacote (CA8).</summary>
        private sealed class CustomMember : ITeamMember
        {
            public CustomMember(TeamDefinition team, TeamRelationMatrix matrix)
            {
                Team = team;
                Matrix = matrix;
            }

            public TeamDefinition Team { get; }
            public TeamRelationMatrix Matrix { get; }
        }
    }
}
