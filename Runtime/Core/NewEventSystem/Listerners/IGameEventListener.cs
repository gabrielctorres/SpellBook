
namespace SpellBook.Core.NewEventSystem
{
    public interface IGameEventListener<T>
    {
        public void OnEventRaised(T item);
    }
}
