using System;
using System.Globalization;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.GAS.Values
{
    /// <summary>
    /// Número que pode variar com o nível (ADR 0020 e 0030): constante, um valor por nível (array)
    /// ou uma curva avaliada no nível. O nível começa em 1; nível menor que 1 vale como 1.
    /// <see cref="Evaluate"/> não aloca.
    /// </summary>
    [Serializable]
    public sealed class ScalableFloat
    {
        public enum ScalableMode
        {
            Constant,
            Array,
            Curve
        }

        private static readonly char[] Separators = { ' ', ',', ';', '\t', '\n', '\r' };

        [SerializeField] private ScalableMode mode;
        [SerializeField] private float constant;
        [SerializeField] private float[] values = System.Array.Empty<float>();
        [SerializeField] private AnimationCurve curve;

        /// <summary>Como o valor é calculado.</summary>
        public ScalableMode Mode => mode;

        /// <summary>Mesmo valor em qualquer nível.</summary>
        public static ScalableFloat Constant(float value)
        {
            return new ScalableFloat { mode = ScalableMode.Constant, constant = value };
        }

        /// <summary>Um valor por nível; o primeiro é o nível 1. O array é copiado.</summary>
        public static ScalableFloat FromArray(params float[] values)
        {
            float[] copy = values != null ? (float[])values.Clone() : System.Array.Empty<float>();
            return new ScalableFloat { mode = ScalableMode.Array, values = copy };
        }

        /// <summary>Curva avaliada no nível (x = nível inteiro).</summary>
        public static ScalableFloat FromCurve(AnimationCurve curve)
        {
            return new ScalableFloat { mode = ScalableMode.Curve, curve = curve };
        }

        /// <summary>
        /// Valor no nível. Array vazio e curva nula valem 0; acima do tamanho do array, repete o último.
        /// </summary>
        public float Evaluate(int level)
        {
            if (level < 1)
            {
                level = 1;
            }

            switch (mode)
            {
                case ScalableMode.Constant:
                    return constant;
                case ScalableMode.Array:
                    if (values == null || values.Length == 0)
                    {
                        return 0f;
                    }

                    return values[Math.Min(level, values.Length) - 1];
                case ScalableMode.Curve:
                    return curve != null ? curve.Evaluate(level) : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Lê o texto de um array ("10 8 6 4"). Separadores: espaço, vírgula e ponto e vírgula; o ponto é o
        /// separador decimal (cultura invariante). Texto vazio dá um array vazio. Usado pelo Editor.
        /// </summary>
        public static bool TryParse(string text, out float[] values, out string error)
        {
            string[] parts = (text ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            var parsed = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                {
                    values = null;
                    error = $"\"{parts[i]}\" não é um número. Use ponto como separador decimal e espaço, vírgula ou ponto e vírgula entre os valores.";
                    return false;
                }
            }

            values = parsed;
            error = null;
            return true;
        }

        /// <summary>Acusa array vazio e curva nula, com o caminho do campo dono.</summary>
        public void Validate(ValidationResult result, string propertyPath)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (mode == ScalableMode.Array && (values == null || values.Length == 0))
            {
                result.AddError("O array de valores por nível está vazio; ele vale 0 em todo nível.", propertyPath);
            }
            else if (mode == ScalableMode.Curve && curve == null)
            {
                result.AddError("A curva de valores por nível não foi definida; ela vale 0 em todo nível.", propertyPath);
            }
        }
    }
}
