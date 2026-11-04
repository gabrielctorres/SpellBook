using System;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Rótulo da tecla de cada slot (Spec do B-016, P5): o texto "Q" ou "Botão A" que a view mostra. A chave é o asset
    /// da tag do slot visto como <see cref="ScriptableObject"/>. Nenhuma implementação do pacote conhece o sistema de
    /// entrada; o projeto escreve a sua para a troca de tecla e chama <see cref="RaiseChanged"/>.
    /// </summary>
    public abstract class KeyLabelProvider : ScriptableObject
    {
        /// <summary>Disparado quando algum rótulo muda; quem mostra o rótulo o escreve de novo.</summary>
        public event Action Changed;

        /// <summary>
        /// Escreve o rótulo de <paramref name="slotKey"/> no fim de <paramref name="into"/>, com o texto pelo provedor
        /// <paramref name="text"/> (nulo: o texto padrão). Chave nula ou sem rótulo devolve <c>false</c> e não escreve.
        /// Guarde <see cref="TextBuffer.Length"/> antes e pegue o trecho com <see cref="TextBuffer.SpanFrom"/>. Não aloca.
        /// </summary>
        public abstract bool TryWriteLabel(ScriptableObject slotKey, ITextProvider text, TextBuffer into);

        /// <summary>Avisa os assinantes de <see cref="Changed"/>; chame depois de trocar um rótulo.</summary>
        protected void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
