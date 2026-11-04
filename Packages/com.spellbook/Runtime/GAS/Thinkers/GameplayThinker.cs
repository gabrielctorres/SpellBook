using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.Core.Validation;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEngine;

namespace SpellBook.GAS.Thinkers
{
    /// <summary>
    /// Objeto no mundo com vida, pulsos e eventos de ações (ADR 0036). As execuções dele rodam no
    /// <see cref="AbilitySystemComponent"/> do próprio objeto, com o thinker como dono lógico, e o tempo é o desse ASC:
    /// a vida conta pulsos e duração pelo executor, sem relógio próprio. Volta pelo spawner que o criou e reinicia o
    /// estado no <c>OnEnable</c> (ADR 0015).
    /// </summary>
    [RequireComponent(typeof(AbilitySystemComponent))]
    [HelpURL(DocUrls.Thinkers + "#gameplaythinker")]
    public class GameplayThinker : MonoBehaviour, IValidatable
    {
        [Tooltip("Vida em segundos do ASC, por nível. 0 ou menos: sem duração.")]
        [SerializeField] private ScalableFloat duration = ScalableFloat.Constant(0f);
        [Tooltip("Segundos entre pulsos, por nível. 0 ou menos: sem pulsos.")]
        [SerializeField] private ScalableFloat pulseInterval = ScalableFloat.Constant(0f);
        [Tooltip("Copia a posição de quem o criou a cada quadro. Criador destruído ou desativado termina o thinker.")]
        [SerializeField] private bool followOwner;
        [Tooltip("Com Follow Owner, copia também a rotação.")]
        [SerializeField] private bool followRotation;
        [Tooltip("Termina quando a ativação ou o efeito que o criou termina ou é cancelado, ou quando o criador some.")]
        [SerializeField] private bool endWithExecution = true;
        [SerializeReference, TypePicker] private ThinkerBehaviour behaviour;
        [SerializeReference, TypePicker, ActionEvent("Ao criar", DefaultTarget.Owner, true)]
        private List<GameplayAction> onCreated = new List<GameplayAction>();
        [SerializeReference, TypePicker, ActionEvent("A cada pulso", DefaultTarget.Owner, true)]
        private List<GameplayAction> onPulse = new List<GameplayAction>();
        [SerializeReference, TypePicker, ActionEvent("Ao terminar", DefaultTarget.Owner, true)]
        private List<GameplayAction> onEnded = new List<GameplayAction>();

        private AbilitySystemComponent _asc;
        private GameplayAction[] _life;
        private object _state;
        private bool _begun;
        private bool _ended;
        private SpawnerAsset _spawner;
        // Cópia do contexto de criação (P11): o criador é o instigador das execuções do thinker
        private AbilitySystemComponent _instigator;
        private GameplayAbility _ability;
        private AbilitySpec _spec;
        private int _level = 1;
        private EffectContext _effect;
        private bool _hasEffect;
        private bool _hasCreator;
        // Vigia de EndWithExecution no ASC do criador, cancelada no término
        private AbilitySystemComponent _watchHost;
        private ActionExecutionHandle _watch;

        /// <summary>Vida por nível, avaliada no <see cref="Begin"/> com o nível e a tabela da habilidade de criação.</summary>
        public ScalableFloat Duration { get => duration; set => duration = value; }

        /// <summary>Intervalo entre pulsos por nível, avaliado no <see cref="Begin"/>.</summary>
        public ScalableFloat PulseInterval { get => pulseInterval; set => pulseInterval = value; }

        /// <summary>Se segue a posição do criador. Criador destruído ou desativado termina com <see cref="ActionEndReason.OwnerGone"/>.</summary>
        public bool FollowOwner { get => followOwner; set => followOwner = value; }

        /// <summary>Com <see cref="FollowOwner"/>, se segue também a rotação do criador.</summary>
        public bool FollowRotation { get => followRotation; set => followRotation = value; }

        /// <summary>
        /// Se termina com <see cref="ActionEndReason.ExecutionEnded"/> quando o dono da execução que o criou (ativação ou
        /// instância de efeito) termina ou é cancelado (ADR 0036, item 5), e com <see cref="ActionEndReason.OwnerGone"/>
        /// quando o criador some.
        /// </summary>
        public bool EndWithExecution { get => endWithExecution; set => endWithExecution = value; }

