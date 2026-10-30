using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Ouve canais pelo Inspector, sem código. Cada resposta liga um canal a reações e a um escape.
    /// Inscreve no <c>OnEnable</c> e desinscreve no <c>OnDisable</c>: desabilitado, não recebe disparos.
    /// </summary>
    [HelpURL(DocUrls.Events + "#eventlistener")]
    public sealed class EventListener : MonoBehaviour
    {
        [SerializeReference, TypePicker, Tooltip("Uma resposta por canal ouvido.")]
        private List<EventResponse> responses = new List<EventResponse>();

        private void OnEnable()
        {
            for (int i = 0; i < responses.Count; i++)
            {
                responses[i]?.Subscribe(this);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < responses.Count; i++)
            {
                responses[i]?.Unsubscribe();
            }
        }
    }
}
