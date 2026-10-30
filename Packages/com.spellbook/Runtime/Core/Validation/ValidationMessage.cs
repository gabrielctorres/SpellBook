namespace SpellBook.Core.Validation
{
    /// <summary>Gravidade de uma mensagem de validação (ADR 0026, item 6).</summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    /// <summary>Uma mensagem gerada por <see cref="IValidatable.Validate"/>, com o objeto e o campo a corrigir.</summary>
    public readonly struct ValidationMessage
    {
        public ValidationMessage(ValidationSeverity severity, string message, UnityEngine.Object context, string propertyPath)
        {
            Severity = severity;
            Message = message;
            Context = context;
            PropertyPath = propertyPath;
        }

        /// <summary>Erro ou aviso.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>Texto curto que diz o que corrigir.</summary>
        public string Message { get; }

        /// <summary>Objeto validado, usado para selecionar e fazer ping.</summary>
        public UnityEngine.Object Context { get; }

        /// <summary>Caminho do campo serializado com o problema, ou <c>null</c> quando a regra vale para o objeto todo.</summary>
        public string PropertyPath { get; }
    }
}
