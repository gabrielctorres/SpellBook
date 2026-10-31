using UnityEngine;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// ScriptableObject que ouve um canal por uma resposta ou o guarda num campo comum, para o Find references (CA24).
    /// Fica em arquivo próprio para a Unity associar o script ao asset.
    /// </summary>
    public class TestListenerAsset : ScriptableObject
    {
        [SerializeReference] internal EventResponse response;
        [SerializeField] internal EventChannel channel;
    }
}
