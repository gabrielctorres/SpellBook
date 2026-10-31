using SpellBook.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>Inspector do <see cref="EventEmitter"/> (R19): depois do Inspector padrão, o botão "Raise", ativo só no Play.</summary>
    [CustomEditor(typeof(EventEmitter))]
    [CanEditMultipleObjects]
    public class EventEmitterEditor : SpellBookEditor
    {
        public const string RaiseButtonName = "spellbook-emitter-raise";

        protected override void BuildAfterDefault(VisualElement root)
        {
            var raise = new Button(RaiseAll) { name = RaiseButtonName, text = "Raise", tooltip = "Chama Raise() do emissor (só no Play)." };
            root.Add(raise);

            void Refresh(PlayModeStateChange _) => raise.SetEnabled(EditorApplication.isPlaying);

            raise.SetEnabled(EditorApplication.isPlaying);
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= Refresh);
        }

        private void RaiseAll()
        {
            foreach (Object item in targets)
            {
                (item as EventEmitter)?.Raise();
            }
        }
    }
}
