using SpellBook.Core;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Time;
using UnityEngine;

namespace SpellBook.Samples.Sandbox
{
    /// <summary>
    /// Cor do boneco: pisca ao receber dano e fica com a cor de queimadura enquanto tiver a tag. Prioridade:
    /// acerto, depois queimadura, depois normal. O piscar é medido pela fonte de tempo e para na pausa.
    /// </summary>
    [RequireComponent(typeof(AbilitySystemComponent))]
    [HelpURL(DocUrls.Samples + "#trainingdummyfeedback")]
    public sealed class TrainingDummyFeedback : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("Renderer pintado pelo feedback.")]
        private Renderer target;

        [SerializeField, Tooltip("Tag que indica queimadura.")]
        private GameplayTag burningTag;

        [SerializeField, Tooltip("Cor sem acerto nem queimadura.")]
        private Color normal = Color.white;

        [SerializeField, Tooltip("Cor do piscar ao receber dano.")]
        private Color hit = Color.red;

        [SerializeField, Tooltip("Cor enquanto a tag de queimadura estiver ativa.")]
        private Color burning = new Color(1f, 0.5f, 0f);

        [SerializeField, Tooltip("Duração do piscar, em segundos da fonte de tempo.")]
        private float hitFlash = 0.15f;

        [SerializeField, Tooltip("Fonte de tempo do piscar. Vazio usa o tempo escalado.")]
        private TimeSourceAsset timeSource;

        private AbilitySystemComponent _asc;
        private MaterialPropertyBlock _block;
        private double _hitUntil = double.NegativeInfinity;
        private bool _burning;
        private bool _hasApplied;
        private Color _applied;

        private double Now => (timeSource != null ? timeSource : TimeSourceAsset.Scaled).ElapsedTime;

        private void Awake()
        {
            _asc = GetComponent<AbilitySystemComponent>();
            _block = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _asc.OnTagChanged += HandleTagChanged;
        }

        private void OnDisable()
        {
            _asc.OnTagChanged -= HandleTagChanged;
        }

        private void Start()
        {
            _burning = _asc.HasTag(burningTag);
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        /// <summary>Pisca a cor de acerto. Chamado pelo escape do ouvinte do canal de dano recebido.</summary>
        public void OnDamaged(float amount)
        {
            _hitUntil = Now + hitFlash;
            Apply();
        }

        private void HandleTagChanged(GameplayTag tag, bool added)
        {
            _burning = _asc.HasTag(burningTag);
            Apply();
        }

        /// <summary>Aplica a cor da vez só quando ela muda.</summary>
        private void Apply()
        {
            if (target == null)
            {
                return;
            }

            Color color = Now < _hitUntil ? hit : _burning ? burning : normal;
            if (_hasApplied && color == _applied)
            {
                return;
            }

            _block.SetColor(ColorId, color);
            target.SetPropertyBlock(_block);
            _applied = color;
            _hasApplied = true;
        }
    }
}
