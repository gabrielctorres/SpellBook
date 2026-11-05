using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SpellBook.UI.Editor
{
    /// <summary>
    /// Base dos Inspectors de binder (R16; ADR 0026, item 3a). Acima do Inspector padrão (resolvedor, fonte pelo seletor
    /// de tipos e view), mostra o resumo de uma linha da fonte, do limite visível (binder de lista) e do dono; a cada
    /// mudança, refaz o resumo e redesenha a prévia do binder.
    /// </summary>
    public abstract class UIBinderEditor : SpellBookEditor
    {
        /// <summary>Classe USS da linha de resumo.</summary>
        public const string SummaryClass = "spellbook-binder-summary";

        private Label _summary;

        /// <summary>Refaz o resumo e chama <see cref="UIBinderBase.Rebind"/> em cada alvo: em edição, redesenha a prévia.</summary>
        public void Refresh()
        {
            RefreshSummary();
            foreach (UnityEngine.Object current in targets)
            {
                if (current is UIBinderBase binder)
                {
                    binder.Rebind();
                }
            }
        }

        /// <inheritdoc />
        protected override void BuildBeforeDefault(VisualElement root)
        {
            _summary = new Label();
            _summary.AddToClassList(SummaryClass);
            root.Add(_summary);
            RefreshSummary();
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
        }

        private void RefreshSummary()
        {
            if (_summary == null)
            {
                return;
            }

            bool single = !serializedObject.isEditingMultipleObjects;
            _summary.style.display = single ? DisplayStyle.Flex : DisplayStyle.None;
            if (single)
            {
                serializedObject.Update();
                _summary.text = $"{SummaryFormatter.Format(serializedObject.FindProperty("source"))}{LimitText()} · dono: {OwnerText()}";
            }
        }

        // Binder de lista com limite visível: "· até N"; sem limite (0) ou sem o campo, nada
        private string LimitText()
        {
            SerializedProperty maxVisible = serializedObject.FindProperty("maxVisible");
            return maxVisible != null && maxVisible.intValue > 0 ? $" · até {maxVisible.intValue}" : string.Empty;
        }

        private string OwnerText()
        {
            switch ((ActorResolveMode)serializedObject.FindProperty("owner.mode").intValue)
            {
                case ActorResolveMode.FromAnchor:
                    return "âncora " + NameOf(serializedObject.FindProperty("owner.anchor"));
                case ActorResolveMode.Explicit:
                    return NameOf(serializedObject.FindProperty("owner.explicitOwner"));
                default:
                    return "no objeto ou pai";
            }
        }

        private static string NameOf(SerializedProperty reference)
        {
            return reference.objectReferenceValue != null ? reference.objectReferenceValue.name : SummaryFormatter.None;
        }
    }
}
