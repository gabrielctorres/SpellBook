using SpellBook.GAS.Actions;

namespace SpellBook.GAS.Abilities
{
    /// <summary>
    /// Instância de uma habilidade concedida a um AbilitySystemComponent.
    /// O GameplayAbility (ScriptableObject) é compartilhado; o estado por personagem fica aqui.
    /// </summary>
    public class AbilitySpec
    {
        public GameplayAbility Ability { get; }
        /// <summary>Nível atual, em [1, MaxLevel]. Muda por <c>AbilitySystemComponent.SetAbilityLevel</c>.</summary>
        public int Level { get; internal set; }
        public bool IsActive { get; internal set; }
        /// <summary>A ativação em curso já cobrou a recarga, o custo e a carga. Volta a <c>false</c> a cada ativação.</summary>
        public bool IsCommitted { get; internal set; }
        /// <summary>Contexto da ativação em curso, preenchido até o fim da habilidade; nulo com ela inativa. É do ASC: não guarde depois do fim.</summary>
        public ActionContext ActiveContext { get; internal set; }

        /// <summary>Execução de <c>OnActivate</c> da ativação em curso; só vale com <see cref="Executing"/>.</summary>
        internal ActionExecutionHandle Execution;

        /// <summary>A execução de <c>OnActivate</c> ficou rodando depois da ativação; o ASC termina a habilidade quando ela acaba.</summary>
        internal bool Executing;

        /// <summary>Conta as ativações, para distinguir a ativação em curso de uma anterior que usou o mesmo contexto do pool.</summary>
        internal int ActivationId;

        /// <summary>A ativação aplicou a recarga e o <c>Ended</c> ainda não saiu.</summary>
        internal bool CooldownTracked;

        /// <summary>Cargas atuais (com cargas desligadas, 0). Muda pelo ASC: ativação, recarga, <c>AddCharges</c> e <c>RefillCharges</c>.</summary>
        public int Charges { get; internal set; }

        /// <summary>
        /// Máximo de cargas avaliado pelo ASC (inteiro, pelo menos 0; com cargas desligadas, 0), na concessão, a cada mudança
        /// de nível e a cada <c>Tick</c>.
        /// </summary>
        public int MaxCharges { get; internal set; }

        /// <summary>Segundos até voltar a próxima carga; 0 com a recarga parada.</summary>
        public float ChargeRechargeRemaining { get; internal set; }

        /// <summary>Duração aplicada da recarga em curso, já com a redução de recarga; 0 com a recarga parada.</summary>
        public float ChargeRechargeDuration { get; internal set; }

        /// <summary>A recarga de carga está correndo.</summary>
        internal bool ChargeRecharging;

        internal AbilitySpec(GameplayAbility ability, int level)
        {
            Ability = ability;
            Level = level;
        }
    }
}
