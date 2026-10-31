using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Nome legível de um ouvinte de canal, usado pelo Event Debugger e pelo Inspector do canal (CA23). O canal lista o
    /// alvo do delegate; para uma <see cref="EventResponse"/>, o nome mostrado é o do <see cref="EventListener"/> dono.
    /// </summary>
    internal static class ListenerLabel
    {
        /// <summary>
        /// "nome (Tipo)" para um objeto da Unity ou para o dono de uma resposta, o nome curto do tipo para uma resposta
        /// sem dono, "(destruído)", "(nulo)" ou o <c>ToString()</c> de outro alvo.
        /// </summary>
        public static string Describe(object target)
        {
            if (target is EventResponse response)
            {
                EventListener owner = response.Owner;
                return owner is null ? response.GetType().Name : Describe(owner);
            }

            if (target is Object unityObject)
            {
                return unityObject != null ? $"{unityObject.name} ({unityObject.GetType().Name})" : "(destruído)";
            }

            return target?.ToString() ?? "(nulo)";
        }
    }
}
