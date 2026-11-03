using System;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Values;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable, TypeCategory("Fluxo", Summary = "Espera alguns segundos no tempo do sistema de habilidades do ator.")]
    public class WaitAction : GameplayAction, ISerializationCallbackReceiver
    {
        [SerializeField] private ScalableFloat duration = ScalableFloat.Constant(0f);

        // Só leitura do formato 2.x ("Duration: 1.5"); convertido em OnAfterDeserialize, como no GameplayEffect.
        [FormerlySerializedAs("Duration")]
        [SerializeField, HideInInspector] private float legacyDuration = float.NaN;

        /// <summary>
        /// Segundos do tempo do ASC por nível, contados do fim do passo anterior. Zero ou menos não espera. Numa lista de
        /// efeito, sem habilidade, o modo <c>Table</c> vale o <c>Fallback</c>.
        /// </summary>
        public ScalableFloat Duration
        {
            get => duration;
            set => duration = value;
        }

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            if (frame.Phase != 0) return ActionStatus.Done;

            float seconds = duration.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null);
            if (seconds <= 0f) return ActionStatus.Done;

            frame.Phase = 1;
            return ActionStatus.Wait(seconds);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (float.IsNaN(legacyDuration)) return;
            duration = ScalableFloat.Constant(legacyDuration);
            legacyDuration = float.NaN;
        }
    }
}
