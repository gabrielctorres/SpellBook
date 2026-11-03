using System;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>Componente que põe o objeto num time. O time pode mudar em runtime por <see cref="SetTeam"/>.</summary>
    [HelpURL(DocUrls.Teams + "#teammember")]
    public sealed class TeamMember : MonoBehaviour, ITeamMember
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
    }
}
