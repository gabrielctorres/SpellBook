using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Estilo de um número flutuante (R13): o papel que o tema traduz em cor, a duração, o movimento e o texto fixo
    /// opcional (por exemplo "Imune"). A view aplica a subida e o deslocamento; o <see cref="FloatingTextBinder"/> usa a
    /// duração para devolver o número ao spawner.
    /// </summary>
    [CreateAssetMenu(menuName = "SpellBook/UI/Floating Text Style", fileName = "FloatingTextStyle")]
    [HelpURL(DocUrls.UiFloatingText)]
    public sealed class FloatingTextStyle : ScriptableObject
    {
        /// <summary>Duração, em segundos, de um número sem estilo.</summary>
        public const float DefaultDuration = 1f;

        [SerializeField]
        [Tooltip("Papel do texto, que o tema traduz em cor.")]
        private TextRole role = TextRole.Neutral;

        [SerializeField, Min(0f)]
        [Tooltip("Segundos até o número voltar ao spawner, pelo tempo do UIUpdateDriver.")]
        private float duration = DefaultDuration;

        [SerializeField]
        [Tooltip("Quanto o número sobe ao longo da duração, nas unidades do container.")]
        private Vector2 rise = new Vector2(0f, 50f);

        [SerializeField]
        [Tooltip("Deslocamento aleatório máximo do ponto de nascimento, em cada eixo, nas unidades do container.")]
        private Vector2 randomOffset;

        [SerializeField]
        [Tooltip("Texto fixo opcional, como \"Imune\". Vazio: o texto vem do valor.")]
        private LocalizedText format;

        /// <summary>Papel do texto.</summary>
        public TextRole Role => role;

        /// <summary>Segundos até o número voltar ao spawner; nunca negativo.</summary>
        public float Duration => Mathf.Max(0f, duration);

        /// <summary>Subida ao longo da duração.</summary>
        public Vector2 Rise => rise;

        /// <summary>Deslocamento aleatório máximo do nascimento.</summary>
        public Vector2 RandomOffset => randomOffset;

        /// <summary>Texto fixo opcional; vazio quando o número mostra o valor.</summary>
        public LocalizedText Format => format;
    }
}
