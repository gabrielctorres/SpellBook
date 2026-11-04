using System;
using SpellBook.Core;
using SpellBook.Core.Text;
using UnityEngine;

namespace SpellBook.UI
{
    /// <summary>
    /// Modelo da barra (R7): valor atual, máximo, fração preenchida e fração do rastro atrasado. Sem camada de escudo:
    /// escudo é uma segunda barra com outro atributo (P1). O <c>default</c> é o estado vazio.
    /// </summary>
    public readonly struct BarModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo; a fração é <c>current / max</c> entre 0 e 1 (0 com <c>max ≤ 0</c>) e o rastro é igual a ela.</summary>
        public BarModel(float current, float max, TextSpan text, TextRole role)
        {
            Current = current;
            Max = max;
            Fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            TrailFraction = Fraction;
            Text = text;
            Role = role;
            _hasValue = true;
        }

        private BarModel(in BarModel source, float trailFraction)
        {
            this = source;
            TrailFraction = Mathf.Clamp01(trailFraction);
        }

        /// <summary>Valor atual.</summary>
        public float Current { get; }

        /// <summary>Valor máximo.</summary>
        public float Max { get; }

        /// <summary>Fração preenchida, de 0 a 1.</summary>
        public float Fraction { get; }

        /// <summary>
        /// Fração do rastro, de 0 a 1; igual à <see cref="Fraction"/> por padrão, e <see cref="WithTrail"/> a muda. O
        /// <see cref="BarBinder"/> a segura acima da fração depois de uma queda.
        /// </summary>
        public float TrailFraction { get; }

        /// <summary>Texto da barra, escrito no buffer da ligação; vazio sem texto.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>): sem dono ou sem fonte.</summary>
        public bool IsEmpty => !_hasValue;

