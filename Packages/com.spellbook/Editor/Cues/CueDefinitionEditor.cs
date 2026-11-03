using SpellBook.Core.Editor;
using SpellBook.Core.Editor.Inspector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Cues.Editor
{
    /// <summary>
    /// Inspector da <see cref="CueDefinition"/>: o resumo de uma linha de cada item e de cada som antes dos campos, e o
    /// botão "Testar" depois, ativo só no Play.
    /// </summary>
    [CustomEditor(typeof(CueDefinition))]
    [CanEditMultipleObjects]
    internal sealed class CueDefinitionEditor : SpellBookEditor
    {
        internal const string LineClass = "spellbook-cue-line";
        internal const string TestButtonName = "spellbook-cue-test";

        private const long RefreshIntervalMs = 500;

        private readonly CueTester _tester = new CueTester();

        protected override void BuildBeforeDefault(VisualElement root)
        {
            var lines = new VisualElement();
            root.Add(lines);

            void Refresh()
            {
                lines.Clear();
                serializedObject.Update();
                AddLines(lines, serializedObject.FindProperty("items"));
                AddLines(lines, serializedObject.FindProperty("sounds"));
            }

            Refresh();
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
        }

        protected override void BuildAfterDefault(VisualElement root)
        {
            var test = new Button(Test)
            {
                name = TestButtonName,
                tooltip = "Toca o cue pelo primeiro CueRouter ativo, na seleção da cena ou na câmera principal (só no Play).",
            };
            root.Add(test);

            void Refresh()
            {
                test.SetEnabled(EditorApplication.isPlaying && targets.Length == 1);
                test.text = _tester.IsLooping ? "Parar" : "Testar";
            }

            void OnPlayModeChanged(PlayModeStateChange _) => Refresh();

            Refresh();
            root.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.playModeStateChanged += OnPlayModeChanged);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.playModeStateChanged -= OnPlayModeChanged);
        }

        private static void AddLines(VisualElement container, SerializedProperty list)
        {
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                var line = new Label(SummaryFormatter.Format(list.GetArrayElementAtIndex(i)));
                line.AddToClassList(LineClass);
                container.Add(line);
            }
        }

        private void Test()
        {
            CueRouter router = CueTester.FirstActiveRouter(
                Object.FindObjectsByType<CueRouter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            if (router == null)
            {
                Debug.LogWarning("Nenhum CueRouter ativo na cena para testar o cue.");
                return;
            }

            Camera main = Camera.main;
            _tester.Toggle((CueDefinition)target, router,
                CueTester.Point(Selection.activeTransform, main != null ? main.transform : null));
        }
    }

    /// <summary>
    /// Estado do botão "Testar": toca um <see cref="CueKind.Burst"/> por <see cref="CueRouter.Execute"/>; liga um
    /// <see cref="CueKind.Looping"/> por <see cref="CueRouter.Add"/> e o desliga no clique seguinte.
    /// </summary>
    internal sealed class CueTester
    {
        private CueRouter _router;
        private CueHandle _looping;

        /// <summary>Verdadeiro enquanto o Looping ligado pelo botão está ativo.</summary>
        internal bool IsLooping => _router != null && _router.IsActive(_looping);

        /// <summary>Toca, liga ou desliga o <paramref name="cue"/> pela tag dele. Falso se nada tocou nem parou.</summary>
        internal bool Toggle(CueDefinition cue, CueRouter router, in CueParameters parameters)
        {
            if (IsLooping)
            {
                bool removed = _router.Remove(_looping);
                _router = null;
                _looping = default;
                return removed;
            }

            if (cue.Kind == CueKind.Burst) return router.Execute(cue.Tag, parameters);

            _looping = router.Add(cue.Tag, parameters);
            _router = router;
            return _looping.IsValid;
        }

        /// <summary>O primeiro roteador ativo e habilitado da lista, ou <c>null</c>.</summary>
        internal static CueRouter FirstActiveRouter(CueRouter[] routers)
        {
            foreach (CueRouter router in routers)
            {
                if (router != null && router.isActiveAndEnabled) return router;
            }

            return null;
        }

        /// <summary>No objeto selecionado; sem seleção, na pose da câmera; sem as duas, na origem.</summary>
        internal static CueParameters Point(Transform selection, Transform camera)
        {
            if (selection != null) return CueParameters.On(selection);
            if (camera != null) return CueParameters.At(camera.position).WithRotation(camera.rotation);
            return CueParameters.At(Vector3.zero);
        }
    }
}
