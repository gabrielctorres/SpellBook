using System;
using System.Collections.Generic;
using SpellBook.Core.Editor;
using SpellBook.Tags;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpellBook.Cues.Editor
{
    /// <summary>Inspector da <see cref="CueLibrary"/>: busca por tag, com um botão por definição que casa.</summary>
    [CustomEditor(typeof(CueLibrary))]
    [CanEditMultipleObjects]
    internal sealed class CueLibraryEditor : SpellBookEditor
    {
        protected override void BuildBeforeDefault(VisualElement root)
        {
            var search = new TextField("Buscar tag") { tooltip = "Mostra as definições cuja tag contém o texto." };
            var results = new VisualElement();
            root.Add(search);
            root.Add(results);

            void Refresh()
            {
                results.Clear();
                if (string.IsNullOrEmpty(search.value)) return;

                IReadOnlyList<CueDefinition> cues = ((CueLibrary)target).Cues;
                for (int i = 0; i < cues.Count; i++)
                {
                    CueDefinition cue = cues[i];
                    GameplayTagDefinition tag = cue != null ? cue.Tag.GetDefinition() : null;
                    if (tag == null || tag.name.IndexOf(search.value, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    results.Add(new Button(() => Selection.activeObject = cue) { text = $"{tag.name} → {cue.name}" });
                }
            }

            search.RegisterValueChangedCallback(_ => Refresh());
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
        }
    }

    /// <summary>Inspector do <see cref="CueRouter"/>: o botão que põe o prefab de áudio do pacote.</summary>
    [CustomEditor(typeof(CueRouter))]
    [CanEditMultipleObjects]
    internal sealed class CueRouterEditor : SpellBookEditor
    {
        internal const string PackageAudioPrefabPath = "Packages/com.spellbook/Runtime/Cues/Prefabs/CueAudioSource.prefab";

        protected override void BuildAfterDefault(VisualElement root)
        {
            root.Add(new Button(() => UsePackageAudioPrefab(serializedObject))
            {
                text = "Usar prefab de áudio do pacote",
                tooltip = "Põe o CueAudioSource do pacote no prefab de áudio.",
            });
        }

        /// <summary>Põe o prefab de áudio do pacote no campo <c>audioPrefab</c>, com desfazer.</summary>
        internal static void UsePackageAudioPrefab(SerializedObject router)
        {
            router.Update();
            router.FindProperty("audioPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PackageAudioPrefabPath);
            router.ApplyModifiedProperties();
        }
    }

    [CustomEditor(typeof(CueRouterAnchor))]
    [CanEditMultipleObjects]
    internal sealed class CueRouterAnchorEditor : SpellBookEditor { }

    [CustomEditor(typeof(CueAudioPlayer))]
    [CanEditMultipleObjects]
    internal sealed class CueAudioPlayerEditor : SpellBookEditor { }
}
