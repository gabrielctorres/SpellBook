using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>Um time. O próprio asset é o identificador: a matriz e os membros o referenciam.</summary>
    [HelpURL(DocUrls.Teams + "#teamdefinition")]
    [CreateAssetMenu(fileName = "NewTeam", menuName = "SpellBook/Teams/Team")]
    public sealed class TeamDefinition : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Color color = Color.white;

        /// <summary>Nome mostrado ao jogador e no Inspector.</summary>
        public string DisplayName => displayName;

        /// <summary>Cor do time no Inspector e nos debuggers.</summary>
        public Color Color => color;
    }
}
