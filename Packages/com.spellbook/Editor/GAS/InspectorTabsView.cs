using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Faixa de abas do topo dos Inspectors do GAS (DS do Inspector da habilidade, seção 2). As abas vêm do dono,
    /// os campos do Inspector padrão mudam de lugar para os grupos de cada aba, e a aba ativa fica no
    /// <see cref="SessionState"/> por asset. Um asset sem aba lembrada abre na primeira.
    /// </summary>
    internal sealed class InspectorTabsView : VisualElement
    {
        internal const string ClassName = "spellbook-inspector-tabs";
        internal const string GroupClass = "spellbook-inspector-tabs__group";
        internal const string GroupTitleClass = "spellbook-inspector-tabs__group-title";
        internal const string OthersGroupClass = "spellbook-inspector-tabs__group--others";
        internal const string OthersTitle = "Outros";
        private const string FieldPrefix = "PropertyField:";
        private const string SessionPrefix = "SpellBook.InspectorTabs.Tab.";

        private readonly Object _target;
        private readonly TabView _tabView = new TabView();
        private readonly List<Tab> _tabs = new List<Tab>();

        /// <param name="target">Asset cuja aba é lembrada; em multi-seleção, o primeiro.</param>
        /// <param name="labels">Nomes das abas, na ordem.</param>
        internal InspectorTabsView(Object target, params string[] labels)
        {
            _target = target;
            AddToClassList(ClassName);
            foreach (string label in labels)
            {
                var tab = new Tab(label);
                _tabs.Add(tab);
                _tabView.Add(tab);
            }

            Add(_tabView);
            Tab remembered = Find(SessionState.GetString(SessionKey(target), null)) ?? _tabs.FirstOrDefault();
            if (remembered != null) _tabView.activeTab = remembered;
            _tabView.activeTabChanged += (_, tab) => Remember(tab);
        }

        /// <summary>Nome da aba ativa.</summary>
        internal string ActiveTab => _tabView.activeTab?.label;

        /// <summary>Conteúdo da aba pelo nome, ou <c>null</c>.</summary>
        internal VisualElement Content(string label) => Find(label);

        /// <summary>Ativa a aba pelo nome e a lembra para o asset.</summary>
        internal void Select(string label)
        {
            Tab tab = Find(label);
            if (tab == null) return;
            _tabView.activeTab = tab;
            Remember(tab);
        }

        /// <summary>Chave da aba lembrada do asset no <see cref="SessionState"/>.</summary>
        internal static string SessionKey(Object target)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string guid, out long localId)
                ? $"{SessionPrefix}{guid}.{localId}"
                : $"{SessionPrefix}{target.GetHashCode()}";
        }

        /// <summary>Grupo com título em negrito, sem dobra, no fim de <paramref name="parent"/>.</summary>
        internal static VisualElement AddGroup(VisualElement parent, string title)
        {
            var group = new VisualElement();
            group.AddToClassList(GroupClass);
            var label = new Label(title);
            label.AddToClassList(GroupTitleClass);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            label.style.marginBottom = 2;
            group.Add(label);
            parent.Add(group);
            return group;
        }

        /// <summary>Move os campos do Inspector padrão, na ordem dada, para o fim do grupo.</summary>
        internal static void MoveFields(VisualElement root, VisualElement group, params string[] properties)
        {
            foreach (string property in properties)
            {
                PropertyField field = root.Q<PropertyField>(FieldPrefix + property);
                if (field != null) group.Add(field);
            }
        }

        /// <summary>
        /// Move os campos que ficaram no Inspector padrão, menos os de <paramref name="kept"/>, para um grupo "Outros"
        /// no fim de <paramref name="tab"/>. Sem campo sobrando, o grupo não é criado.
        /// </summary>
        internal static void MoveRemaining(VisualElement root, VisualElement tab, params string[] kept)
        {
            List<PropertyField> rest = root.Children().OfType<PropertyField>()
                .Where(f => f.name.StartsWith(FieldPrefix) && !kept.Contains(f.name.Substring(FieldPrefix.Length)))
                .ToList();
            if (rest.Count == 0) return;

            VisualElement group = AddGroup(tab, OthersTitle);
            group.AddToClassList(OthersGroupClass);
            foreach (PropertyField field in rest) group.Add(field);
        }

        private Tab Find(string label)
        {
            return label == null ? null : _tabs.FirstOrDefault(t => t.label == label);
        }

        private void Remember(Tab tab)
        {
            if (tab != null && _tabs.Contains(tab)) SessionState.SetString(SessionKey(_target), tab.label);
        }
    }
}
