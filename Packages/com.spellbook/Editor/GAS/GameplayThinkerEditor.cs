using System.Globalization;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Thinkers;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Inspector do <see cref="GameplayThinker"/> (R22, Design 7): o padrão, com o cabeçalho da aura (efeito, raio,
    /// intervalo e filtro) antes do comportamento, e as abas das três listas de ações no fim.
    /// </summary>
    [CustomEditor(typeof(GameplayThinker))]
    [CanEditMultipleObjects]
    internal sealed class GameplayThinkerEditor : SpellBookEditor
    {
        internal const string HeaderName = "spellbook-thinker-behaviour-header";

        private Label _header;

        protected override void BuildAfterDefault(VisualElement root)
        {
            _header = new Label { name = HeaderName };
            PropertyField behaviour = root.Q<PropertyField>("PropertyField:behaviour");
            if (behaviour != null) behaviour.parent.Insert(behaviour.parent.IndexOf(behaviour), _header);
            else root.Add(_header);

            ActionEventsView.HideDefaultFields(root, typeof(GameplayThinker));
            root.Add(new ActionEventsView(serializedObject,
                "Este thinker ainda não tem ações. Adicione uma em \"Ao criar\" ou \"A cada pulso\"."));
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshHeader());
            RefreshHeader();
        }

        private void RefreshHeader()
        {
            string text = serializedObject.isEditingMultipleObjects ? null : AuraHeader();
            _header.text = text ?? string.Empty;
            _header.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Resumo da aura, raio e filtro do seletor e o intervalo do thinker; nulo sem aura
        private string AuraHeader()
        {
            serializedObject.Update();
            SerializedProperty behaviour = serializedObject.FindProperty("behaviour");
            if (behaviour == null || !(behaviour.managedReferenceValue is AuraBehaviour aura)) return null;

            SerializedProperty filter = serializedObject.FindProperty("behaviour.area.filter");
            string filterText = filter != null ? SummaryFormatter.Format(filter) : SummaryFormatter.None;
            var thinker = (GameplayThinker)target;
            return $"{SummaryFormatter.Format(behaviour)} · raio {Format(aura.Area?.Radius)} · " +
                $"intervalo {Format(thinker.PulseInterval)} s · filtro: {filterText}";
        }

        private static string Format(ScalableFloat value)
        {
            if (value == null) return SummaryFormatter.None;
            switch (value.Mode)
            {
                case ScalableFloat.ScalableMode.Constant:
                    return value.Evaluate(1).ToString("0.##", CultureInfo.CurrentCulture);
                case ScalableFloat.ScalableMode.Table:
                    return "%" + value.TableName;
                default:
                    return SummaryFormatter.PerLevel;
            }
        }
    }
}
