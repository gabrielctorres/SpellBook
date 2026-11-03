using System;

namespace SpellBook.Teams
{
    /// <summary>Relação de um time com outro, lida na <see cref="TeamRelationMatrix"/>.</summary>
    public enum TeamRelation
    {
        Allied,
        Hostile,
        Neutral,
    }

    /// <summary>Conjunto de relações aceitas por um filtro. <see cref="Self"/> casa só com o próprio membro.</summary>
    [Flags]
    public enum TeamRelationMask
    {
        None = 0,
        Self = 1,
        Allied = 2,
        Hostile = 4,
        Neutral = 8,
    }

    public static class TeamRelationMaskExtensions
    {
        /// <summary>
        /// Com <paramref name="isSelf"/>, casa só se a máscara tem <see cref="TeamRelationMask.Self"/>. Sem ele, casa a
        /// relação. A máscara vazia não casa com nada.
        /// </summary>
        public static bool Matches(this TeamRelationMask mask, TeamRelation relation, bool isSelf)
        {
            TeamRelationMask required = isSelf ? TeamRelationMask.Self : ToMask(relation);
            return (mask & required) != 0;
        }

        private static TeamRelationMask ToMask(TeamRelation relation)
        {
            switch (relation)
            {
                case TeamRelation.Allied: return TeamRelationMask.Allied;
                case TeamRelation.Hostile: return TeamRelationMask.Hostile;
                case TeamRelation.Neutral: return TeamRelationMask.Neutral;
                default: return TeamRelationMask.None;
            }
        }
    }
}
