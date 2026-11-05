using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Tags
{
    [HelpURL(DocUrls.Tags + "#gameplaytagdefinition")]
    [CreateAssetMenu(fileName = "NewTag", menuName = "SpellBook/Tags/Tag Definition")]
    public class GameplayTagDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTagDefinition parent;

        /// <summary>Tag pai; <c>null</c> na raiz da hierarquia.</summary>
        public GameplayTagDefinition Parent => parent;

        public bool Matches(GameplayTagDefinition other)
        {
            if (this == other) return true;
            return parent != null && parent.Matches(other);
        }
    }
}

