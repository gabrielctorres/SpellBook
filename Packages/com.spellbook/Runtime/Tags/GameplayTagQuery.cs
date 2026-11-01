using System;
using System.Collections.Generic;
using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Tags
{
    /// <summary>
    /// Consulta sobre um conjunto de tags: casa se o conjunto tem <see cref="All">todas</see> as tags,
    /// <see cref="Any">alguma</see> delas (se a lista não é vazia) e <see cref="None">nenhuma</see> das
    /// proibidas. Usa a regra de <see cref="GameplayTag.Matches"/>: uma tag filha satisfaz o pai.
    /// Lista vazia não restringe. Tag inválida nunca casa. As consultas não alocam.
    /// </summary>
    [Serializable]
    [HelpURL(DocUrls.Tags + "#gameplaytagquery")]
    public sealed class GameplayTagQuery
    {
        [SerializeField] private GameplayTagContainer all = new GameplayTagContainer();
        [SerializeField] private GameplayTagContainer any = new GameplayTagContainer();
        [SerializeField] private GameplayTagContainer none = new GameplayTagContainer();

        /// <summary>Tags que o conjunto precisa ter todas.</summary>
        public GameplayTagContainer All => all;

        /// <summary>Tags das quais o conjunto precisa ter alguma; vazia não restringe.</summary>
        public GameplayTagContainer Any => any;

        /// <summary>Tags que o conjunto não pode ter.</summary>
        public GameplayTagContainer None => none;

        /// <summary>As três listas estão vazias; a consulta casa com qualquer conjunto.</summary>
        public bool IsEmpty => all.IsEmpty && any.IsEmpty && none.IsEmpty;

        /// <summary>O conjunto, lista ou array satisfaz a consulta. <c>null</c> conta como vazio.</summary>
        public bool Matches(IReadOnlyList<GameplayTag> tags)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (!AnyMatches(tags, all[i])) return false;
            }

            if (!any.IsEmpty && !AnyMatchesSome(tags, any)) return false;
            return !AnyMatchesSome(tags, none);
        }

        /// <summary>Uma única tag satisfaz a consulta; igual a <c>Matches</c> de um conjunto só com ela.</summary>
        public bool Matches(GameplayTag tag)
        {
            for (int i = 0; i < all.Count; i++)
            {
                if (!tag.Matches(all[i])) return false;
            }

            if (!any.IsEmpty && !MatchesSome(tag, any)) return false;
            return !MatchesSome(tag, none);
        }

        private static bool AnyMatchesSome(IReadOnlyList<GameplayTag> tags, GameplayTagContainer conditions)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (AnyMatches(tags, conditions[i])) return true;
            }

            return false;
        }

        private static bool AnyMatches(IReadOnlyList<GameplayTag> tags, GameplayTag condition)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].Matches(condition)) return true;
            }

            return false;
        }

        private static bool MatchesSome(GameplayTag tag, GameplayTagContainer conditions)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (tag.Matches(conditions[i])) return true;
            }

            return false;
        }
    }
}
