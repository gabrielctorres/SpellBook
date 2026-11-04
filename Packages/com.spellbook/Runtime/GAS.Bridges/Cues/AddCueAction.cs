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
    /// Liga o cue <see cref="CueKind.Looping"/> da tag no primeiro alvo resolvido (ou no ponto) enquanto o dono da lista
    /// dura, e termina na mesma chamada. Uma execução de vigia <see cref="ExecutionLink.FollowsOwner"/> remove o cue quando
    /// o efeito sai, por qualquer motivo, ou quando a ativação termina ou é cancelada, sem segurar a ativação (ADR 0036).
    /// Numa lista sem dono, ninguém removeria o cue: ele toca uma vez, como explosão, com aviso.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#addcueaction")]
    [Serializable, TypeCategory("Apresentação", Summary = "Liga um cue contínuo que some quando o efeito sai ou a ativação termina.",
        Line = "Cue contínuo {tag}")]
    public sealed class AddCueAction : GameplayAction
    {
        [SerializeField, Tooltip("Âncora em que o roteador de cues da cena se publica.")]
        private CueRouterAnchor router;

        [SerializeField, Tooltip("Tag do cue ligado. Sem definição para ela, procura a da tag pai.")]
        private GameplayTag tag;

        [SerializeField, Tooltip("Magnitude passada ao cue.")]
        private CueMagnitude magnitude = new CueMagnitude();

        [NonSerialized] private bool _warnedRouter;
        [NonSerialized] private bool _warnedOwner;
        [NonSerialized] private GameplayAction[] _watch;

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
            CueRouter cueRouter = CueActionUtility.Resolve(router, ref _warnedRouter, nameof(AddCueAction));
            if (cueRouter == null) return ActionStatus.Done;

            // A vigia liga o cue na própria execução e o remove no OnCancel dela
            _watch ??= new GameplayAction[] { new CueWatchAction(this) };
            if (context.RunFollowingOwner(_watch)) return ActionStatus.Done;

            if (!_warnedOwner && Debug.isDebugBuild)
            {
                _warnedOwner = true;
                Debug.LogWarning(
                    $"[GAS] AddCueAction ({tag.Name}) numa lista sem dono: nada removeria o cue, então ele tocou uma vez, como explosão.");
            }

            CueHandle handle = cueRouter.Add(tag, CueActionUtility.Build(context, FirstTarget(context.Targets), magnitude));
            cueRouter.Remove(handle);
            return ActionStatus.Done;
        }

        public override void Validate(ValidationResult result)
        {
            CueActionUtility.Validate(result, router, tag, magnitude, nameof(AddCueAction));
        }

        private static AbilitySystemComponent FirstTarget(List<AbilitySystemComponent> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null) return targets[i];
            }

            return null;
        }

        /// <summary>
        /// Vigia do cue: liga o cue, guarda o handle no quadro (<c>Index</c> = slot, <c>Count</c> = geração, <c>Data</c> =
        /// roteador) e espera até ser cancelada. O cancelamento remove o cue.
        /// </summary>
        private sealed class CueWatchAction : GameplayAction
        {
            private readonly AddCueAction _source;

            public CueWatchAction(AddCueAction source) => _source = source;

            protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Phase > 0) return ActionStatus.Done;
                frame.Phase = 1;

                CueRouter cueRouter = _source.router != null ? _source.router.Value : null;
                if (cueRouter == null) return ActionStatus.Done;

                CueParameters parameters = CueActionUtility.Build(context, FirstTarget(context.Targets), _source.magnitude);
                CueHandle handle = cueRouter.Add(_source.tag, parameters);
                if (!cueRouter.IsActive(handle)) return ActionStatus.Done;

                frame.Index = handle.Slot;
                frame.Count = handle.Generation;
                frame.Data = cueRouter;
                return ActionStatus.WaitUntilCancelled;
            }

            protected override void OnCancel(ActionContext context, ref ActionFrame frame)
            {
                if (frame.Data is CueRouter cueRouter && cueRouter != null)
                {
                    cueRouter.Remove(new CueHandle(frame.Index, frame.Count));
                }
            }
        }
    }
}