        /// <summary>Comportamento do thinker, como a aura. Nulo: só as listas de ações.</summary>
        public ThinkerBehaviour Behaviour { get => behaviour; set => behaviour = value; }

        /// <summary>Ações do começo da vida. Sem seletor, o alvo é o próprio thinker.</summary>
        public List<GameplayAction> OnCreated => onCreated;

        /// <summary>Ações de cada pulso, cada uma numa execução própria: a espera de um pulso não atrasa o seguinte.</summary>
        public List<GameplayAction> OnPulse => onPulse;

        /// <summary>Ações do término, com o motivo em <see cref="ActionContext.EndReason"/>. O que ficar esperando é cancelado.</summary>
        public List<GameplayAction> OnEnded => onEnded;

        /// <summary>ASC do thinker, dono das execuções e do tempo dele.</summary>
        public AbilitySystemComponent AbilitySystem => _asc != null ? _asc : _asc = GetComponent<AbilitySystemComponent>();

        /// <summary>Se a vida atual já terminou.</summary>
        public bool HasEnded => _ended;

        // Duração e intervalo da vida atual, avaliados no Begin
        internal float LifeDuration { get; private set; }
        internal float LifeInterval { get; private set; }

        private void Awake()
        {
            _life = new GameplayAction[] { new ThinkerLifeAction(this) };
            _state = behaviour?.CreateState();
        }

        private void OnEnable()
        {
            _begun = false;
            _ended = false;
            _spawner = null;
            _instigator = null;
            _ability = null;
            _spec = null;
            _level = 1;
            _effect = default;
            _hasEffect = false;
            _hasCreator = false;
            _watchHost = null;
            _watch = default;
        }

        // Thinker colocado na cena (P2): sem criador, instigador nulo e nível 1
        private void Start()
        {
            if (!_begun && !_ended) Begin(null, null, null);
        }

        // Desligado sem término: a vida para sem "Ao terminar"
        private void OnDisable()
        {
            if (_begun && !_ended && AbilitySystem != null) AbilitySystem.CancelActions(this);
            CancelWatch();
        }

        // Segue o criador e confere se ele continua ativo (ADR 0036, item 6). Só lê o transform; o tempo é do ASC.
        private void LateUpdate()
        {
            if (!_hasCreator || _ended || !_begun) return;
            if (_instigator == null || !_instigator.isActiveAndEnabled)
            {
                if (followOwner || endWithExecution) End(ActionEndReason.OwnerGone);
                return;
            }
            if (!followOwner) return;
            Transform creator = _instigator.transform;
            if (followRotation) transform.SetPositionAndRotation(creator.position, creator.rotation);
            else transform.position = creator.position;
        }

        /// <summary>
        /// Começa a vida: guarda o <paramref name="creator"/> como instigador e copia habilidade, spec, nível e
        /// <see cref="EffectContext"/> do <paramref name="creationContext"/> (pode ser nulo), e roda a vida no ASC do
        /// thinker. O término volta pelo <paramref name="spawner"/>, ou pelo <see cref="SpawnerAsset.Default"/>. Uma vez
        /// por ciclo do pool; o thinker da cena começa sozinho no <c>Start</c>.
        /// </summary>
        public void Begin(AbilitySystemComponent creator, ActionContext creationContext, SpawnerAsset spawner)
        {
            if (_begun) return;
            _begun = true;
            _spawner = spawner;
            _instigator = creator;
            _hasCreator = creator != null;
            if (creationContext != null)
            {
                _ability = creationContext.Ability;
                _spec = creationContext.Spec;
                _level = creationContext.Level;
                _hasEffect = creationContext.HasEffect;
                _effect = creationContext.Effect;
            }
            // Comportamento atribuído depois do Awake: o estado nasce na primeira vida
            if (_state == null && behaviour != null) _state = behaviour.CreateState();
            LifeDuration = Evaluate(duration);
            LifeInterval = Evaluate(pulseInterval);
            AbilitySystem.RunActions(_life, RentContext(), this);
        }

