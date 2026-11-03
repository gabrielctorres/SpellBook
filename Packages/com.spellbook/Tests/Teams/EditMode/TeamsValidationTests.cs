using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.Teams.Tests
{
    /// <summary>Validação do time, da matriz e do membro (CA9, cenário C8).</summary>
    public class TeamsValidationTests
    {
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
        public void Matrix_NullDuplicateUnknownTeam_RepeatedPair_EmptyList()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamDefinition outsider = factory.CreateTeam("Outsider");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, null, blue, red);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            factory.SetCell(matrix, outsider, red, TeamRelation.Hostile);
            factory.SetCell(matrix, red, blue, TeamRelation.Allied);
            TeamRelationMatrix empty = factory.CreateMatrix(TeamRelation.Neutral);

            IReadOnlyList<ValidationMessage> messages = Validate(matrix);
            ValidationMessage emptyMessage = Validate(empty).Single();

            Assert.That(messages.Select(m => m.PropertyPath), Is.EqualTo(new[]
            {
                "teams.Array.data[1]",
                "teams.Array.data[3]",
                "entries.Array.data[1].from",
                "entries.Array.data[2]",
            }));
            Assert.That(messages.All(m => m.Severity == ValidationSeverity.Error), Is.True);
            Assert.That(messages[0].Message, Is.EqualTo("Time vazio em teams (posição 1). Escolha um time ou remova a linha."));
            Assert.That(messages[1].Message, Is.EqualTo("Time 'Red' repetido em teams. Remova a repetição."));
            Assert.That(messages[2].Message,
                Is.EqualTo("A célula 1 usa o time 'Outsider', que não está em teams. Ponha o time na lista ou remova a célula."));
            Assert.That(messages[3].Message,
                Is.EqualTo("O par 'Red' → 'Blue' já tem célula. Vale a última; remova a repetição."));
            Assert.That(emptyMessage.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(emptyMessage.PropertyPath, Is.EqualTo("teams"));
            Assert.That(emptyMessage.Message,
                Is.EqualTo("A matriz não tem times. Toda consulta usa a relação de sem time."));
        }

        [Test]
        public void Matrix_CellWithoutTeam_IsError()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red);
            factory.SetCell(matrix, red, null, TeamRelation.Hostile);

            ValidationMessage message = Validate(matrix).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Error));
            Assert.That(message.PropertyPath, Is.EqualTo("entries.Array.data[0].to"));
            Assert.That(message.Message, Is.EqualTo("A célula 0 está sem time. Escolha um time ou remova a célula."));
        }

        [Test]
        public void Member_NoMatrix_NoTeam_TeamOutsideMatrix()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red);
            TeamMember bare = factory.CreateMember(null, null);
            TeamMember outside = factory.CreateMember(blue, matrix);

            IReadOnlyList<ValidationMessage> bareMessages = Validate(bare);
            ValidationMessage outsideMessage = Validate(outside).Single();

            Assert.That(bareMessages.Select(m => m.PropertyPath), Is.EqualTo(new[] { "team", "matrix" }));
            Assert.That(bareMessages.All(m => m.Severity == ValidationSeverity.Warning), Is.True);
            Assert.That(bareMessages[0].Message, Is.EqualTo("Sem time. O membro usa a relação de sem time."));
            Assert.That(bareMessages[1].Message,
                Is.EqualTo("Sem matriz. As consultas deste membro usam a matriz do outro membro ou Neutro."));
            Assert.That(outsideMessage.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(outsideMessage.PropertyPath, Is.EqualTo("team"));
            Assert.That(outsideMessage.Message,
                Is.EqualTo("O time 'Blue' não está na matriz 'TeamRelationMatrix_Test'. Ponha o time na lista da matriz."));
        }

        [Test]
        public void Definition_EmptyDisplayName_IsWarning()
        {
            TeamDefinition unnamed = factory.CreateTeam("");

            ValidationMessage message = Validate(unnamed).Single();

            Assert.That(message.Severity, Is.EqualTo(ValidationSeverity.Warning));
            Assert.That(message.PropertyPath, Is.EqualTo("displayName"));
            Assert.That(message.Message, Is.EqualTo("Nome de exibição vazio. Dê um nome ao time."));
        }

        [Test]
        public void ValidSetup_HasNoMessages()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            factory.SetCell(matrix, blue, red, TeamRelation.Hostile);
            TeamMember member = factory.CreateMember(red, matrix);

            Assert.That(Validate(red), Is.Empty);
            Assert.That(Validate(matrix), Is.Empty);
            Assert.That(Validate(member), Is.Empty);
        }

        [Test]
        public void ProjectValidator_ReportsTeamsAssetsClickable()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, red);
            TeamMember member = factory.CreateMember(red, null);
            var messages = new List<ValidationMessage>();

            ProjectValidator.Validate(new Object[] { matrix, member.gameObject }, messages);

            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[0].Context, Is.SameAs(matrix));
            Assert.That(messages[0].PropertyPath, Is.EqualTo("teams.Array.data[1]"));
            Assert.That(messages[1].Context, Is.SameAs(member));
            Assert.That(messages[1].PropertyPath, Is.EqualTo("matrix"));
        }

        private static IReadOnlyList<ValidationMessage> Validate(Object target)
        {
            return SpellBookEditor.CollectMessages(new[] { target });
        }
    }
}
