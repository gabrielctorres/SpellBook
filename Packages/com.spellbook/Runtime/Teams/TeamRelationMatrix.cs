using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>
    /// Relação entre pares ordenados de times. A matriz é esparsa: o par sem célula usa um dos padrões. Não muda em
    /// runtime.
    /// </summary>
    [HelpURL(DocUrls.Teams + "#teamrelationmatrix")]
    [CreateAssetMenu(fileName = "New Team Relation Matrix", menuName = "SpellBook/Teams/Team Relation Matrix")]
    public sealed class TeamRelationMatrix : ScriptableObject, IValidatable
    {
        [SerializeField] private List<TeamDefinition> teams = new List<TeamDefinition>();
        [SerializeField] private List<TeamRelationEntry> entries = new List<TeamRelationEntry>();
        [SerializeField] private TeamRelation defaultRelation = TeamRelation.Neutral;
        [SerializeField] private TeamRelation sameTeamRelation = TeamRelation.Allied;
        [SerializeField] private TeamRelation noTeamRelation = TeamRelation.Neutral;

        [NonSerialized] private Dictionary<TeamDefinition, int> index;
        [NonSerialized] private TeamRelation[] cells;

        /// <summary>
        /// Relação de <paramref name="from"/> para <paramref name="to"/>. Sem célula: a do time consigo mesmo, para o
        /// mesmo time, ou a padrão. Time nulo ou fora da lista: a relação de "sem time".
        /// </summary>
        public TeamRelation GetRelation(TeamDefinition from, TeamDefinition to)
        {
            if (from == null || to == null)
            {
                return noTeamRelation;
            }

            if (index == null)
            {
                BuildIndex();
            }

            if (!index.TryGetValue(from, out int row) || !index.TryGetValue(to, out int column))
            {
                return noTeamRelation;
            }

            return cells[row * index.Count + column];
        }

        /// <summary>
        /// Erro para time vazio ou repetido na lista, célula sem time ou com time fora da lista e par repetido. Aviso
        /// para a lista vazia.
        /// </summary>
        public void Validate(ValidationResult result)
        {
            if (teams.Count == 0)
            {
                result.AddWarning("A matriz não tem times. Toda consulta usa a relação de sem time.", nameof(teams));
            }

            var seen = new HashSet<TeamDefinition>();
            for (int i = 0; i < teams.Count; i++)
            {
                string path = $"{nameof(teams)}.Array.data[{i}]";
                if (teams[i] == null)
                {
                    result.AddError($"Time vazio em teams (posição {i}). Escolha um time ou remova a linha.", path);
                }
                else if (!seen.Add(teams[i]))
                {
                    result.AddError($"Time '{teams[i].name}' repetido em teams. Remova a repetição.", path);
                }
            }

            var pairs = new HashSet<(TeamDefinition, TeamDefinition)>();
            for (int i = 0; i < entries.Count; i++)
            {
                TeamRelationEntry entry = entries[i];
                string path = $"{nameof(entries)}.Array.data[{i}]";
                bool fromValid = ValidateCellTeam(result, entry.From, i, path + ".from");
                bool toValid = ValidateCellTeam(result, entry.To, i, path + ".to");
                if (fromValid && toValid && !pairs.Add((entry.From, entry.To)))
                {
                    result.AddError($"O par '{entry.From.name}' → '{entry.To.name}' já tem célula. Vale a última; " +
                        "remova a repetição.", path);
                }
            }
        }

        /// <summary>Se <paramref name="team"/> está na lista de times da matriz.</summary>
        internal bool Contains(TeamDefinition team)
        {
            return team != null && teams.Contains(team);
        }

        private bool ValidateCellTeam(ValidationResult result, TeamDefinition team, int index, string path)
        {
            if (team == null)
            {
                result.AddError($"A célula {index} está sem time. Escolha um time ou remova a célula.", path);
                return false;
            }

            if (!teams.Contains(team))
            {
                result.AddError($"A célula {index} usa o time '{team.name}', que não está em teams. Ponha o time na " +
                    "lista ou remova a célula.", path);
                return false;
            }

            return true;
        }

        private void OnValidate()
        {
            index = null;
        }

        private void BuildIndex()
        {
            var built = new Dictionary<TeamDefinition, int>();
            foreach (TeamDefinition team in teams)
            {
                if (team != null && !built.ContainsKey(team))
                {
                    built.Add(team, built.Count);
                }
            }

            int count = built.Count;
            var relations = new TeamRelation[count * count];
            for (int row = 0; row < count; row++)
            {
                for (int column = 0; column < count; column++)
                {
                    relations[row * count + column] = row == column ? sameTeamRelation : defaultRelation;
                }
            }

            // Par repetido: a última entrada vence.
            foreach (TeamRelationEntry entry in entries)
            {
                if (entry.From != null && entry.To != null
                    && built.TryGetValue(entry.From, out int row) && built.TryGetValue(entry.To, out int column))
                {
                    relations[row * count + column] = entry.Relation;
                }
            }

            cells = relations;
            index = built;
        }
    }

    [Serializable]
    internal struct TeamRelationEntry
    {
        [SerializeField] private TeamDefinition from;
        [SerializeField] private TeamDefinition to;
        [SerializeField] private TeamRelation relation;

        internal TeamDefinition From => from;
        internal TeamDefinition To => to;
        internal TeamRelation Relation => relation;
    }
}
