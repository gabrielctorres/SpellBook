using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Inspector de todo <see cref="EventChannel"/> (R12). Depois do Inspector padrão: o botão "Raise", que dispara o
    /// <c>debugPayload</c> e só fica ativo no Play, o último payload e os ouvintes inscritos agora.
    /// </summary>
    [CustomEditor(typeof(EventChannel), true)]
    [CanEditMultipleObjects]
    public class EventChannelEditor : SpellBookEditor
    {
        public const string RaiseButtonName = "spellbook-channel-raise";

        private const long RefreshIntervalMs = 500;

        private readonly List<object> _listeners = new List<object>();

        protected override void BuildAfterDefault(VisualElement root)
        {
            var raise = new Button(RaiseAll) { name = RaiseButtonName, text = "Raise", tooltip = "Dispara o Debug Payload (só no Play)." };
            var lastPayload = new Label();
            var listeners = new Foldout { value = true };

            root.Add(raise);
            root.Add(lastPayload);
            root.Add(listeners);

            void Refresh()
            {
                raise.SetEnabled(EditorApplication.isPlaying);
                var channel = target as EventChannel;
                if (channel == null)
                {
                    return;
                }

                lastPayload.text = $"Último payload: {channel.BoxedLastPayload ?? "(nenhum)"}";
                channel.GetListeners(_listeners);
                listeners.text = $"Ouvintes ({_listeners.Count})";
                listeners.Clear();
                foreach (object listener in _listeners)
                {
                    listeners.Add(new Label(ListenerLabel.Describe(listener)));
                }
            }

            void OnPlayModeChanged(PlayModeStateChange _) => Refresh();

            Refresh();
            root.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayModeChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
        }

        private void RaiseAll()
        {
            foreach (Object item in targets)
            {
                (item as EventChannel)?.RaiseDebugPayload();
            }
        }
    }
}
