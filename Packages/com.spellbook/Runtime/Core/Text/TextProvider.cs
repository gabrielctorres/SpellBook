using System;
using UnityEngine;

namespace SpellBook.Core.Text
{
    /// <summary>
    /// Devolve o texto de uma chave na cultura do provedor (ADR 0038, item 1). Quem exibe texto usa
    /// <see cref="Culture"/> para os números; não há provedor global (item 5).
    /// </summary>
    public interface ITextProvider
    {
        /// <summary>A cultura dos textos e dos números; nunca nula (invariante quando não configurada).</summary>
        IFormatProvider Culture { get; }

        /// <summary>O texto da chave; chave nula, vazia ou ausente devolve <paramref name="fallback"/>.</summary>
        string GetText(string key, string fallback);

        /// <summary>Disparado quando a cultura ou a tabela muda.</summary>
        event Action Changed;
    }

    /// <summary>Base dos provedores em asset, para escolher o provedor por campo no Inspector (ADR 0038, item 1).</summary>
    public abstract class TextProviderAsset : ScriptableObject, ITextProvider
    {
        /// <inheritdoc />
        public abstract IFormatProvider Culture { get; }

        /// <inheritdoc />
        public event Action Changed;

        /// <inheritdoc />
        public abstract string GetText(string key, string fallback);

        /// <summary>Avisa os assinantes de <see cref="Changed"/>; chame depois de trocar a cultura ou a tabela.</summary>
        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Convenção das chaves: <c>&lt;módulo&gt;.&lt;área&gt;.&lt;nome&gt;</c> (ADR 0038, item 3).</summary>
    public static class TextKey
    {
        /// <summary>
        /// Verdadeiro se a chave tem dois ou mais segmentos de <c>[a-z0-9_]</c> separados por ponto, como
        /// <c>gas.activation.no_charges</c>. Não aloca.
        /// </summary>
        public static bool IsValid(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            int segments = 1;
            int segmentLength = 0;
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (c == '.')
                {
                    if (segmentLength == 0) return false;
                    segments++;
                    segmentLength = 0;
                }
                else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                {
                    segmentLength++;
                }
                else
                {
                    return false;
                }
            }

            return segmentLength > 0 && segments >= 2;
        }
    }
}
