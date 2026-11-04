using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Modelo da barra (R7): valor atual, máximo, fração preenchida e fração do rastro atrasado. Sem camada de escudo:
    /// escudo é uma segunda barra com outro atributo (P1). O <c>default</c> é o estado vazio.
    /// </summary>
    public readonly struct BarModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo; a fração é <c>current / max</c> entre 0 e 1 (0 com <c>max ≤ 0</c>) e o rastro é igual a ela.</summary>
        public BarModel(float current, float max, TextSpan text, TextRole role)
        {
            Current = current;
            Max = max;
            Fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            TrailFraction = Fraction;
            Text = text;
            Role = role;
            _hasValue = true;
        }

        private BarModel(in BarModel source, float trailFraction)
        {
            this = source;
            TrailFraction = Mathf.Clamp01(trailFraction);
        }

        /// <summary>Valor atual.</summary>
        public float Current { get; }

        /// <summary>Valor máximo.</summary>
        public float Max { get; }

        /// <summary>Fração preenchida, de 0 a 1.</summary>
        public float Fraction { get; }

        /// <summary>
        /// Fração do rastro, de 0 a 1; igual à <see cref="Fraction"/> por padrão, e <see cref="WithTrail"/> a muda. O
        /// <see cref="BarBinder"/> a segura acima da fração depois de uma queda.
        /// </summary>
        public float TrailFraction { get; }

        /// <summary>Texto da barra, escrito no buffer da ligação; vazio sem texto.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>): sem dono ou sem fonte.</summary>
        public bool IsEmpty => !_hasValue;

        /// <summary>Cópia com o rastro em <paramref name="trailFraction"/>, limitado entre 0 e 1.</summary>
        public BarModel WithTrail(float trailFraction) => new BarModel(this, trailFraction);
    }

    /// <summary>Modelo do texto (R7): o texto escrito no buffer da ligação e o papel. O <c>default</c> é o estado vazio.</summary>
    public readonly struct TextModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo.</summary>
        public TextModel(TextSpan text, TextRole role)
        {
            Text = text;
            Role = role;
            _hasValue = true;
        }

        /// <summary>Texto, escrito no buffer da ligação.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>Modelo do indicador de tag (R7): aceso ou apagado. O <c>default</c> é o estado vazio.</summary>
    public readonly struct TagIndicatorModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo.</summary>
        public TagIndicatorModel(bool active)
        {
            Active = active;
            _hasValue = true;
        }

        /// <summary>Verdadeiro com a tag ou a consulta satisfeita.</summary>
        public bool Active { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>
    /// Pedido de um número flutuante (R14): onde nasce, o texto, o papel e o estilo. O texto é escrito no buffer da
    /// ligação e vale só durante o <see cref="IFloatingTextView.Show"/>. O <c>default</c> é o estado vazio: nada nasce.
    /// </summary>
    public readonly struct FloatingTextRequest
    {
        private readonly bool _hasValue;

        /// <summary>Cria o pedido; <paramref name="style"/> pode ser nulo (duração <see cref="FloatingTextStyle.DefaultDuration"/>).</summary>
        public FloatingTextRequest(Vector3 worldPosition, TextSpan text, TextRole role, FloatingTextStyle style)
        {
            WorldPosition = worldPosition;
            Text = text;
            Role = role;
            Style = style;
            _hasValue = true;
        }

        /// <summary>Ponto do mundo em que o número nasce; a view o converte para o container.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Texto do número, escrito no buffer da ligação.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Estilo do número, ou nulo.</summary>
        public FloatingTextStyle Style { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>
    /// View de um número flutuante (R14), no prefab que o <see cref="FloatingTextBinder"/> cria pelo spawner. Recebe o
    /// pedido ao nascer e o progresso a cada tick; não lê o tempo nem volta ao pool sozinha.
    /// </summary>
    public interface IFloatingTextView
    {
        /// <summary>Mostra o número do <paramref name="request"/>; o texto deve ser copiado aqui.</summary>
        void Show(in FloatingTextRequest request);

        /// <summary>Progresso da duração, de 0 (nasceu) a 1 (vai voltar ao spawner).</summary>
        void SetProgress(float normalized);
    }
}
