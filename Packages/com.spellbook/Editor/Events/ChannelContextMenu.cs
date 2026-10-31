using UnityEditor;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// "Find references" no menu de contexto do canal: no Inspector (engrenagem) e na janela Project (botão direito).
    /// Abre o Event Debugger com a lista de referências do canal (CA24).
    /// </summary>
    internal static class ChannelContextMenu
    {
        private const string ProjectMenu = "Assets/SpellBook/Find References";

        [MenuItem("CONTEXT/EventChannel/Find References")]
        private static void FromInspector(MenuCommand command)
        {
            EventDebuggerWindow.ShowReferences(command.context as EventChannel);
        }

        [MenuItem(ProjectMenu)]
        private static void FromProject()
        {
            EventDebuggerWindow.ShowReferences(Selection.activeObject as EventChannel);
        }

        [MenuItem(ProjectMenu, true)]
        private static bool FromProjectValidate()
        {
            return Selection.activeObject is EventChannel;
        }
    }
}
