using System;
using System.Globalization;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.GAS.Values
{
    /// <summary>
    /// Número que pode variar com o nível (ADR 0020 e 0030): constante, um valor por nível (array), uma curva
    /// avaliada no nível ou um valor nomeado da tabela <see cref="AbilityValues"/> da habilidade (modo <c>Table</c>).
    /// O nível começa em 1; nível menor que 1 vale como 1. <see cref="Evaluate"/> não aloca.
    /// </summary>
    [Serializable]
    public sealed class ScalableFloat
    {
        public enum ScalableMode
        {
            Constant,
            Array,
            Curve,
            Table
        }

        private static readonly char[] Separators = { ' ', ',', ';', '\t', '\n', '\r' };

        [SerializeField] private ScalableMode mode;
        [SerializeField] private float constant;
        [SerializeField] private float[] values = System.Array.Empty<float>();
        [SerializeField] private AnimationCurve curve;
        [SerializeField] private string tableName;
        [SerializeField] private float fallback;

        // Cache do nome na tabela (R7): com a mesma tabela e a mesma versão, a avaliação não compara texto.
        [NonSerialized] private AbilityValues _cachedTable;
        [NonSerialized] private int _cachedVersion;
        [NonSerialized] private int _cachedIndex;
        [NonSerialized] private bool _warnedMissing;

        /// <summary>Como o valor é calculado.</summary>
        public ScalableMode Mode => mode;

        /// <summary>Nome do valor na tabela, no modo <c>Table</c>.</summary>
        public string TableName => tableName;

        /// <summary>Valor no modo <c>Table</c> sem tabela ou sem o nome nela.</summary>
        public float Fallback => fallback;

        /// <summary>Tamanho do array de valores por nível (0 fora do modo <c>Array</c> ou com o array vazio).</summary>
        internal int ArrayLength => mode == ScalableMode.Array && values != null ? values.Length : 0;

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

        /// <summary>Valor <paramref name="name"/> da tabela da habilidade; sem tabela ou sem o nome, vale <paramref name="fallback"/>.</summary>
        public static ScalableFloat FromTable(string name, float fallback = 0f)
        {
            return new ScalableFloat { mode = ScalableMode.Table, tableName = name, fallback = fallback };
        }

        /// <summary>
        /// Valor no nível. Array vazio e curva nula valem 0; acima do tamanho do array, repete o último. No modo
        /// <c>Table</c>, lê a entrada da <paramref name="table"/>: sem tabela vale o <see cref="Fallback"/>, sem aviso;
        /// com tabela mas sem o nome, vale o <see cref="Fallback"/> com um aviso único por instância.
        /// </summary>
        public float Evaluate(int level, AbilityValues table = null)
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
                case ScalableMode.Table:
                    return EvaluateTable(level, table);
                default:
                    return 0f;
            }
        }

        private float EvaluateTable(int level, AbilityValues table)
        {
            if (table == null)
            {
                return fallback;
            }

            if (!ReferenceEquals(table, _cachedTable) || table.Version != _cachedVersion)
            {
                _cachedTable = table;
                _cachedVersion = table.Version;
                _cachedIndex = table.IndexOf(tableName);
            }

            if (_cachedIndex < 0)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning($"[GAS] O valor \"{tableName}\" não existe na tabela da habilidade; usando o fallback {fallback}.");
                }

                return fallback;
            }

            return table.Evaluate(_cachedIndex, level);
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

        /// <summary>
        /// Acusa array vazio, curva nula, modo <c>Table</c> sem nome e, com <paramref name="allowTable"/> falso (dentro da
        /// própria <see cref="AbilityValues"/>), modo <c>Table</c>. O caminho é o do campo dono.
        /// </summary>
        public void Validate(ValidationResult result, string propertyPath, bool allowTable = true)
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
            else if (mode == ScalableMode.Table && !allowTable)
            {
                result.AddError("Uma entrada da tabela não pode ler outra entrada (modo Table).", propertyPath);
            }
            else if (mode == ScalableMode.Table && string.IsNullOrEmpty(tableName))
            {
                result.AddError("O modo Table está sem o nome do valor; ele vale o fallback em todo nível.", propertyPath);
            }
        }
    }
}
