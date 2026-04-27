using System;
using System.Data;
using System.Globalization;
using UnityEngine;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// A lightweight mathematical expression evaluator.
    /// Supports: +, -, *, /, ( )
    /// </summary>
    public static class ExpressionParser
    {
        private static DataTable _cachedTable;

        private static DataTable GetTable()
        {
            if (_cachedTable == null)
            {
                _cachedTable = new DataTable();
                _cachedTable.Locale = CultureInfo.InvariantCulture;
            }
            return _cachedTable;
        }

        /// <summary>
        /// Evaluates a mathematical string and returns the result.
        /// </summary>
        public static float Evaluate(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return 0;

            try
            {
                // We use DataTable.Compute as it's a built-in .NET way to handle math strings safely.
                // It handles operator precedence and parentheses automatically.
                var table = GetTable();
                var result = table.Compute(expression, string.Empty);
                return Convert.ToSingle(result);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ExpressionParser] Failed to evaluate expression: {expression}. Error: {e.Message}");
                return 0;
            }
        }
    }
}

