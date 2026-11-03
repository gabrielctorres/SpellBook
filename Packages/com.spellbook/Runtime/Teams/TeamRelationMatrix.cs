using System;
using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>
    /// Relação entre pares ordenados de times. A matriz é esparsa: o par sem célula usa um dos padrões. Não muda em
    /// runtime.
    /// </summary>
    [HelpURL(DocUrls.Teams + "#teamrelationmatrix")]
    [CreateAssetMenu(fileName = "NewTeamRelationMatrix", menuName = "SpellBook/Teams/Relation Matrix")]
    public sealed class TeamRelationMatrix : ScriptableObject
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
