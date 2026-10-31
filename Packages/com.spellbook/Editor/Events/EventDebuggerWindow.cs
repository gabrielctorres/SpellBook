using System.Collections.Generic;
using SpellBook.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Editor
{
    /// <summary>
    /// Event Debugger (R20): lista os canais do projeto com os ouvintes vivos e o histórico de disparos com quadro,
    /// horário, payload e emissor. Busca por nome e botão para limpar. Só grava com a janela aberta, e o histórico
    /// recomeça a cada entrada no Play (I-d).
    /// </summary>
    internal sealed class EventDebuggerWindow : EditorWindow
    {
        private const long RefreshIntervalMs = 250;

        private readonly EventDebuggerModel _model = new EventDebuggerModel();
        private readonly List<EventChannel> _projectChannels = new List<EventChannel>();
        private readonly List<EventChannel> _channels = new List<EventChannel>();
        private readonly List<EventDebuggerModel.Entry> _entries = new List<EventDebuggerModel.Entry>();
        private readonly List<object> _listeners = new List<object>();
        private readonly List<string> _listenerNames = new List<string>();

        private string _search = "";
        private ListView _channelList;
        private ListView _historyList;
        private Label _historyHeader;

        [MenuItem("Window/SpellBook/Event Debugger")]
        public static void Open()
        {
            GetWindow<EventDebuggerWindow>("Event Debugger");
        }

        private void OnEnable()
        {
            _model.Start();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.projectChanged += ReloadChannels;
            ReloadChannels();
        }

        private void OnDisable()
        {
            _model.Stop();
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.projectChanged -= ReloadChannels;
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            var search = new ToolbarSearchField { tooltip = "Filtra canais e disparos pelo nome do canal." };
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue;
                Refresh();
            });
            toolbar.Add(search);
            toolbar.Add(new ToolbarButton(() =>
            {
                _model.Clear();
                Refresh();
            }) { text = "Limpar", tooltip = "Apaga o histórico de disparos." });
            toolbar.Add(new ToolbarButton(() => Application.OpenURL(DocUrls.EventDebugger)) { text = "?", tooltip = "Abre o manual do Event Debugger." });
            rootVisualElement.Add(toolbar);

            _channelList = new ListView
            {
                itemsSource = _channels,
                makeItem = MakeChannelRow,
                bindItem = BindChannelRow,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            _channelList.selectionChanged += _ => Ping(_channelList.selectedItem as Object);

            _historyHeader = new Label();
            _historyList = new ListView
            {
                itemsSource = _entries,
                makeItem = () => new Label(),
                bindItem = BindEntryRow,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            _historyList.selectionChanged += _ =>
            {
                if (_historyList.selectedItem is EventDebuggerModel.Entry entry)
                {
                    Ping(entry.Sender != null ? entry.Sender : entry.Channel);
                }
            };

            var split = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Vertical);
            split.Add(Section(new Label("Canais"), _channelList));
            split.Add(Section(_historyHeader, _historyList));
            rootVisualElement.Add(split);

            Refresh();
            rootVisualElement.schedule.Execute(Refresh).Every(RefreshIntervalMs);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                _model.Clear();
            }
        }

        // Assets de canal do projeto. Os canais criados em memória no Play entram pelo LiveChannels, no Refresh.
        private void ReloadChannels()
        {
            _projectChannels.Clear();
            _projectChannels.AddRange(ChannelCatalog.FindAll());
        }

        private void Refresh()
        {
            if (_channelList == null) return;

            _channels.Clear();
            AddMatching(_projectChannels);
            AddMatching(EventChannel.LiveChannels);
            _channelList.RefreshItems();

            _model.GetEntries(_search, _entries);
            _historyHeader.text = $"Disparos ({_model.Count} de {EventDebuggerModel.Capacity}, mais recente primeiro)";
            _historyList.RefreshItems();
        }

        private void AddMatching(IReadOnlyList<EventChannel> channels)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                EventChannel channel = channels[i];
                if (channel != null && !_channels.Contains(channel) && EventDebuggerModel.Matches(channel.name, _search))
                {
                    _channels.Add(channel);
                }
            }
        }

        private static VisualElement MakeChannelRow()
        {
            var row = new VisualElement();
            row.Add(new Label());
            var listeners = new Label();
            listeners.style.paddingLeft = 16;
            listeners.style.whiteSpace = WhiteSpace.Normal;
            row.Add(listeners);
            return row;
        }

        private void BindChannelRow(VisualElement row, int index)
        {
            EventChannel channel = _channels[index];
            channel.GetListeners(_listeners);
            ((Label)row[0]).text = $"{channel.name} ({channel.PayloadType.Name}): {_listeners.Count} ouvinte(s)";

            _listenerNames.Clear();
            foreach (object listener in _listeners)
            {
                _listenerNames.Add(ListenerLabel.Describe(listener));
            }

            var names = (Label)row[1];
            names.text = string.Join(", ", _listenerNames);
            names.style.display = _listenerNames.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BindEntryRow(VisualElement row, int index)
        {
            EventDebuggerModel.Entry entry = _entries[index];
            string payload = entry.Payload.Length > 0 ? $" = {entry.Payload}" : "";
            string sender = entry.SenderName.Length > 0 ? $"  por {entry.SenderName}" : "";
            ((Label)row).text = $"{entry.Time:HH:mm:ss.fff}  quadro {entry.Frame}  {entry.ChannelName}{payload}{sender}";
        }

        private static VisualElement Section(Label header, ListView list)
        {
            var section = new VisualElement();
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 4;
            list.style.flexGrow = 1;
            section.Add(header);
            section.Add(list);
            return section;
        }

        private static void Ping(Object target)
        {
            if (target != null)
            {
                EditorGUIUtility.PingObject(target);
            }
        }
    }
}
