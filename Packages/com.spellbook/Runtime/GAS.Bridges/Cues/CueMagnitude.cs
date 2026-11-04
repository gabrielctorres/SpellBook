using System;
using SpellBook.Core.Validation;
using SpellBook.Cues;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Magnitude que uma ação de cue passa ao roteador: nenhuma (vale a padrão da definição), um valor por nível ou um
    /// SetByCaller do efeito. A normalizada é a magnitude dividida por <see cref="Max"/>, presa entre 0 e 1.
    /// </summary>
    [Serializable]
    public sealed class CueMagnitude
    {
        /// <summary>De onde vem a magnitude.</summary>
        public enum MagnitudeMode
        {
            /// <summary>Sem magnitude: vale a padrão da definição do cue.</summary>
            None,
            /// <summary>Valor no nível do efeito, quando há, ou no da execução. Aceita <c>%nome</c> da tabela da habilidade.</summary>
            Value,
            /// <summary>
            /// Valor por nome passado a quem aplicou o efeito. Fora de efeito, ou sem o nome, não há magnitude. No efeito com
            /// duração, a instância guarda só os nomes que os modificadores dela leem (ADR 0030).
            /// </summary>
            SetByCaller,
        }

        [SerializeField, Tooltip("De onde vem a magnitude. None usa a padrão da definição do cue.")]
        private MagnitudeMode mode;

        [SerializeField, Tooltip("Valor no nível do efeito, quando há, ou no da execução.")]
        private ScalableFloat value = ScalableFloat.Constant(1f);

        [SerializeField, Tooltip("Nome do SetByCaller lido do efeito.")]
        private string callerName;

        [SerializeField, Tooltip("Magnitude que vale 1 na normalizada.")]
        private float max = 1f;

        public MagnitudeMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public ScalableFloat Value
        {
            get => value;
            set => this.value = value;
        }

        public string CallerName
        {
            get => callerName;
            set => callerName = value;
        }

        public float Max
        {
            get => max;
            set => max = value;
        }

        /// <summary>Avalia a magnitude em <paramref name="context"/>. Falso sem magnitude.</summary>
        public bool TryEvaluate(ActionContext context, out float magnitude)
        {
            magnitude = 0f;
            switch (mode)
            {
                case MagnitudeMode.Value:
                    if (value == null) return false;
                    magnitude = context.HasEffect
                        ? value.Evaluate(context.Effect.Level, context.Effect.Table)
                        : value.Evaluate(context.Level, context.Ability != null ? context.Ability.Values : null);
                    return true;
                case MagnitudeMode.SetByCaller:
                    IReadOnlySetByCaller values = context.HasEffect ? context.Effect.CallerValues : null;
                    return values != null && !string.IsNullOrEmpty(callerName) && values.TryGet(callerName, out magnitude);
                default:
                    return false;
            }
        }

        /// <summary>Acusa SetByCaller sem nome e máximo que não é positivo. <paramref name="path"/> é o caminho do campo.</summary>
        public void Validate(ValidationResult result, string path)
        {
            if (mode == MagnitudeMode.None) return;

            if (mode == MagnitudeMode.Value) value?.Validate(result, path + ".value");
            if (mode == MagnitudeMode.SetByCaller && string.IsNullOrEmpty(callerName))
            {
                result.AddError("Magnitude SetByCaller sem nome: escreva o nome do valor.", path + ".callerName");
            }

            if (max <= 0f)
            {
                result.AddError($"Magnitude com máximo {max}: a normalizada divide pelo máximo, que é maior que 0.", path + ".max");
            }
        }

        internal CueParameters Apply(ActionContext context, in CueParameters parameters)
        {
            if (!TryEvaluate(context, out float magnitude)) return parameters;
            return parameters.WithMagnitude(magnitude, max > 0f ? magnitude / max : 1f);
        }
    }

    // O que as ações de cue repartem: os parâmetros, a busca do roteador e a validação
    internal static class CueActionUtility
    {
        /// <summary>
        /// Parâmetros do cue em <paramref name="target"/> ou, sem ele, no ponto do contexto ou na posição do dono, com o
        /// instigador do contexto e a <paramref name="magnitude"/>.
        /// </summary>
        public static CueParameters Build(ActionContext context, AbilitySystemComponent target, CueMagnitude magnitude)
        {
            CueParameters parameters;
            if (target != null) parameters = CueParameters.On(target.transform);
            else if (context.HasPoint) parameters = CueParameters.At(context.Point);
            else parameters = CueParameters.At(context.Owner != null ? context.Owner.transform.position : Vector3.zero);

            if (context.Instigator != null) parameters = parameters.WithInstigator(context.Instigator.transform);
            return magnitude != null ? magnitude.Apply(context, parameters) : parameters;
        }

        /// <summary>O roteador publicado na <paramref name="anchor"/>. Sem ele, avisa uma vez por ação e devolve nulo.</summary>
        public static CueRouter Resolve(CueRouterAnchor anchor, ref bool warned, string action)
        {
            if (anchor != null && anchor.HasValue) return anchor.Value;

            if (!warned && Debug.isDebugBuild)
            {
                warned = true;
                string name = anchor != null ? $"a âncora '{anchor.name}' está vazia" : "sem âncora";
                Debug.LogWarning($"[GAS] {action}: nenhum roteador de cues ({name}); o cue não tocou.");
            }

            return null;
        }

        public static void Validate(ValidationResult result, CueRouterAnchor anchor, GameplayTag tag, CueMagnitude magnitude,
            string action)
        {
            if (!tag.IsValid) result.AddError($"{action} sem tag: escolha a tag do cue.", "tag");
            if (anchor == null) result.AddWarning($"{action} sem âncora: o cue não acha o roteador.", "router");
            magnitude?.Validate(result, "magnitude");
        }
    }
}
