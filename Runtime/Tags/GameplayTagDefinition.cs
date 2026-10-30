using UnityEngine;

namespace SpellBook.GAS.Tags
{
    [CreateAssetMenu(fileName = "NewTag", menuName = "GAS/Tag Definition")]
    public class GameplayTagDefinition : ScriptableObject
    {
        [SerializeField] private GameplayTagDefinition parent;

        public bool Matches(GameplayTagDefinition other)
        {
            if (this == other) return true;
            return parent != null && parent.Matches(other);
        }
    }
}

