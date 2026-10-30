namespace SpellBook.Core.Validation
{
    /// <summary>
    /// Tipo do pacote com regra de validação (ADR 0026, item 6). O <c>Validate</c> não muda estado, não escreve no
    /// console e não acessa <c>UnityEditor</c>; só adiciona mensagens curtas, em português, que dizem o que corrigir.
    /// </summary>
    public interface IValidatable
    {
        void Validate(ValidationResult result);
    }
}
