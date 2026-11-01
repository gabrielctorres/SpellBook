using System.Collections.Generic;
using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Bridges.Editor
{
    /// <summary>
    /// Inspector do <see cref="AbilitySystemEventRelay"/> (Spec do relay, Design 5): os pares ficam em
    /// <see cref="Foldout"/>s fechados por grupo, antes do Inspector padrão, que mostra só o ASC; depois dele, em Play,
    /// os últimos disparos.
    /// </summary>
    [CustomEditor(typeof(AbilitySystemEventRelay))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySystemEventRelayEditor : SpellBookEditor
    {
        public const string GroupClass = "spellbook-relay-group";
        public const string RecentClass = "spellbook-relay-recent";
        private const long RecentRefreshMs = 250;

        private static readonly (string Title, string[] Paths)[] Groups =
        {
            ("Efeitos", new[] { "effectApplied", "effectRemoved", "effectStacksChanged", "effectBlocked" }),
            ("Habilidades", new[] { "abilityActivated", "abilityEnded", "abilityFailed", "abilityGranted", "abilityRemoved" }),
            ("Atributos", new[] { "attributeChanged", "attributeDecreased", "decreasedAttribute" }),
            ("Tags", new[] { "tagAdded", "tagRemoved" }),
            ("Bloqueio", new[] { "blocked" }),
        };

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = base.CreateInspectorGUI();
            RemoveDefaultPairFields(root);
            return root;
        }

        protected override void BuildBeforeDefault(VisualElement root)
        {
            foreach ((string title, string[] paths) in Groups)
            {
                var foldout = new Foldout { text = title, value = false };
                foldout.AddToClassList(GroupClass);
                foreach (string path in paths)
                {
                    foldout.Add(new PropertyField(serializedObject.FindProperty(path)));
                }

                root.Add(foldout);
            }
        }

        // Em Play, os últimos disparos do relay (par e quadro), do mais novo ao mais antigo; fora do Play, oculto.
        protected override void BuildAfterDefault(VisualElement root)
        {
            var recent = new VisualElement();
            recent.AddToClassList(RecentClass);
            recent.Add(new Label("Últimos disparos") { style = { unityFontStyleAndWeight = UnityEngine.FontStyle.Bold } });
            var lines = new VisualElement();
            recent.Add(lines);
            root.Add(recent);
            RefreshRecent(recent, lines);
            recent.schedule.Execute(() => RefreshRecent(recent, lines)).Every(RecentRefreshMs);
        }

        private void RefreshRecent(VisualElement recent, VisualElement lines)
        {
            var relay = target as AbilitySystemEventRelay;
            bool show = EditorApplication.isPlaying && targets.Length == 1 && relay != null;
            recent.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            lines.Clear();
            if (relay.RecentFiringCount == 0)
            {
                lines.Add(new Label("Nenhum disparo ainda."));
                return;
            }

            for (int i = relay.RecentFiringCount - 1; i >= 0; i--)
            {
                AbilitySystemEventRelay.RelayFiring firing = relay.GetRecentFiring(i);
                lines.Add(new Label($"Quadro {firing.Frame}: {ObjectNames.NicifyVariableName(firing.Pair)}"));
            }
        }

        // O Inspector padrão desenha todos os campos; os dos pares já estão nos grupos.
        private static void RemoveDefaultPairFields(VisualElement root)
        {
            var grouped = new HashSet<string>();
            foreach ((string _, string[] paths) in Groups)
            {
                grouped.UnionWith(paths);
            }

            List<PropertyField> fields = root.Query<PropertyField>().ToList();
            foreach (PropertyField field in fields)
            {
                Foldout group = field.GetFirstAncestorOfType<Foldout>();
                if (grouped.Contains(field.bindingPath) && (group == null || !group.ClassListContains(GroupClass)))
                {
                    field.RemoveFromHierarchy();
                }
            }
        }
    }
}
