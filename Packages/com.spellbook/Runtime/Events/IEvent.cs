namespace SpellBook.Events
{
    /// <summary>
    /// Marca um struct como evento do <see cref="EventBus{T}"/>. Use só para fatos globais entre
    /// sistemas de código; estado de uma entidade fica num <c>event</c> C# do próprio componente (ADR 0016).
    /// </summary>
    public interface IEvent
    {
    }
}
