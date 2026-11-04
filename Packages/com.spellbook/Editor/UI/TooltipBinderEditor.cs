using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.UI.Editor
{
    /// <summary>
    /// Inspector do <see cref="TooltipBinder"/> (Spec UI-3, Design 6; R13). A view e o tema ficam no Inspector padrão;
    /// acima dele, um aviso quando há mais de um <see cref="TooltipBinder"/> ativo nas cenas carregadas, porque todos
    /// disputam a mesma âncora. Na cena de prévia do Prefab Mode não avisa.
    /// </summary>
    [CustomEditor(typeof(TooltipBinder))]
    [CanEditMultipleObjects]
    public sealed class TooltipBinderEditor : SpellBookEditor
    {
        /// <summary>Nome do <see cref="HelpBox"/> do aviso de cena.</summary>
        public const string SceneWarningName = "spellbook-tooltip-binder-scene-warning";

        private HelpBox _sceneWarning;

        /// <summary>Refaz o aviso de cena.</summary>
        public void Refresh()
        {
            if (_sceneWarning == null) return;

            bool inScene = false;
            foreach (Object current in targets)
            {
                if (current is TooltipBinder binder && InEditedScene(binder.gameObject))
                {
                    inScene = true;
                    break;
                }
            }

            int count = inScene ? Object.FindObjectsByType<TooltipBinder>(FindObjectsSortMode.None).Length : 0;
            _sceneWarning.text = $"Há {count} TooltipBinder ativos nas cenas abertas; deixe um só, para que a âncora aponte para um tooltip.";
            _sceneWarning.style.display = count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <inheritdoc />
        protected override void BuildBeforeDefault(VisualElement root)
        {
            _sceneWarning = new HelpBox(string.Empty, HelpBoxMessageType.Warning) { name = SceneWarningName };
            root.Add(_sceneWarning);
            Refresh();
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.hierarchyChanged += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.hierarchyChanged -= Refresh);
        }

        private static bool InEditedScene(GameObject gameObject)
        {
            return gameObject.scene.IsValid() && gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(gameObject.scene);
        }
    }
}
