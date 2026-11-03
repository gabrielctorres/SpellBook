using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SpellBook.Core.Editor;
using SpellBook.Core.Validation;
using SpellBook.GAS.Actions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor.Actions
{
    /// <summary>
    /// Faixa de abas das listas <see cref="ActionEventAttribute"/> de um dono (DS do Inspector de ações, seção 3): uma aba
    /// por evento, com contador, ponto de validação e subtítulo do alvo padrão. A aba escolhida fica no
    /// <see cref="SessionState"/> por asset.
    /// </summary>
    internal sealed class ActionEventsView : VisualElement
    {
        internal const string StyleSheetPath = "Packages/com.spellbook/Editor/GAS/Actions/ActionInspector.uss";
        internal const string ClassName = "action-inspector";
        internal const string LightClass = "action-inspector--light";
        internal const string DotClass = "action-events__dot";
        internal const string DotWarningClass = "action-events__dot--warning";
        internal const string DotErrorClass = "action-events__dot--error";
        internal const string EmptyTabClass = "action-events__tab--empty";
        internal const string GroupLabelClass = "action-events__group";
        internal const string HiddenWarningClass = "action-events__hidden-warning";
        internal const string MultiSelectionText = "Edição múltipla não mostra ações";
        private const string ReactionsLabel = "Reações";
        private const string SessionPrefix = "SpellBook.ActionEvents.Tab.";

        private readonly SerializedObject _serializedObject;
        private readonly Func<string, bool> _isShown;
        private readonly ICollection<string> _reactions;
        private readonly List<(FieldInfo Field, ActionEventAttribute Event)> _events;
        private readonly List<EventTab> _tabs = new List<EventTab>();
        private readonly HelpBox _hiddenWarning;
        private readonly Label _emptyOwner;
        private readonly TabView _tabView;
        private string _shownKey;

        /// <param name="emptyText">Texto acima das abas quando o dono não tem nenhuma ação.</param>
        /// <param name="isShown">Se a aba do campo aparece; nula, aparecem todas.</param>
        /// <param name="reactions">Campos agrupados sob "Reações", depois das outras abas.</param>
        internal ActionEventsView(SerializedObject serializedObject, string emptyText, Func<string, bool> isShown = null,
            ICollection<string> reactions = null)
        {
            _serializedObject = serializedObject;
            _isShown = isShown ?? (_ => true);
            _reactions = reactions ?? Array.Empty<string>();
            AddToClassList(ClassName);
            EnableInClassList(LightClass, !EditorGUIUtility.isProSkin);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (sheet != null) styleSheets.Add(sheet);

            if (serializedObject.isEditingMultipleObjects)
            {
                _events = new List<(FieldInfo, ActionEventAttribute)>();
                Add(new Label(MultiSelectionText));
                return;
            }

            _events = FindEvents(serializedObject.targetObject.GetType());
            _hiddenWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning);
            _hiddenWarning.AddToClassList(HiddenWarningClass);
            _emptyOwner = new Label(emptyText);
            _tabView = new TabView();
            _tabView.activeTabChanged += (_, tab) => Remember(tab);
            Add(_hiddenWarning);
            Add(_emptyOwner);
            Add(_tabView);
            Refresh();
            this.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            RunningTracker = new RunningActionsTracker(this);
        }

        /// <summary>Indicador de "rodando" das linhas em Play (Design 10a); nulo em multi-seleção.</summary>
        internal RunningActionsTracker RunningTracker { get; }

        /// <summary>Campo da aba ativa, ou <c>null</c> sem abas.</summary>
        internal string ActiveEvent => _tabs.FirstOrDefault(t => t.Tab == _tabView?.activeTab)?.Field;

        /// <summary>Chave da aba lembrada do asset no <see cref="SessionState"/>.</summary>
        internal static string SessionKey(Object target)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long localId)
                ? $"{SessionPrefix}{guid}.{localId}"
                : $"{SessionPrefix}{target.GetHashCode()}";
        }

        /// <summary>Remove do Inspector padrão as listas que a faixa de abas mostra.</summary>
        internal static void HideDefaultFields(VisualElement root, Type ownerType)
        {
            foreach ((FieldInfo field, _) in FindEvents(ownerType))
            {
                root.Q<PropertyField>("PropertyField:" + field.Name)?.RemoveFromHierarchy();
            }
        }

        /// <summary>Ativa a aba do campo e a lembra para o asset.</summary>
        internal void Select(string field)
        {
            EventTab tab = _tabs.FirstOrDefault(t => t.Field == field);
            if (tab == null) return;
            _tabView.activeTab = tab.Tab;
            Remember(tab.Tab);
        }

        /// <summary>Refaz as abas se as visíveis mudaram, e o contador, a validação e as linhas de cada uma.</summary>
        internal void Refresh()
        {
            if (_tabView == null) return;
            _serializedObject.Update();
            List<(FieldInfo Field, ActionEventAttribute Event)> shown = _events.Where(e => _isShown(e.Field.Name)).ToList();
            string key = string.Join(",", shown.Select(e => e.Field.Name));
            if (key != _shownKey)
            {
                _shownKey = key;
                Rebuild(shown);
            }

            IReadOnlyList<ValidationMessage> messages = SpellBookEditor.CollectMessages(_serializedObject.targetObjects);
            foreach (EventTab tab in _tabs) tab.Refresh(Count(tab.Field), messages);

            var hidden = _events.Where(e => !_isShown(e.Field.Name) && Count(e.Field.Name) > 0).ToList();
            _hiddenWarning.text = string.Join("\n", hidden.Select(e =>
                $"{Count(e.Field.Name)} ações em \"{e.Event.Label}\" não vão rodar com o comportamento atual."));
            Show(_hiddenWarning, hidden.Count > 0);
            Show(_emptyOwner, _events.All(e => Count(e.Field.Name) == 0));
        }

        private void Rebuild(List<(FieldInfo Field, ActionEventAttribute Event)> shown)
        {
            _tabView.Clear();
            _tabs.Clear();
            bool grouped = false;
            foreach ((FieldInfo field, ActionEventAttribute ev) in shown.OrderBy(e => _reactions.Contains(e.Field.Name)))
            {
                var tab = new EventTab(_serializedObject.FindProperty(field.Name), field.Name, ev);
                _tabView.Add(tab.Tab);
                _tabs.Add(tab);
                if (!grouped && _reactions.Contains(field.Name))
                {
                    var group = new Label(ReactionsLabel);
                    group.AddToClassList(GroupLabelClass);
                    tab.Tab.tabHeader.Insert(0, group);
                    grouped = true;
                }
            }

            string remembered = SessionState.GetString(SessionKey(_serializedObject.targetObject), null);
            EventTab active = _tabs.FirstOrDefault(t => t.Field == remembered)
                ?? _tabs.FirstOrDefault(t => Count(t.Field) > 0) ?? _tabs.FirstOrDefault();
            if (active != null) _tabView.activeTab = active.Tab;
        }

        private void Remember(Tab tab)
        {
            string field = _tabs.FirstOrDefault(t => t.Tab == tab)?.Field;
            if (field != null) SessionState.SetString(SessionKey(_serializedObject.targetObject), field);
        }

        private int Count(string field)
        {
            SerializedProperty list = _serializedObject.FindProperty(field);
            return list != null && list.isArray ? list.arraySize : 0;
        }

        private static List<(FieldInfo, ActionEventAttribute)> FindEvents(Type ownerType)
        {
            return ownerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(f => (Field: f, Event: f.GetCustomAttribute<ActionEventAttribute>()))
                .Where(e => e.Event != null)
                .OrderBy(e => e.Field.MetadataToken)
                .ToList();
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Uma aba: rótulo com contador, ponto de validação, subtítulo e a lista do evento.</summary>
        private sealed class EventTab
        {
            internal readonly Tab Tab;
            internal readonly string Field;
            private readonly ActionEventAttribute _event;
            private readonly Label _dot;
            private readonly ActionListView _list;

            internal EventTab(SerializedProperty list, string field, ActionEventAttribute ev)
            {
                Field = field;
                _event = ev;
                Tab = new Tab(ev.Label);
                _dot = new Label();
                _dot.AddToClassList(DotClass);
                Tab.tabHeader.Add(_dot);
                string target = ev.DefaultTarget == DefaultTarget.Owner ? "o dono" : "os alvos do evento";
                Tab.Add(new Label($"Alvo padrão: {target}. Roda de cima para baixo.") { name = "action-events__subtitle" });
                _list = new ActionListView(list);
                Tab.Add(_list);
            }

            internal void Refresh(int count, IReadOnlyList<ValidationMessage> messages)
            {
                Tab.label = count > 0 ? $"{_event.Label} · {count}" : _event.Label;
                Tab.tabHeader.EnableInClassList(EmptyTabClass, count == 0);
                List<ValidationMessage> own = messages.Where(m => ActionRowView.IsUnder(m.PropertyPath, Field)).ToList();
                _dot.text = own.Count.ToString();
                bool error = own.Any(m => m.Severity == ValidationSeverity.Error);
                _dot.EnableInClassList(DotErrorClass, error);
                _dot.EnableInClassList(DotWarningClass, own.Count > 0 && !error);
                _dot.tooltip = string.Join("\n", own.Select(m => m.Message));
                Show(_dot, own.Count > 0);
                _list.Refresh(messages);
            }
        }
    }
}
