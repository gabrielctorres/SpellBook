using System;
using System.Collections.Generic;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI
{
    /// <summary>
    /// Fonte da barra de cast (Spec do B-016, Design 5 e P7; R18 e R19 do PRD): mostra a canalização em curso no ASC
    /// dono, com ícone, nome, fração e segundos lidos do ASC, e o resultado da última que acabou. Com canalizações
    /// simultâneas, mostra a que começou por último e, quando ela acaba, a mais recente das que sobram. Atualiza pelo
    /// início e pelo fim das habilidades e pede tick só enquanto a mostrada tem duração. Não ativa nem cancela nada.
    /// </summary>
    [Serializable, TypeCategory("Habilidades", Summary = "Barra de cast: a canalização em curso no dono, com progresso, segundos e o resultado da última.",
        Line = "Barra de cast · prévia {previewAbility}")]
    public sealed class ChannelSource : UISource<CastBarModel>
    {
        private const float PreviewFraction = 0.6f;
        private const float PreviewRemaining = 1.2f;
        private const string PreviewName = "Fireball";

        [Tooltip("Habilidade cujo ícone e nome aparecem na prévia, fora do Play. Vazia: \"Fireball\" sem ícone.")]
        [SerializeField] private GameplayAbility previewAbility;

        /// <summary>Habilidade mostrada na prévia, ou nula.</summary>
        public GameplayAbility PreviewAbility { get => previewAbility; set => previewAbility = value; }

        /// <inheritdoc />
        public override Type OwnerType => typeof(AbilitySystemComponent);

        /// <inheritdoc />
        public override UIBinding<CastBarModel> CreateBinding() => new Binding();

        /// <summary>A barra em 60 %, com 1,2 s, o ícone e o nome da <see cref="PreviewAbility"/> ou "Fireball" (R21).</summary>
        public override CastBarModel CreatePreview(in UIPreviewContext context)
        {
            string name = previewAbility != null ? previewAbility.DisplayName.Resolve(context.Text) : PreviewName;
            return new CastBarModel(CastBarState.Channeling, previewAbility != null ? previewAbility.Icon : null,
                string.IsNullOrEmpty(name) ? PreviewName : name, PreviewFraction, PreviewRemaining, false,
                CastBarResult.None, 0);
        }

        // Assina o início e o fim das habilidades com delegates criados uma vez; ligar, publicar e o tick não alocam
        private sealed class Binding : UIBinding<CastBarModel>
        {
            private readonly Action<AbilitySpec> _onActivated;
            private readonly Action<AbilitySpec, bool> _onEnded;

            private AbilitySystemComponent _asc;
            private AbilitySpec _shown;
            private Sprite _icon;
            private string _name;
            private CastBarResult _lastResult;
            private int _resultCount;

            public Binding()
            {
                _onActivated = OnActivated;
                _onEnded = OnEnded;
            }

            public override void Tick(double time) => Publish();

            // Dono que não é ASC: Idle, sem assinar nada
            protected override void OnAttach(Object owner)
            {
                if (owner is not AbilitySystemComponent asc)
                {
                    Publish(new CastBarModel(CastBarState.Idle, null, null, 0f, 0f, false, _lastResult, _resultCount));
                    return;
                }

                _asc = asc;
                _asc.OnAbilityActivated += _onActivated;
                _asc.OnAbilityEnded += _onEnded;
                Show(FindMostRecent());
            }

            protected override void OnDetach()
            {
                if (_asc != null)
                {
                    _asc.OnAbilityActivated -= _onActivated;
                    _asc.OnAbilityEnded -= _onEnded;
                }

                _asc = null;
                _shown = null;
            }

            private void OnActivated(AbilitySpec spec)
            {
                if (spec.IsChanneling) Show(spec);
            }

            // Só o fim da mostrada é resultado; o de outra, ou de habilidade que não canaliza, não publica
            private void OnEnded(AbilitySpec spec, bool cancelled)
            {
                if (spec != _shown) return;

                _lastResult = cancelled ? CastBarResult.Cancelled : CastBarResult.Completed;
                _resultCount++;
                Show(FindMostRecent());
            }

            // A canalização em curso que começou por último, a de menos tempo decorrido; laço sem alocar
            private AbilitySpec FindMostRecent()
            {
                IReadOnlyList<AbilitySpec> abilities = _asc.Abilities;
                AbilitySpec found = null;
                float foundElapsed = 0f;
                for (int i = 0; i < abilities.Count; i++)
                {
                    AbilitySpec spec = abilities[i];
                    if (!spec.IsChanneling) continue;

                    float elapsed = _asc.GetChannelElapsed(spec);
                    if (found != null && elapsed > foundElapsed) continue;

                    found = spec;
                    foundElapsed = elapsed;
                }

                return found;
            }

            // Nula: Idle, com o ícone e o nome da última mostrada
            private void Show(AbilitySpec spec)
            {
                _shown = spec;
                if (spec != null)
                {
                    _icon = spec.Ability.Icon;
                    _name = spec.Ability.DisplayName.Resolve(Context.Text);
                }

                Publish();
            }

            private void Publish()
            {
                AbilitySpec spec = _shown;
                bool timed = spec != null && spec.ChannelDuration > 0f;
                Publish(spec == null
                    ? new CastBarModel(CastBarState.Idle, _icon, _name, 0f, 0f, false, _lastResult, _resultCount)
                    : new CastBarModel(CastBarState.Channeling, _icon, _name, _asc.GetChannelNormalized(spec),
                        _asc.GetChannelRemaining(spec), !timed, _lastResult, _resultCount));
                RequestTick(timed);
            }
        }
    }
}
