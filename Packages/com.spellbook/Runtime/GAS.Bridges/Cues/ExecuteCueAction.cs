using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.Cues;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using UnityEngine;

namespace SpellBook.GAS.Bridges
{
    /// <summary>
    /// Toca o cue <see cref="CueKind.Burst"/> da tag pelo roteador da âncora, uma vez por alvo resolvido ou, sem alvo, no
    /// ponto da execução (ou na posição do dono). Termina na mesma chamada. Sem roteador, avisa uma vez e não toca.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#executecueaction")]
    [Serializable, TypeCategory("Apresentação", Summary = "Toca um cue de explosão em cada alvo, pelo roteador da âncora.",
        Line = "Cue {tag}")]
    public sealed class ExecuteCueAction : GameplayAction
    {
        [SerializeField, Tooltip("Âncora em que o roteador de cues da cena se publica.")]
        private CueRouterAnchor router;

        [SerializeField, Tooltip("Tag do cue tocado. Sem definição para ela, procura a da tag pai.")]
        private GameplayTag tag;

        [SerializeField, Tooltip("Magnitude passada ao cue.")]
        private CueMagnitude magnitude = new CueMagnitude();

        [NonSerialized] private bool _warnedRouter;

        public CueRouterAnchor Router
        {
            get => router;
            set => router = value;
        }

        public GameplayTag Tag
        {
            get => tag;
            set => tag = value;
        }

        public CueMagnitude Magnitude
        {
            get => magnitude;
            set => magnitude = value;
        }

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            CueRouter cueRouter = CueActionUtility.Resolve(router, ref _warnedRouter, nameof(ExecuteCueAction));
            if (cueRouter == null) return ActionStatus.Done;

            List<AbilitySystemComponent> targets = context.Targets;
            bool played = false;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                played = true;
                cueRouter.Execute(tag, CueActionUtility.Build(context, targets[i], magnitude));
            }

            if (!played) cueRouter.Execute(tag, CueActionUtility.Build(context, null, magnitude));
            return ActionStatus.Done;
        }

        public override void Validate(ValidationResult result)
        {
            CueActionUtility.Validate(result, router, tag, magnitude, nameof(ExecuteCueAction));
        }
    }
}
