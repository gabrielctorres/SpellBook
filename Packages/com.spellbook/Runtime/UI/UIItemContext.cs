using System;
using SpellBook.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI
{
    /// <summary>
    /// Contexto de um item de lista (ADR 0039, item 6), para o tooltip e outros consumidores do item: o dono, escrito
    /// pelo binder de lista ao criar o item, e o assunto, escrito pela view no <c>Render</c>. Avisa
    /// <see cref="Changed"/> só quando um dos dois muda.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(DocUrls.Ui + "#uiitemcontext")]
    public sealed class UIItemContext : MonoBehaviour
    {
        /// <summary>Disparado quando <see cref="Owner"/> ou <see cref="Subject"/> muda.</summary>
        public event Action Changed;

        /// <summary>O dono que o binder de lista lê, ou <c>null</c> com o item fora da lista.</summary>
        public Object Owner { get; private set; }

        /// <summary>O que o item mostra (por exemplo, a habilidade do slot), ou <c>null</c>.</summary>
        public ScriptableObject Subject { get; private set; }

        /// <summary>Define o assunto; a view chama no <c>Render</c>.</summary>
        public void SetSubject(ScriptableObject subject)
        {
            if (ReferenceEquals(Subject, subject))
            {
                return;
            }

            Subject = subject;
            Changed?.Invoke();
        }

        // Escrito só pelo binder de lista
        internal void SetOwner(Object owner)
        {
            if (ReferenceEquals(Owner, owner))
            {
                return;
            }

            Owner = owner;
            Changed?.Invoke();
        }
    }
}
