using System.Collections.Generic;
using UnityEngine;

namespace SpellBook.Core.Tests.Inspector
{
    /// <summary>
    /// Dono dos campos <c>[SerializeReference]</c> dos testes do seletor de tipo. Fica em arquivo próprio para a Unity
    /// associar o script e recarregar o asset salvo em disco.
    /// </summary>
    internal class PickHolder : ScriptableObject, ITestPick
    {
        [SerializeReference] internal PickBase single;
        [SerializeReference] internal List<PickBase> items = new List<PickBase>();
    }
}
