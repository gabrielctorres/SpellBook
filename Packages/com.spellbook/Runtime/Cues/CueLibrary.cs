using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Validation;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>Lista de <see cref="CueDefinition"/> que um <c>CueRouter</c> usa para achar o cue de cada tag.</summary>
    [HelpURL(DocUrls.Cues + "#cuelibrary")]
    [CreateAssetMenu(menuName = "SpellBook/Cues/Cue Library", fileName = "New Cue Library")]
    public class CueLibrary : ScriptableObject, IValidatable
    {
        [SerializeField, Tooltip("Uma definição por tag.")]
        private List<CueDefinition> cues = new List<CueDefinition>();

        /// <summary>As definições da biblioteca.</summary>
        public IReadOnlyList<CueDefinition> Cues => cues;

        public void Validate(ValidationResult result)
        {
            var seen = new HashSet<GameplayTagDefinition>();
            for (int i = 0; i < cues.Count; i++)
            {
                string path = $"{nameof(cues)}.Array.data[{i}]";
                if (cues[i] == null)
                {
                    result.AddError($"Cue vazio em cues (posição {i}). Escolha uma definição ou remova a linha.", path);
                    continue;
                }

                // A definição sem tag acusa a si mesma; aqui só conta a repetição
                GameplayTagDefinition tag = cues[i].Tag.GetDefinition();
                if (tag != null && !seen.Add(tag))
                {
                    result.AddError($"Tag '{tag.name}' repetida em cues. Vale a primeira; remova a repetição.", path);
                }
            }
        }
    }

    /// <summary>
    /// Mapa da definição de tag para o cue, montado das bibliotecas do roteador. A busca sobe pelo pai da tag e devolve
    /// a definição mais específica que casa.
    /// </summary>
    internal sealed class CueIndex
    {
        private readonly Dictionary<GameplayTagDefinition, CueDefinition> _byTag =
            new Dictionary<GameplayTagDefinition, CueDefinition>();

        /// <summary>Monta o mapa de novo. Bibliotecas e definições nulas ou sem tag são puladas; na tag repetida, vale a primeira.</summary>
        public void Rebuild(IReadOnlyList<CueLibrary> libraries)
        {
            _byTag.Clear();
            if (libraries == null)
            {
                return;
            }

            for (int i = 0; i < libraries.Count; i++)
            {
                CueLibrary library = libraries[i];
                if (library == null)
                {
                    continue;
                }

                IReadOnlyList<CueDefinition> cues = library.Cues;
                for (int j = 0; j < cues.Count; j++)
                {
                    CueDefinition cue = cues[j];
                    GameplayTagDefinition key = cue != null ? cue.Tag.GetDefinition() : null;
                    if (key != null && !_byTag.ContainsKey(key))
                    {
                        _byTag.Add(key, cue);
                    }
                }
            }
        }

        /// <summary>Acha o cue da tag ou do ancestral mais próximo que tem um. Não aloca.</summary>
        public bool TryFind(GameplayTag tag, out CueDefinition cue)
        {
            for (GameplayTagDefinition key = tag.GetDefinition(); key != null; key = key.Parent)
            {
                if (_byTag.TryGetValue(key, out cue))
                {
                    return true;
                }
            }

            cue = null;
            return false;
        }
    }
}
