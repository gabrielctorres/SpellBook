using SpellBook.Core;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Teams
{
    /// <summary>Um time. O próprio asset é o identificador: a matriz e os membros o referenciam.</summary>
    [HelpURL(DocUrls.Teams + "#teamdefinition")]
    [CreateAssetMenu(fileName = "NewTeam", menuName = "SpellBook/Teams/Team")]
    public sealed class TeamDefinition : ScriptableObject, IValidatable
    {
        [SerializeField] private string displayName;
        [SerializeField] private Color color = Color.white;

        /// <summary>Nome mostrado ao jogador e no Inspector.</summary>
        public string DisplayName => displayName;

        /// <summary>Cor do time no Inspector e nos debuggers.</summary>
        public Color Color => color;

        /// <summary>Avisa quando o nome de exibição está vazio.</summary>
        public void Validate(ValidationResult result)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                result.AddWarning("Nome de exibição vazio. Dê um nome ao time.", nameof(displayName));
            }
        }
    }
}
