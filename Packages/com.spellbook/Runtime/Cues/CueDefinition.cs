using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>Como o cue vive: toca e acaba sozinho, ou fica ligado até ser removido.</summary>
    public enum CueKind
    {
        /// <summary>Toca uma vez e acaba sozinho.</summary>
        Burst,

        /// <summary>Fica ligado até ser removido pelo handle.</summary>
        Looping,
    }

    /// <summary>O que fazer quando o cue já tem o máximo de cópias ativas.</summary>
    public enum CueLimitPolicy
    {
        /// <summary>Não toca a cópia nova.</summary>
        DropNew,

        /// <summary>Remove a cópia mais antiga e toca a nova.</summary>
        ReplaceOldest,
    }

    /// <summary>Quantos sons da lista tocam a cada vez.</summary>
    public enum SoundSelection
    {
        /// <summary>Um som sorteado da lista.</summary>
        RandomOne,

        /// <summary>Todos os sons da lista.</summary>
        All,
    }

    /// <summary>
    /// O que toca para uma tag de cue: VFX, sons e itens próprios. O <c>CueRouter</c> acha a definição pela tag, pelas
    /// <see cref="CueLibrary"/> que ele usa.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#cuedefinition")]
    [CreateAssetMenu(menuName = "SpellBook/Cues/Cue", fileName = "New Cue")]
    public class CueDefinition : ScriptableObject, IValidatable
    {
        [SerializeField, Tooltip("Tag que toca este cue. Uma tag filha sem definição própria usa esta.")]
        private GameplayTag tag;

        [SerializeField, Tooltip("Burst toca e acaba sozinho; Looping fica ligado até ser removido.")]
        private CueKind kind;

        [SerializeReference, TypePicker, Tooltip("Itens que tocam juntos a cada vez.")]
        private List<CueItem> items = new List<CueItem>();

        [SerializeReference, TypePicker, Tooltip("Sons do cue. A seleção diz se toca um sorteado ou todos.")]
        private List<SoundCueItem> sounds = new List<SoundCueItem>();

        [SerializeField, Tooltip("RandomOne sorteia um som da lista a cada vez; All toca todos.")]
        private SoundSelection soundSelection;

        [SerializeField, Min(0), Tooltip("Máximo de cópias ativas ao mesmo tempo. 0 é sem limite.")]
        private int maxSimultaneous;

        [SerializeField, Tooltip("O que fazer quando o máximo de cópias já está ativo.")]
        private CueLimitPolicy limitPolicy;

        [SerializeField, Range(0f, 1f), Tooltip("Magnitude normalizada usada quando quem toca não informa uma.")]
        private float defaultMagnitude = 1f;

        /// <summary>Tag que toca este cue.</summary>
        public GameplayTag Tag => tag;

        /// <summary>Como o cue vive.</summary>
        public CueKind Kind => kind;

        /// <summary>Itens que tocam juntos a cada vez.</summary>
        public IReadOnlyList<CueItem> Items => items;

        /// <summary>Sons do cue.</summary>
        public IReadOnlyList<SoundCueItem> Sounds => sounds;

        /// <summary>Se toca um som sorteado ou todos.</summary>
        public SoundSelection SoundSelection => soundSelection;

        /// <summary>Máximo de cópias ativas ao mesmo tempo; 0 é sem limite.</summary>
        public int MaxSimultaneous => maxSimultaneous;

        /// <summary>O que fazer quando o máximo de cópias já está ativo.</summary>
        public CueLimitPolicy LimitPolicy => limitPolicy;

        /// <summary>Magnitude normalizada, de 0 a 1, usada quando quem toca não informa uma.</summary>
        public float DefaultMagnitude => defaultMagnitude;

        public void Validate(ValidationResult result)
        {
            if (!tag.IsValid)
            {
                result.AddError("Sem tag. Escolha a tag que toca este cue.", nameof(tag));
            }

            if (items.Count == 0 && sounds.Count == 0)
            {
                result.AddWarning("Sem itens e sem sons. O cue não toca nada.", nameof(items));
            }

            for (int i = 0; i < items.Count; i++)
            {
                string path = $"{nameof(items)}.Array.data[{i}]";
                if (items[i] == null)
                {
                    result.AddError($"Item vazio em items (posição {i}). Escolha um tipo ou remova a linha.", path);
                }
                else
                {
                    items[i].Validate(result, path, kind);
                }
            }

            for (int i = 0; i < sounds.Count; i++)
            {
                string path = $"{nameof(sounds)}.Array.data[{i}]";
                if (sounds[i] == null)
                {
                    result.AddError($"Som vazio em sounds (posição {i}). Escolha um som ou remova a linha.", path);
                }
                else
                {
                    sounds[i].Validate(result, path, kind);
                }
            }

            if (maxSimultaneous < 0)
            {
                result.AddError("Máximo de cópias negativo. Use 0 para sem limite.", nameof(maxSimultaneous));
            }
        }
    }
}
