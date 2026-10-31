using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Ouve canais pelo Inspector, sem código. Cada resposta liga um canal a reações e a um escape.
    /// Inscreve no <c>OnEnable</c> e desinscreve no <c>OnDisable</c>: desabilitado, não recebe disparos e descarta
    /// as esperas pendentes do Invoke com delay.
    /// </summary>
    [HelpURL(DocUrls.Events + "#eventlistener")]
    public sealed class EventListener : MonoBehaviour, IValidatable
    {
        [SerializeReference, TypePicker, Tooltip("Uma resposta por canal ouvido.")]
        private List<EventResponse> responses = new List<EventResponse>();

        private readonly List<Wait> _waits = new List<Wait>();

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

            _waits.Clear();
        }

        private void Update()
        {
            // Remove antes de rodar: uma ação que agenda outra espera ou desabilita o ouvinte não pula nem repete
            // nenhuma espera.
            for (int i = 0; i < _waits.Count;)
            {
                Wait wait = _waits[i];
                if (wait.Source.ElapsedTime < wait.Due)
                {
                    i++;
                    continue;
                }

                _waits.RemoveAt(i);
                wait.Action();
            }
        }

        /// <summary>Valida cada resposta da lista (R35).</summary>
        public void Validate(ValidationResult result)
        {
            for (int i = 0; i < responses.Count; i++)
            {
                responses[i]?.Validate(result);
            }
        }

        /// <summary>
        /// Agenda <paramref name="action"/> para quando <paramref name="source"/> avançar <paramref name="delay"/>
        /// segundos a partir de agora. Desabilitado, não agenda; o <c>OnDisable</c> descarta as esperas.
        /// </summary>
        internal void Schedule(ITimeSource source, float delay, Action action)
        {
            if (!isActiveAndEnabled) return;
            _waits.Add(new Wait(source, source.ElapsedTime + delay, action));
        }

        private readonly struct Wait
        {
            public readonly ITimeSource Source;
            public readonly double Due;
            public readonly Action Action;

            public Wait(ITimeSource source, double due, Action action)
            {
                Source = source;
                Due = due;
                Action = action;
            }
        }
    }
}