        /// <summary>Cópia com o rastro em <paramref name="trailFraction"/>, limitado entre 0 e 1.</summary>
        public BarModel WithTrail(float trailFraction) => new BarModel(this, trailFraction);
    }

    /// <summary>Modelo do texto (R7): o texto escrito no buffer da ligação e o papel. O <c>default</c> é o estado vazio.</summary>
    public readonly struct TextModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo.</summary>
        public TextModel(TextSpan text, TextRole role)
        {
            Text = text;
            Role = role;
            _hasValue = true;
        }

        /// <summary>Texto, escrito no buffer da ligação.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>Modelo do indicador de tag (R7): aceso ou apagado. O <c>default</c> é o estado vazio.</summary>
    public readonly struct TagIndicatorModel
    {
        private readonly bool _hasValue;

        /// <summary>Cria o modelo.</summary>
        public TagIndicatorModel(bool active)
        {
            Active = active;
            _hasValue = true;
        }

        /// <summary>Verdadeiro com a tag ou a consulta satisfeita.</summary>
        public bool Active { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>
    /// Modelo do "+N" de uma lista (ADR 0039, item 6): quantos itens estão na lista sem objeto, acima do máximo visível.
    /// O <c>default</c> é zero oculto.
    /// </summary>
    public readonly struct ListOverflowModel
    {
        /// <summary>Cria o modelo; negativos valem 0.</summary>
        public ListOverflowModel(int hidden)
        {
            Hidden = hidden > 0 ? hidden : 0;
        }

        /// <summary>Itens ocultos.</summary>
        public int Hidden { get; }
    }

    /// <summary>
    /// Estado do slot (Spec do B-016, Design 2), pelas checagens de ativação até o custo. O estado ativo não está aqui:
    /// é marca à parte no <see cref="SlotModel"/>, para não esconder uma falha.
    /// </summary>
    public enum SlotState
    {
        /// <summary>Sem habilidade concedida; o <c>default</c> do modelo.</summary>
        Empty,

        /// <summary>Pode ativar.</summary>
        Ready,

        /// <summary>Em recarga.</summary>
        OnCooldown,

        /// <summary>Sem cargas.</summary>
        NoCharges,

        /// <summary>Sem recurso para o custo.</summary>
        NoResource,

        /// <summary>Bloqueado por tag ou por outra habilidade ativa.</summary>
        Blocked,
    }

    /// <summary>
    /// Modelo do slot de habilidade (R3; Spec do B-016, Design 2). Sem GAS e sem <see cref="string"/> montada: os textos
    /// são trechos do buffer da ligação. O construtor dá o estado, o ícone e o assunto; as cópias <c>With</c> dão o
    /// resto. O <c>default</c> é o slot vazio.
    /// </summary>
    public readonly struct SlotModel
    {
        /// <summary>Cria o modelo com o estado, o ícone (pode ser nulo) e a habilidade como assunto (pode ser nula).</summary>
        public SlotModel(SlotState state, Sprite icon, ScriptableObject subject)
            : this(state, icon, subject, default, 0f, 0f, false, 0, 0, 0f, null, null, default, false, 0, 0, false,
                false, false, false, false)
        {
        }

        private SlotModel(SlotState state, Sprite icon, ScriptableObject subject, TextSpan keyLabel,
            float cooldownFraction, float cooldownRemaining, bool hasCharges, int charges, int maxCharges,
            float chargeFraction, string failureKey, ScriptableObject failureTag, TextSpan failureText,
            bool failureVisible, int failureCount, int readyPulseCount, bool isActive, bool isToggledOn,
            bool isChanneling, bool autocastAllowed, bool autocastEnabled)
        {
            State = state;
            Icon = icon;
            Subject = subject;
            KeyLabel = keyLabel;
            CooldownFraction = Mathf.Clamp01(cooldownFraction);
            CooldownRemaining = Mathf.Max(0f, cooldownRemaining);
            HasCharges = hasCharges;
            Charges = Math.Max(0, charges);
            MaxCharges = Math.Max(0, maxCharges);
            ChargeFraction = Mathf.Clamp01(chargeFraction);
            FailureKey = failureKey;
            FailureTag = failureTag;
            FailureText = failureText;
            FailureVisible = failureVisible;
            FailureCount = failureCount;
            ReadyPulseCount = readyPulseCount;
            IsActive = isActive;
            IsToggledOn = isToggledOn;
            IsChanneling = isChanneling;
            AutocastAllowed = autocastAllowed;
            AutocastEnabled = autocastEnabled;
        }

        /// <summary>Estado pelas checagens até o custo.</summary>
        public SlotState State { get; }

        /// <summary>Ícone da habilidade, ou nulo.</summary>
        public Sprite Icon { get; }

        /// <summary>A habilidade, vista como <see cref="ScriptableObject"/>; a view a escreve no <see cref="UIItemContext"/>.</summary>
        public ScriptableObject Subject { get; }

        /// <summary>Rótulo da tecla, escrito no buffer da ligação; vazio sem rótulo.</summary>
        public TextSpan KeyLabel { get; }

        /// <summary>Fração da recarga que falta, de 0 a 1.</summary>
        public float CooldownFraction { get; }

        /// <summary>Segundos de recarga que faltam; nunca negativo.</summary>
        public float CooldownRemaining { get; }

        /// <summary>Verdadeiro quando a habilidade usa cargas.</summary>
        public bool HasCharges { get; }

        /// <summary>Cargas atuais; nunca negativo.</summary>
        public int Charges { get; }

        /// <summary>Cargas máximas; nunca negativo.</summary>
        public int MaxCharges { get; }

        /// <summary>Fração da recarga da próxima carga, de 0 a 1.</summary>
        public float ChargeFraction { get; }

        /// <summary>Chave do texto do último motivo de falha, ou nula.</summary>
        public string FailureKey { get; }

        /// <summary>Tag do último motivo de falha, vista como <see cref="ScriptableObject"/>, ou nula quando não há.</summary>
        public ScriptableObject FailureTag { get; }

        /// <summary>Texto do último motivo de falha, escrito no buffer da ligação.</summary>
        public TextSpan FailureText { get; }

        /// <summary>Verdadeiro enquanto o motivo da falha deve aparecer.</summary>
        public bool FailureVisible { get; }

        /// <summary>Contador que muda a cada falha; a view anima quando ele muda.</summary>
        public int FailureCount { get; }

        /// <summary>Contador que muda quando o slot volta a ficar pronto; a view pulsa quando ele muda.</summary>
        public int ReadyPulseCount { get; }

        /// <summary>Verdadeiro com a habilidade ativa.</summary>
        public bool IsActive { get; }

        /// <summary>Verdadeiro com a habilidade de alternar ligada.</summary>
        public bool IsToggledOn { get; }

        /// <summary>Verdadeiro enquanto a habilidade canaliza.</summary>
        public bool IsChanneling { get; }

        /// <summary>Verdadeiro quando a habilidade aceita autocast.</summary>
        public bool AutocastAllowed { get; }

        /// <summary>Verdadeiro com o autocast ligado.</summary>
        public bool AutocastEnabled { get; }

        /// <summary>Cópia com o rótulo da tecla.</summary>
        public SlotModel WithKeyLabel(TextSpan keyLabel) => new SlotModel(State, Icon, Subject, keyLabel,
            CooldownFraction, CooldownRemaining, HasCharges, Charges, MaxCharges, ChargeFraction, FailureKey, FailureTag,
            FailureText, FailureVisible, FailureCount, ReadyPulseCount, IsActive, IsToggledOn, IsChanneling,
            AutocastAllowed, AutocastEnabled);

        /// <summary>Cópia com a recarga; a fração fica entre 0 e 1 e os segundos negativos valem 0.</summary>
        public SlotModel WithCooldown(float fraction, float remaining) => new SlotModel(State, Icon, Subject, KeyLabel,
            fraction, remaining, HasCharges, Charges, MaxCharges, ChargeFraction, FailureKey, FailureTag, FailureText,
            FailureVisible, FailureCount, ReadyPulseCount, IsActive, IsToggledOn, IsChanneling, AutocastAllowed,
            AutocastEnabled);

        /// <summary>Cópia com as cargas e a fração da próxima; liga <see cref="HasCharges"/>. Negativos valem 0.</summary>
        public SlotModel WithCharges(int charges, int maxCharges, float nextChargeFraction) => new SlotModel(State,
            Icon, Subject, KeyLabel, CooldownFraction, CooldownRemaining, true, charges, maxCharges, nextChargeFraction,
            FailureKey, FailureTag, FailureText, FailureVisible, FailureCount, ReadyPulseCount, IsActive, IsToggledOn,
            IsChanneling, AutocastAllowed, AutocastEnabled);

        /// <summary>Cópia com o último motivo de falha, se ele aparece, o contador e a tag do motivo (nula sem tag).</summary>
        public SlotModel WithFailure(string key, TextSpan text, bool visible, int count, ScriptableObject tag = null) =>
            new SlotModel(State, Icon, Subject, KeyLabel, CooldownFraction, CooldownRemaining, HasCharges, Charges,
                MaxCharges, ChargeFraction, key, tag, text, visible, count, ReadyPulseCount, IsActive, IsToggledOn,
                IsChanneling, AutocastAllowed, AutocastEnabled);

        /// <summary>Cópia com o contador do pulso de pronto.</summary>
        public SlotModel WithReadyPulse(int count) => new SlotModel(State, Icon, Subject, KeyLabel, CooldownFraction,
            CooldownRemaining, HasCharges, Charges, MaxCharges, ChargeFraction, FailureKey, FailureTag, FailureText,
            FailureVisible, FailureCount, count, IsActive, IsToggledOn, IsChanneling, AutocastAllowed,
            AutocastEnabled);

        /// <summary>Cópia com as marcas de ativa, alternar ligada e canalizando.</summary>
        public SlotModel WithActivity(bool isActive, bool isToggledOn, bool isChanneling) => new SlotModel(State, Icon,
            Subject, KeyLabel, CooldownFraction, CooldownRemaining, HasCharges, Charges, MaxCharges, ChargeFraction,
            FailureKey, FailureTag, FailureText, FailureVisible, FailureCount, ReadyPulseCount, isActive, isToggledOn,
            isChanneling, AutocastAllowed, AutocastEnabled);

        /// <summary>Cópia com o autocast: se a habilidade aceita e se está ligado.</summary>
        public SlotModel WithAutocast(bool allowed, bool enabled) => new SlotModel(State, Icon, Subject, KeyLabel,
            CooldownFraction, CooldownRemaining, HasCharges, Charges, MaxCharges, ChargeFraction, FailureKey, FailureTag,
            FailureText, FailureVisible, FailureCount, ReadyPulseCount, IsActive, IsToggledOn, IsChanneling, allowed,
            enabled);
    }

    /// <summary>Estado da barra de cast (R17).</summary>
    public enum CastBarState
    {
        /// <summary>Sem canalização; o <c>default</c> do modelo.</summary>
        Idle,

        /// <summary>Uma habilidade canaliza.</summary>
        Channeling,
    }

    /// <summary>Como acabou a última canalização mostrada (R17).</summary>
    public enum CastBarResult
    {
        /// <summary>Nenhuma acabou ainda.</summary>
        None,

        /// <summary>Chegou ao fim da duração ou terminou sem cancelamento.</summary>
        Completed,

        /// <summary>Foi cancelada.</summary>
        Cancelled,
    }

    /// <summary>
    /// Modelo da barra de cast (R17; Spec do B-016, Design 5): a canalização mostrada e o resultado da última. Sem GAS e
    /// sem <see cref="string"/> montada: o nome vem do provedor de texto. No <see cref="CastBarState.Idle"/> depois de um
    /// fim, o ícone e o nome são os da que acabou. O <c>default</c> é o estado <see cref="CastBarState.Idle"/> sem resultado.
    /// </summary>
    public readonly struct CastBarModel
    {
        /// <summary>Cria o modelo; a fração fica entre 0 e 1, e segundos e contador negativos valem 0.</summary>
        public CastBarModel(CastBarState state, Sprite icon, string name, float fraction, float remaining,
            bool indeterminate, CastBarResult lastResult, int resultCount)
        {
            State = state;
            Icon = icon;
            Name = name;
            Fraction = Mathf.Clamp01(fraction);
            Remaining = Mathf.Max(0f, remaining);
            Indeterminate = indeterminate;
            LastResult = lastResult;
            ResultCount = Math.Max(0, resultCount);
        }

        /// <summary>Se há canalização.</summary>
        public CastBarState State { get; }

        /// <summary>Ícone da habilidade, ou nulo.</summary>
        public Sprite Icon { get; }

        /// <summary>Nome da habilidade, pelo provedor de texto; nulo sem habilidade.</summary>
        public string Name { get; }

        /// <summary>Progresso, de 0 a 1; 0 na canalização indeterminada.</summary>
        public float Fraction { get; }

        /// <summary>Segundos até o fim; 0 na canalização indeterminada.</summary>
        public float Remaining { get; }

        /// <summary>Verdadeiro na canalização sem duração, que dura até ser cancelada.</summary>
        public bool Indeterminate { get; }

        /// <summary>Como acabou a última canalização mostrada.</summary>
        public CastBarResult LastResult { get; }

        /// <summary>Contador que muda a cada fim; a view anima quando ele muda.</summary>
        public int ResultCount { get; }
    }

    /// <summary>
    /// Pedido de um número flutuante (R14): onde nasce, o texto, o papel e o estilo. O texto é escrito no buffer da
    /// ligação e vale só durante o <see cref="IFloatingTextView.Show"/>. O <c>default</c> é o estado vazio: nada nasce.
    /// </summary>
    public readonly struct FloatingTextRequest
    {
        private readonly bool _hasValue;

        /// <summary>Cria o pedido; <paramref name="style"/> pode ser nulo (duração <see cref="FloatingTextStyle.DefaultDuration"/>).</summary>
        public FloatingTextRequest(Vector3 worldPosition, TextSpan text, TextRole role, FloatingTextStyle style)
        {
            WorldPosition = worldPosition;
            Text = text;
            Role = role;
            Style = style;
            _hasValue = true;
        }

        /// <summary>Ponto do mundo em que o número nasce; a view o converte para o container.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Texto do número, escrito no buffer da ligação.</summary>
        public TextSpan Text { get; }

        /// <summary>Papel do texto, que o tema traduz em cor.</summary>
        public TextRole Role { get; }

        /// <summary>Estilo do número, ou nulo.</summary>
        public FloatingTextStyle Style { get; }

        /// <summary>Verdadeiro no estado vazio (<c>default</c>).</summary>
        public bool IsEmpty => !_hasValue;
    }

    /// <summary>
    /// View de um número flutuante (R14), no prefab que o <see cref="FloatingTextBinder"/> cria pelo spawner. Recebe o
    /// pedido ao nascer e o progresso a cada tick; não lê o tempo nem volta ao pool sozinha.
    /// </summary>
    public interface IFloatingTextView
    {
        /// <summary>Mostra o número do <paramref name="request"/>; o texto deve ser copiado aqui.</summary>
        void Show(in FloatingTextRequest request);

        /// <summary>Progresso da duração, de 0 (nasceu) a 1 (vai voltar ao spawner).</summary>
        void SetProgress(float normalized);
    }
}
