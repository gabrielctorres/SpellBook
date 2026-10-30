using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Events
{
    /// <summary>Canal sem dado: só avisa que algo aconteceu. O payload é <see cref="Void"/> do SpellBook, não <c>System.Void</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Events/Void Channel")]
    [HelpURL(DocUrls.Events + "#voideventchannel")]
    public sealed class VoidEventChannel : EventChannel<Void>
    {
        /// <summary>Dispara o canal.</summary>
        public void Raise()
        {
            Raise(default(Void));
        }
    }
}
