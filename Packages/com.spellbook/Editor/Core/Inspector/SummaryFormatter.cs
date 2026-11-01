using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using SpellBook.Core.Inspector;
using UnityEditor;

namespace SpellBook.Core.Editor.Inspector
{
    /// <summary>
    /// Resumo de uma linha de uma referência gerenciada, pelo modelo <see cref="TypeCategoryAttribute.Line"/> do tipo
    /// (Spec do B-009, seção 2). Lê só o <see cref="SerializedProperty"/>, sem reflexão sobre os campos da instância.
    /// </summary>
    public static class SummaryFormatter
    {
        /// <summary>Texto de uma referência vazia.</summary>
        public const string None = "(nenhum)";

        /// <summary>Texto de um <c>ScalableFloat</c> em modo array ou curva.</summary>
        public const string PerLevel = "por nível";

        private const int MaxDepth = 3;

        /// <summary>
        /// Resumo da referência: o <c>Line</c> do tipo com os campos trocados pelos valores; sem <c>Line</c>, o nome
        /// exibido do tipo; referência nula, <see cref="None"/>. Campo inexistente fica como está no modelo.
        /// </summary>
        public static string Format(SerializedProperty managedReference)
        {
            if (managedReference == null)
            {
                throw new ArgumentNullException(nameof(managedReference));
            }

            if (managedReference.propertyType != SerializedPropertyType.ManagedReference)
            {
                throw new ArgumentException("A propriedade não é uma referência gerenciada.", nameof(managedReference));
            }

            return FormatReference(managedReference, 1);
        }

        private static string FormatReference(SerializedProperty property, int depth)
        {
            object value = property.managedReferenceValue;
            if (value == null)
            {
                return None;
            }

            Type type = value.GetType();
            string displayName = ObjectNames.NicifyVariableName(type.Name);
            string line = type.GetCustomAttribute<TypeCategoryAttribute>(false)?.Line;
            if (depth > MaxDepth || string.IsNullOrEmpty(line))
            {
                return displayName;
            }

            return CollapseSpaces(Expand(line, property, depth));
        }

        private static string Expand(string line, SerializedProperty owner, int depth)
        {
            var builder = new StringBuilder(line.Length);
            int index = 0;
            while (index < line.Length)
            {
                int open = line.IndexOf('{', index);
                int close = open < 0 ? -1 : line.IndexOf('}', open + 1);
                if (close < 0)
                {
                    builder.Append(line, index, line.Length - index);
                    break;
                }

                builder.Append(line, index, open - index);
                string token = line.Substring(open + 1, close - open - 1);
                builder.Append(FormatToken(token, owner, depth) ?? line.Substring(open, close - open + 1));
                index = close + 1;
            }

            return builder.ToString();
        }

        private static string FormatToken(string token, SerializedProperty owner, int depth)
        {
            int question = token.IndexOf('?');
            string name = question < 0 ? token : token.Substring(0, question);
            SerializedProperty field = FindField(owner, name);
            if (field == null)
            {
                return null;
            }

            if (question < 0)
            {
                return FormatValue(field, depth);
            }

            bool on = field.propertyType == SerializedPropertyType.Boolean && field.boolValue;
            return on ? token.Substring(question + 1) : string.Empty;
        }

        private static SerializedProperty FindField(SerializedProperty owner, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return owner.FindPropertyRelative(name)
                ?? owner.FindPropertyRelative(char.ToLowerInvariant(name[0]) + name.Substring(1));
        }

        private static string FormatValue(SerializedProperty property, int depth)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.longValue.ToString(CultureInfo.CurrentCulture);
                case SerializedPropertyType.Float:
                    return FormatNumber(property.doubleValue);
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? "sim" : "não";
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return FormatEnum(property);
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.name : None;
                case SerializedPropertyType.ManagedReference:
                    return FormatReference(property, depth + 1);
            }

            if (property.isArray)
            {
                return property.arraySize.ToString(CultureInfo.CurrentCulture);
            }

            return TryFormatScalableFloat(property, out string scalable)
                ? scalable
                : ObjectNames.NicifyVariableName(property.type);
        }

        private static string FormatEnum(SerializedProperty property)
        {
            int index = property.enumValueIndex;
            string[] names = property.enumDisplayNames;
            return index >= 0 && index < names.Length
                ? names[index]
                : property.intValue.ToString(CultureInfo.CurrentCulture);
        }

        private static bool TryFormatScalableFloat(SerializedProperty property, out string text)
        {
            text = null;
            SerializedProperty mode = property.FindPropertyRelative("mode");
            SerializedProperty constant = property.FindPropertyRelative("constant");
            SerializedProperty tableName = property.FindPropertyRelative("tableName");
            if (mode == null || mode.propertyType != SerializedPropertyType.Enum || constant == null || tableName == null)
            {
                return false;
            }

            int index = mode.enumValueIndex;
            string modeName = index >= 0 && index < mode.enumNames.Length ? mode.enumNames[index] : string.Empty;
            switch (modeName)
            {
                case "Constant":
                    text = FormatNumber(constant.doubleValue);
                    break;
                case "Table":
                    text = "%" + tableName.stringValue;
                    break;
                default:
                    text = PerLevel;
                    break;
            }

            return true;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.##", CultureInfo.CurrentCulture);
        }

        private static string CollapseSpaces(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == ' ' && (builder.Length == 0 || builder[builder.Length - 1] == ' '))
                {
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString().TrimEnd(' ');
        }
    }
}
