using UnityEngine;
namespace SpellBook.Core.NewEventSystem
{
    [CreateAssetMenu(fileName = "New Void Event", menuName = "SmitePackage/NewEventSystem/VoidEvent", order = 0)]
    public class VoidEvent : BaseGameEvent<Void>
    {
        public void Raise() => Raise(new Void());
    }

}