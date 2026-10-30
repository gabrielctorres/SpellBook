using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>
    /// Invoke com delay: espera <c>delay</c> segundos na fonte de tempo e roda a lista de reações, na ordem, com o
    /// payload do disparo. Sem fonte, usa o tempo escalado. Se o <see cref="EventListener"/> for desabilitado na
    /// espera, a lista não roda. Cada disparo pendente guarda uma entrada própria (aloca).
    /// </summary>
    [Serializable, TypeCategory("Fluxo", Summary = "Roda uma lista de reações depois de um atraso.")]
    public sealed class DelayedReaction : Reaction
    {
        [SerializeField, Min(0f), Tooltip("Atraso em segundos, contado na fonte de tempo.")]
        private float delay = 1f;

        [SerializeField, Tooltip("Fonte de tempo do atraso. Vazio usa o tempo escalado (Time.timeScale).")]
        private TimeSourceAsset timeSource;

        [SerializeReference, TypePicker, Tooltip("Reações executadas na ordem da lista depois do atraso.")]
        private List<Reaction> reactions = new List<Reaction>();

        public override void Execute(EventListener listener)
        {
            listener.Schedule(Source, delay, () =>
            {
                for (int i = 0; i < reactions.Count; i++)
                {
                    reactions[i]?.Execute(listener);
                }
            });
        }

        public override void Execute<T>(EventListener listener, in T payload)
        {
            T copy = payload;
            listener.Schedule(Source, delay, () =>
            {
                for (int i = 0; i < reactions.Count; i++)
                {
                    reactions[i]?.Execute(listener, in copy);
                }
            });
        }

        public override void Validate(ValidationResult result)
        {
            if (!HasReactions(result)) return;
            for (int i = 0; i < reactions.Count; i++)
            {
                reactions[i]?.Validate(result);
            }
        }

        public override void Validate(ValidationResult result, Type payloadType)
        {
            if (!HasReactions(result)) return;
            for (int i = 0; i < reactions.Count; i++)
            {
                reactions[i]?.Validate(result, payloadType);
            }
        }

        private ITimeSource Source => timeSource != null ? timeSource : TimeSourceAsset.Scaled;

        private bool HasReactions(ValidationResult result)
        {
            if (reactions != null && reactions.Count > 0) return true;
            result.AddError("Invoke com delay sem reações: adicione ao menos uma em Reactions.");
            return false;
        }
    }
}