        /// <summary>
        /// Termina a vida uma vez: cancela as execuções do thinker, roda <see cref="OnEnded"/> com
        /// <paramref name="reason"/>, chama o <c>OnEnd</c> do comportamento e volta pelo spawner.
        /// </summary>
        public void End(ActionEndReason reason)
        {
            if (_ended) return;
            _ended = true;
            AbilitySystemComponent asc = AbilitySystem;
            asc.CancelActions(this);

            ActionContext context = RentContext();
            context.EndReason = reason;
            RunEvent(onEnded, context);
            if (behaviour != null)
            {
                context = RentContext();
                context.EndReason = reason;
                try { behaviour.OnEnd(_state, this, context); }
                finally { asc.Runner.ReturnContext(context); }
            }
            CancelWatch();
            // O que "Ao terminar" deixou esperando não passa do término
            asc.CancelActions(this);
            SpawnerAsset.OrDefault(_spawner).Release(gameObject);
        }

        // Guarda a vigia de EndWithExecution que a CreateThinkerAction começou no ASC do criador
        internal void SetWatch(AbilitySystemComponent host, ActionExecutionHandle watch)
        {
            _watchHost = host;
            _watch = watch;
        }

        // Cancela a vigia, se ainda está em curso. O OnCancel dela não termina o thinker já terminado ou desligado.
        private void CancelWatch()
        {
            AbilitySystemComponent host = _watchHost;
            _watchHost = null;
            if (host != null && _watch.IsRunning) host.Runner.Cancel(_watch);
            _watch = default;
        }

