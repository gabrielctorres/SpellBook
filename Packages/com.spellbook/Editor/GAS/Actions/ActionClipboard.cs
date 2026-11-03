using System;
using SpellBook.GAS.Actions;
using UnityEditor;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Copiar, colar e duplicar uma ação com as filhas (DS do Inspector de ações, 4.1 e 4.5). O texto copiado é o tipo e o
    /// JSON do <see cref="EditorJsonUtility"/>, a mesma cópia da ADR 0026; colar cria instâncias novas, nunca a mesma referência.
    /// </summary>
    internal static class ActionClipboard
    {
        private const string Header = "SpellBook.GameplayAction";
        private const string PasteUndo = "Colar ação";
        private const string DuplicateUndo = "Duplicar ação";

        /// <summary>Texto da área de transferência do sistema, usado pelo menu e pelos comandos do Editor.</summary>
        internal static string SystemText
        {
            get => EditorGUIUtility.systemCopyBuffer;
            set => EditorGUIUtility.systemCopyBuffer = value;
        }

        /// <summary>Texto copiado da ação do <paramref name="element"/>, ou <c>null</c> se o elemento está vazio.</summary>
        internal static string Copy(SerializedProperty element)
        {
            object value = element.managedReferenceValue;
            if (value == null) return null;
            return Header + "\n" + value.GetType().AssemblyQualifiedName + "\n" + EditorJsonUtility.ToJson(value);
        }

        /// <summary>Se o <paramref name="text"/> veio de <see cref="Copy"/> e o tipo dele ainda existe.</summary>
        internal static bool CanPaste(string text)
        {
            return Parse(text) != null;
        }

        /// <summary>
        /// Insere em <paramref name="list"/>, na posição <paramref name="index"/>, uma ação nova lida do <paramref name="text"/>,
        /// num passo de Undo. Devolve <c>false</c>, sem mudar nada, se o texto não é uma ação copiada.
        /// </summary>
        internal static bool Paste(SerializedProperty list, int index, string text)
        {
            return Insert(list, index, Parse(text), PasteUndo);
        }

        /// <summary>Põe uma cópia nova da ação <paramref name="index"/> logo abaixo dela, num passo de Undo.</summary>
        internal static bool Duplicate(SerializedProperty list, int index)
        {
            if (index < 0 || index >= list.arraySize) return false;
            return Insert(list, index + 1, Parse(Copy(list.GetArrayElementAtIndex(index))), DuplicateUndo);
        }

        private static GameplayAction Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(new[] { '\n' }, 3);
            if (parts.Length < 3 || parts[0] != Header) return null;
            Type type = Type.GetType(parts[1], false);
            if (type == null || type.IsAbstract || !typeof(GameplayAction).IsAssignableFrom(type)) return null;
            var action = (GameplayAction)Activator.CreateInstance(type, true);
            EditorJsonUtility.FromJsonOverwrite(parts[2], action);
            return action;
        }

        private static bool Insert(SerializedProperty list, int index, GameplayAction action, string undoName)
        {
            if (action == null) return false;
            SerializedObject serialized = list.serializedObject;
            serialized.Update();
            SerializedProperty target = serialized.FindProperty(list.propertyPath);
            int at = Math.Max(0, Math.Min(index, target.arraySize));
            target.arraySize++;
            target.GetArrayElementAtIndex(target.arraySize - 1).managedReferenceValue = action;
            target.MoveArrayElement(target.arraySize - 1, at);
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            serialized.ApplyModifiedProperties();
            return true;
        }
    }
}
