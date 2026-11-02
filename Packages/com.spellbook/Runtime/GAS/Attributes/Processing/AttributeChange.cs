using SpellBook.GAS.Effects;

namespace SpellBook.GAS.Attributes
{
    /// <summary>
    /// Alteração pendente da base de um atributo, antes de ser aplicada (ADR 0033). Os processadores da
    /// <see cref="AttributeDefinition"/> mudam o <see cref="Delta"/>; zero anula a alteração.
    /// </summary>
    public struct AttributeChange
    {
        public AttributeChange(AttributeDefinition attribute, float requested, GameplayEffect effect)
        {
            Attribute = attribute;
            Requested = requested;
            Delta = requested;
            Effect = effect;
        }

        /// <summary>Atributo que recebe a alteração.</summary>
        public AttributeDefinition Attribute { get; }

        /// <summary>Variação pedida, antes dos processadores.</summary>
        public float Requested { get; }

        /// <summary>Variação corrente, que os processadores mudam na ordem da lista.</summary>
        public float Delta;

        /// <summary>Efeito que pediu a alteração; nulo numa alteração feita por código.</summary>
        public GameplayEffect Effect { get; }
    }

    /// <summary>
    /// O que aconteceu com uma alteração aplicada: pedida, final (depois dos processadores) e aplicada (depois dos limites).
    /// Um relatório <c>default</c> tem <see cref="IsValid"/> falso.
    /// </summary>
    public readonly struct AttributeChangeReport
    {
        public AttributeChangeReport(in AttributeChange change, float applied, in EffectContext context)
        {
            Attribute = change.Attribute;
            Requested = change.Requested;
            Final = change.Delta;
            Applied = applied;
            Effect = change.Effect;
            Context = context;
            IsValid = true;
        }

        /// <summary>Atributo que recebeu a alteração.</summary>
        public AttributeDefinition Attribute { get; }

        /// <summary>Variação pedida, antes dos processadores.</summary>
        public float Requested { get; }

        /// <summary>Variação depois dos processadores.</summary>
        public float Final { get; }

        /// <summary>Quanto a base mudou de fato, depois dos limites do atributo.</summary>
        public float Applied { get; }

        /// <summary>Efeito de origem; nulo numa alteração feita por código.</summary>
        public GameplayEffect Effect { get; }

        /// <summary>Contexto da aplicação; vazio numa alteração feita por código sem contexto.</summary>
        public EffectContext Context { get; }

        /// <summary>Falso no relatório <c>default</c>, que não veio de uma alteração.</summary>
        public bool IsValid { get; }
    }

    /// <summary>Ouvinte do relatório de uma alteração aplicada.</summary>
    public delegate void AttributeChangeHandler(in AttributeChangeReport report);
}