        /// <summary>
        /// Acusa duração ou intervalo constantes negativos e o objeto sem ASC (erros); o thinker que nunca termina, ações
        /// "A cada pulso" sem intervalo, comportamento que pulsa sem intervalo ou com intervalo abaixo de um quadro e, no
        /// thinker colocado na cena, ações que leem o instigador, que ele não tem (avisos). Valida as listas de ações e o
        /// comportamento.
        /// </summary>
        public void Validate(ValidationResult result)
        {
            duration?.Validate(result, nameof(duration));
            pulseInterval?.Validate(result, nameof(pulseInterval));
            bool hasDuration = !NeverPositive(duration);
            bool hasPulse = !NeverPositive(pulseInterval);
            if (IsNegativeConstant(duration)) result.AddError("Duração negativa: use 0 para \"sem duração\".", nameof(duration));
            if (IsNegativeConstant(pulseInterval)) result.AddError("Intervalo negativo: use 0 para \"sem pulsos\".", nameof(pulseInterval));
            if (!hasDuration && !hasPulse && !endWithExecution)
            {
                result.AddWarning("Sem duração, sem pulsos e sem End With Execution: o thinker nunca termina sozinho.", nameof(duration));
            }

            if (!hasPulse && onPulse != null && onPulse.Count > 0)
            {
                result.AddWarning($"{onPulse.Count} ações em \"A cada pulso\" não vão rodar: o thinker não tem intervalo.", nameof(onPulse));
            }

            if (behaviour != null && behaviour.Pulses) ValidatePulses(result, hasPulse);
            if (!TryGetComponent(out AbilitySystemComponent _))
            {
                result.AddError("Thinker sem AbilitySystemComponent: acrescente o componente, que roda as ações e o tempo dele.");
            }

            // O thinker da cena começa sem criador e sem instigador (P2)
            bool inScene = IsPlacedInScene();
            ActionVisitor visitor = (action, path, _) =>
            {
                if (inScene && action.ReadsInstigator)
                {
                    result.AddWarning("Ação que lê o instigador num thinker colocado na cena: sem criador, ele é nulo e a ação não faz nada.", path);
                }
            };
            ActionLists.Validate(this, nameof(onCreated), onCreated, result, visitor);
            ActionLists.Validate(this, nameof(onPulse), onPulse, result, visitor);
            ActionLists.Validate(this, nameof(onEnded), onEnded, result, visitor);
            ValidateBehaviour(result);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Diz se o objeto está numa cena de prévia do Editor, como a do Prefab Mode. Preenchido pelo código de Editor,
        /// que o runtime não referencia.
        /// </summary>
        internal static System.Func<GameObject, bool> IsInPreviewScene;
#endif

        // Prefab em asset não tem cena, e o prefab aberto no Prefab Mode fica numa cena de prévia: os dois ficam fora
        private bool IsPlacedInScene()
        {
            if (!gameObject.scene.IsValid()) return false;
#if UNITY_EDITOR
            if (IsInPreviewScene != null && IsInPreviewScene(gameObject)) return false;
#endif
            return true;
        }

        private void ValidatePulses(ValidationResult result, bool hasPulse)
        {
            if (!hasPulse)
            {
                result.AddWarning("Comportamento que age nos pulsos sem intervalo: só o pulso inicial roda.", nameof(pulseInterval));
            }
            else if (pulseInterval.Mode == ScalableFloat.ScalableMode.Constant && pulseInterval.Evaluate(1) < MinPulseInterval)
            {
                result.AddWarning("Intervalo menor que um quadro (1/60 s): vários pulsos podem cair no mesmo passo.", nameof(pulseInterval));
            }
        }

        // O comportamento acusa o caminho relativo a ele; aqui ele ganha o do campo
        private void ValidateBehaviour(ValidationResult result)
        {
            if (behaviour == null) return;
            var own = new ValidationResult(result.Context);
            behaviour.Validate(own);
            for (int i = 0; i < own.Messages.Count; i++)
            {
                ValidationMessage message = own.Messages[i];
                string path = string.IsNullOrEmpty(message.PropertyPath) ? nameof(behaviour) : nameof(behaviour) + "." + message.PropertyPath;
                if (message.Severity == ValidationSeverity.Error) result.AddError(message.Message, path);
                else result.AddWarning(message.Message, path);
            }
        }

        private const float MinPulseInterval = 1f / 60f;

        // Só a constante é conferida: array, curva e tabela variam com o nível
        private static bool NeverPositive(ScalableFloat value)
        {
            return value == null || (value.Mode == ScalableFloat.ScalableMode.Constant && value.Evaluate(1) <= 0f);
        }

        private static bool IsNegativeConstant(ScalableFloat value)
        {
            return value != null && value.Mode == ScalableFloat.ScalableMode.Constant && value.Evaluate(1) < 0f;
        }

        // Duração, intervalo e valores do comportamento, lidos da tabela da habilidade de criação
        internal void CollectScalables(List<ScalableFloat> results)
        {
            if (duration != null) results.Add(duration);
            if (pulseInterval != null) results.Add(pulseInterval);
            behaviour?.CollectScalables(results);
        }

        private void OnDrawGizmosSelected()
        {
            behaviour?.DrawGizmos(this);
        }

        // Valor por nível com a tabela da habilidade de criação
        private float Evaluate(ScalableFloat value)
        {
            return value != null ? value.Evaluate(_level, _ability != null ? _ability.Values : null) : 0f;
        }

        // Começo da vida, chamado pela ThinkerLifeAction: o comportamento e depois "Ao criar"
        internal void RunCreated()
        {
            ActionContext context = RentContext();
            behaviour?.OnBegin(_state, this, context);
            RunEvent(onCreated, context);
        }

        // Um pulso: o comportamento e depois "A cada pulso", numa execução própria
        internal void RunPulse()
        {
            ActionContext context = RentContext();
            behaviour?.OnPulse(_state, this, context);
            RunEvent(onPulse, context);
        }

        private void RunEvent(List<GameplayAction> actions, ActionContext context)
        {
            if (actions != null && actions.Count > 0) AbilitySystem.RunActions(actions, context, this);
            else AbilitySystem.Runner.ReturnContext(context);
        }

        // Contexto do pool do ASC do thinker, com a cópia do contexto de criação. O alvo do evento é o thinker, como o
        // dono nas listas de efeito
        private ActionContext RentContext()
        {
            ActionContext context = AbilitySystem.Runner.RentContext();
            context.EventTargets.Add(AbilitySystem);
            context.Instigator = _instigator;
            context.Ability = _ability;
            context.Spec = _spec;
            context.Level = _level;
            if (_hasEffect) context.SetEffect(_effect);
            context.DefaultTarget = DefaultTarget.Owner;
            return context;
        }
    }
}
