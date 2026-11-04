using System;
using UnityEngine;

namespace SpellBook.Core.Text
{
    /// <summary>
    /// Texto visível guardado em asset: chave opcional e texto padrão (ADR 0038, item 1). O <see cref="ITextProvider"/>
    /// troca o texto pela chave; sem provedor, ou sem a chave na tabela, vale o texto padrão.
    /// </summary>
    [Serializable]
    public struct LocalizedText
    {
        [Tooltip("Chave opcional no formato <módulo>.<área>.<nome>, por exemplo gas.activation.no_charges.")]
        [SerializeField] private string key;

        [Tooltip("Texto padrão, usado sem provedor ou quando a chave não está na tabela.")]
        [SerializeField] private string text;

        /// <summary>Cria o texto com a chave (pode ser nula) e o texto padrão.</summary>
        public LocalizedText(string key, string text)
        {
            this.key = key;
            this.text = text;
        }

        /// <summary>A chave; nula ou vazia quando o campo não usa tabela.</summary>
        public string Key => key;

        /// <summary>O texto padrão; nunca nulo.</summary>
        public string Text => text ?? string.Empty;

        /// <summary>Verdadeiro sem chave e sem texto.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(key) && string.IsNullOrEmpty(text);

        /// <summary>O texto pelo provedor; provedor nulo devolve <see cref="Text"/>. Não aloca.</summary>
        public string Resolve(ITextProvider provider)
        {
            return provider == null ? Text : provider.GetText(key, Text);
        }
    }
}
