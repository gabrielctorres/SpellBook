using System;
using System.Collections.Generic;
using SpellBook.Core.Validation;
using UnityEngine;

namespace SpellBook.Core.Text
{
    /// <summary>
    /// Textos de uma cultura, por chave (ADR 0038, item 2). O <see cref="TableTextProvider"/> lista as tabelas e escolhe
    /// pela cultura; cultura vazia é a tabela neutra, lida depois das culturas.
    /// </summary>
    [HelpURL(DocUrls.Text + "#texttable")]
    [CreateAssetMenu(menuName = "SpellBook/Text/Text Table", fileName = "New Text Table")]
    public class TextTable : ScriptableObject, IValidatable
    {
        /// <summary>Uma chave e o texto dela.</summary>
        [Serializable]
        public struct Entry
        {
            [Tooltip("Chave no formato <módulo>.<área>.<nome>, por exemplo gas.activation.no_charges.")]
            [SerializeField] private string key;

            [Tooltip("Texto da chave nesta cultura.")]
            [SerializeField] private string text;

            public Entry(string key, string text)
            {
                this.key = key;
                this.text = text;
            }

            /// <summary>A chave.</summary>
            public string Key => key;

            /// <summary>O texto; nunca nulo.</summary>
            public string Text => text ?? string.Empty;
        }

        [SerializeField, Tooltip("Nome da cultura, como pt-BR, pt ou en. Vazio: tabela neutra, usada por qualquer cultura.")]
        private string cultureName = string.Empty;

        [SerializeField, Tooltip("Uma linha por chave.")]
        private List<Entry> entries = new List<Entry>();

        /// <summary>O nome da cultura; vazio na tabela neutra.</summary>
        public string CultureName => cultureName ?? string.Empty;

        /// <summary>As linhas da tabela.</summary>
        public IReadOnlyList<Entry> Entries => entries;

        public void Validate(ValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].Key;
                string path = $"{nameof(entries)}.Array.data[{i}].key";
                if (!TextKey.IsValid(key))
                {
                    result.AddWarning($"Chave '{key}' fora do formato <módulo>.<área>.<nome> (minúsculas, números e _).", path);
                }

                if (!string.IsNullOrEmpty(key) && !seen.Add(key))
                {
                    result.AddError($"Chave '{key}' repetida. Vale a primeira; remova a repetição.", path);
                }
            }
        }
    }
}
