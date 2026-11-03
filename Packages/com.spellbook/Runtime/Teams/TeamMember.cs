using System;
using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>Componente que põe o objeto num time. O time pode mudar em runtime por <see cref="SetTeam"/>.</summary>
    [HelpURL(DocUrls.Teams + "#teammember")]
    public sealed class TeamMember : MonoBehaviour, ITeamMember, IValidatable
    {
        [SerializeField] private TeamDefinition team;
        [SerializeField] private TeamRelationMatrix matrix;

        /// <summary>
        /// Disparado quando o time muda, com o membro, o time anterior e o novo. O novo time já está gravado quando os
        /// ouvintes rodam. Um ouvinte que troca o time de novo dispara outro evento.
        /// </summary>
        public event Action<TeamMember, TeamDefinition, TeamDefinition> OnTeamChanged;

        /// <inheritdoc />
        public TeamDefinition Team => team;

        /// <inheritdoc />
        public TeamRelationMatrix Matrix => matrix;

        /// <summary>Troca o time. Não faz nada, nem dispara o evento, se o time for o mesmo.</summary>
        public void SetTeam(TeamDefinition newTeam)
        {
            if (newTeam == team)
            {
                return;
            }

            TeamDefinition previous = team;
            team = newTeam;
            OnTeamChanged?.Invoke(this, previous, newTeam);
        }

        /// <summary>Avisa quando falta o time ou a matriz, ou quando o time não está na matriz.</summary>
        public void Validate(ValidationResult result)
        {
            if (team == null)
            {
                result.AddWarning("Sem time. O membro usa a relação de sem time.", nameof(team));
            }

            if (matrix == null)
            {
                result.AddWarning("Sem matriz. As consultas deste membro usam a matriz do outro membro ou Neutro.",
                    nameof(matrix));
            }
            else if (team != null && !matrix.Contains(team))
            {
                result.AddWarning($"O time '{team.name}' não está na matriz '{matrix.name}'. Ponha o time na lista da matriz.",
                    nameof(team));
            }
        }
    }
}
