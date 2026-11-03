namespace SpellBook.Teams
{
    /// <summary>
    /// Consultas de relação entre membros. Um membro nulo conta como sem time. Vale a matriz de quem pergunta, depois a
    /// do outro membro. Sem nenhuma das duas, a relação é <see cref="TeamRelation.Neutral"/>.
    /// </summary>
    public static class TeamQueries
    {
        /// <summary>Relação de <paramref name="a"/> para <paramref name="b"/>.</summary>
        public static TeamRelation GetRelation(this ITeamMember a, ITeamMember b)
        {
            TeamRelationMatrix matrix = a?.Matrix;
            if (matrix == null)
            {
                matrix = b?.Matrix;
            }

            if (matrix == null)
            {
                return TeamRelation.Neutral;
            }

            return matrix.GetRelation(a?.Team, b?.Team);
        }

        /// <summary>Se a relação de <paramref name="a"/> para <paramref name="b"/> é <see cref="TeamRelation.Allied"/>.</summary>
        public static bool IsAllied(this ITeamMember a, ITeamMember b) => GetRelation(a, b) == TeamRelation.Allied;

        /// <summary>Se a relação de <paramref name="a"/> para <paramref name="b"/> é <see cref="TeamRelation.Hostile"/>.</summary>
        public static bool IsHostile(this ITeamMember a, ITeamMember b) => GetRelation(a, b) == TeamRelation.Hostile;

        /// <summary>Se a relação de <paramref name="a"/> para <paramref name="b"/> é <see cref="TeamRelation.Neutral"/>.</summary>
        public static bool IsNeutral(this ITeamMember a, ITeamMember b) => GetRelation(a, b) == TeamRelation.Neutral;

        /// <summary>
        /// Se <paramref name="mask"/> aceita <paramref name="b"/> visto por <paramref name="a"/>. Quando os dois são o
        /// mesmo membro, só <see cref="TeamRelationMask.Self"/> casa.
        /// </summary>
        public static bool Matches(TeamRelationMask mask, ITeamMember a, ITeamMember b)
        {
            bool isSelf = a != null && ReferenceEquals(a, b);
            return mask.Matches(GetRelation(a, b), isSelf);
        }
    }
}
