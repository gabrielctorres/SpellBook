using System.Collections.Generic;
using System.Globalization;
using SpellBook.Core;
using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using SpellBook.Core.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

namespace SpellBook.UI.UGUI.Editor
{
    /// <summary>
    /// Inspector do <see cref="TooltipTrigger"/> (Spec UI-3, Design 6, P6; R12 e R13). Acima do Inspector padrão (fonte
    /// pelo seletor de tipos, resolvedor, modo, atraso, âncora e alvo), mostra o resumo de uma linha, o aviso de modo
    /// ponteiro sem <see cref="EventSystem"/> nas cenas carregadas e a prévia do tooltip por nível, em texto simples com a
    /// cor do papel. O resumo e a prévia aparecem só com um alvo.
    /// </summary>
    [CustomEditor(typeof(TooltipTrigger))]
    [CanEditMultipleObjects]
    public sealed class TooltipTriggerEditor : SpellBookEditor
    {
        /// <summary>Nome do <see cref="Label"/> do resumo.</summary>
        public const string SummaryName = "spellbook-tooltip-summary";

        /// <summary>Nome do <see cref="HelpBox"/> do aviso de cena.</summary>
        public const string SceneWarningName = "spellbook-tooltip-scene-warning";

        /// <summary>Nome do campo do nível da prévia.</summary>
        public const string PreviewLevelName = "spellbook-tooltip-preview-level";

        /// <summary>Nome do contêiner das linhas da prévia.</summary>
        public const string PreviewName = "spellbook-tooltip-preview";

        /// <summary>Classe USS das linhas do modelo na prévia.</summary>
        public const string PreviewLineClass = "spellbook-tooltip-preview-line";

        /// <summary>Texto da prévia sem fonte ou com modelo vazio.</summary>
        public const string NoPreviewText = "Sem prévia: escolha uma fonte com assunto configurado.";

        private Label _summary;
        private HelpBox _sceneWarning;
        private IntegerField _level;
        private VisualElement _preview;

        /// <summary>Nível em que a prévia monta o modelo; nunca menor que 1.</summary>
        public int PreviewLevel { get; private set; } = 1;

        /// <summary>Muda o nível da prévia (menor que 1 vale 1) e refaz a prévia.</summary>
        public void SetPreviewLevel(int level)
        {
            PreviewLevel = Mathf.Max(1, level);
            _level?.SetValueWithoutNotify(PreviewLevel);
            RefreshPreview();
        }

        /// <summary>Refaz o resumo, o aviso de cena e a prévia pela configuração atual.</summary>
        public void Refresh()
        {
            serializedObject.Update();
            RefreshSummary();
            RefreshSceneWarning();
            RefreshPreview();
        }

        /// <inheritdoc />
        protected override void BuildBeforeDefault(VisualElement root)
        {
            _summary = new Label { name = SummaryName };
            root.Add(_summary);
            _sceneWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning) { name = SceneWarningName };
            root.Add(_sceneWarning);
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.hierarchyChanged += RefreshSceneWarning);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.hierarchyChanged -= RefreshSceneWarning);
        }

        /// <inheritdoc />
        protected override void BuildAfterDefault(VisualElement root)
        {
            _level = new IntegerField("Nível de prévia") { name = PreviewLevelName, value = PreviewLevel };
            _level.RegisterValueChangedCallback(evt => SetPreviewLevel(evt.newValue));
            root.Add(_level);
            _preview = new VisualElement { name = PreviewName };
            root.Add(_preview);
            Refresh();
        }

        private bool Single => !serializedObject.isEditingMultipleObjects;

        private void RefreshSummary()
        {
            if (_summary == null) return;

            Show(_summary, Single);
            if (!Single) return;

            string source = SummaryFormatter.Format(serializedObject.FindProperty("source"));
            var mode = (TooltipOpenMode)serializedObject.FindProperty("mode").enumValueIndex;
            string delay = serializedObject.FindProperty("delay").doubleValue.ToString("0.##", CultureInfo.CurrentCulture);
            _summary.text = $"Tooltip · {source} · {ModeText(mode)}, {delay} s";
        }

        private void RefreshSceneWarning()
        {
            if (_sceneWarning == null) return;

            bool warn = false;
            foreach (Object current in targets)
            {
                if (current is TooltipTrigger trigger && UsesPointer(trigger.Mode) && InEditedScene(trigger.gameObject))
                {
                    warn = true;
                    break;
                }
            }

            warn = warn && Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0;
            _sceneWarning.text = "O modo abre pelo ponteiro, mas não há EventSystem ativo nas cenas abertas: o tooltip não abre com o mouse.";
            Show(_sceneWarning, warn);
        }

        private void RefreshPreview()
        {
            if (_preview == null) return;

            Show(_level, Single);
            Show(_preview, Single);
            _preview.Clear();
            if (!Single) return;

            var trigger = target as TooltipTrigger;
            UISource<TooltipModel> source = trigger != null ? trigger.Source : null;
            TooltipModel model = source != null
                ? source.CreatePreview(new UIPreviewContext(PreviewLevel, null, CultureInfo.CurrentCulture))
                : default;
            if (model.IsEmpty)
            {
                AddText(NoPreviewText, null);
                return;
            }

            AddText(model.Title, null);
            AddText(model.Subtitle, null);
            IReadOnlyList<TooltipLine> lines = model.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                TooltipLine line = lines[i];
                string value = SpanText(line.Value);
                string text = string.IsNullOrEmpty(line.Label) ? value : string.IsNullOrEmpty(value) ? line.Label : line.Label + " " + value;
                Label label = AddText(text, PreviewLineClass);
                if (label != null) label.style.color = TextRoleTheme.Default.GetColor(line.Role);
            }

            if (model.HasStacks) AddText("Pilhas: " + model.Stacks.ToString(CultureInfo.CurrentCulture), null);
            if (model.HasRemaining) AddText(SpanText(model.RemainingText), null);
            AddText(model.Footer, null);
        }

        private Label AddText(string text, string className)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var label = new Label(text) { enableRichText = false };
            if (className != null) label.AddToClassList(className);
            _preview.Add(label);
            return label;
        }

        private static string SpanText(TextSpan span)
        {
            return span.IsEmpty ? string.Empty : span.Buffer.ToString(span);
        }

        private static bool UsesPointer(TooltipOpenMode mode)
        {
            return mode == TooltipOpenMode.Pointer || mode == TooltipOpenMode.PointerAndFocus;
        }

        /// <summary>Verdadeiro para um objeto numa cena carregada que não é a de prévia do Prefab Mode.</summary>
        private static bool InEditedScene(GameObject gameObject)
        {
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(gameObject.scene);
        }

        private static string ModeText(TooltipOpenMode mode)
        {
            switch (mode)
            {
                case TooltipOpenMode.Pointer: return "ponteiro";
                case TooltipOpenMode.Focus: return "foco";
                case TooltipOpenMode.Code: return "código";
                default: return "ponteiro e foco";
            }
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
