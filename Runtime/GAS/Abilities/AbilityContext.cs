using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Abilities
{
    public class AbilityContext
    {
        public AbilitySystemComponent Source;
        public Vector3 TargetPosition;
        public List<AbilitySystemComponent> Targets = new List<AbilitySystemComponent>();
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();

        public GameplayAbility Ability { get; internal set; }
        /// <summary>Spec da ativação. Nulo em contextos derivados (ex: impacto de projétil).</summary>
        public AbilitySpec Spec { get; internal set; }
        public int Level { get; internal set; } = 1;
        public bool IsCancelled { get; internal set; }

        // Ações em execução no momento; recebem OnCancel se a habilidade for cancelada
        internal readonly List<AbilityAction> RunningActions = new List<AbilityAction>();

        public AbilityContext(AbilitySystemComponent source, Vector3 targetPosition)
        {
            Source = source;
            TargetPosition = targetPosition;
        }

        /// <summary>
        /// Cria um contexto novo (ex: ponto de impacto) herdando habilidade e nível, mas sem vínculo de cancelamento.
        /// </summary>
        public AbilityContext CreateDerived(Vector3 targetPosition)
        {
            return new AbilityContext(Source, targetPosition) { Ability = Ability, Level = Level };
        }

        /// <summary>
        /// Remove alvos destruídos durante a execução e retorna a lista atualizada.
        /// </summary>
        public List<AbilitySystemComponent> GetValidTargets()
        {
            Targets.RemoveAll(t => t == null);
            return Targets;
        }

        /// <summary>
        /// Inicia uma corrotina paralela no ASC. Ela é interrompida junto se a habilidade for cancelada.
        /// </summary>
        public Coroutine StartChildRoutine(IEnumerator routine)
        {
            if (Source == null || !Source.isActiveAndEnabled) return null;
            var coroutine = Source.StartCoroutine(routine);
            if (Spec != null && Spec.ActiveContext == this) Spec.ChildRoutines.Add(coroutine);
            return coroutine;
        }
    }
}
