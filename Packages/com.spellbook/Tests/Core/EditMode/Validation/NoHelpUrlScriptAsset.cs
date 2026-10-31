using UnityEngine;

namespace SpellBook.Core.Tests.Validation
{
    /// <summary>
    /// SO público e concreto sem <c>[HelpURL]</c> para os testes de <c>ProjectValidator.ValidateTypes</c>. Fica em
    /// arquivo próprio para a Unity associar o <c>MonoScript</c>, que é o alvo do erro.
    /// </summary>
    public class NoHelpUrlScriptAsset : ScriptableObject
    {
    }
}
