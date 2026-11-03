using SpellBook.GAS.Core;
using SpellBook.Teams;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>Acha o membro de time de um ASC, sem cache e sem alocar.</summary>
    public static class AscTeams
    {
        /// <summary>
        /// O primeiro <see cref="ITeamMember"/> no objeto do ASC ou num pai dele. Nulo sem ASC ou sem membro na
        /// hierarquia.
        /// </summary>
        public static ITeamMember GetMember(AbilitySystemComponent asc)
        {
            if (asc == null)
            {
                return null;
            }

            for (Transform current = asc.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out ITeamMember member))
                {
                    return member;
                }
            }

            return null;
        }
    }
}
