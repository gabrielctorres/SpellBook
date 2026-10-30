using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal com payload <see cref="GameObject"/>. Não há canal de <c>Component</c>: passe o <see cref="GameObject"/> e leia o componente nele.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/GameObject Channel")]
    [HelpURL(DocUrls.Events + "#gameobjecteventchannel")]
    public sealed class GameObjectEventChannel : EventChannel<GameObject>
    {
    }
}
