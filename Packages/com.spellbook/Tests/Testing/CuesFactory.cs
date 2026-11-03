#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.Cues;
using SpellBook.Tags;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.Testing
{
    /// <summary>
    /// Cria objetos do Cues para testes sem nenhum asset em disco. Campos privados serializados são preenchidos por
    /// <c>SerializedObject</c>, como o Inspector faria. Tudo o que a factory cria é destruído no <see cref="Dispose"/>.
    /// Só compila no Editor (SerializedObject).
    /// </summary>
    public sealed class CuesFactory : IDisposable
    {
        private readonly List<Object> _created = new List<Object>();

        /// <summary>Tag nova, filha de <paramref name="parent"/> se houver.</summary>
        public GameplayTag CreateTag(string name, GameplayTag parent = default)
        {
            var definition = Track(ScriptableObject.CreateInstance<GameplayTagDefinition>(), name);
            Modify(definition, so => so.FindProperty("parent").objectReferenceValue = parent.GetDefinition());
            return new GameplayTag(definition);
        }

        /// <summary>Definição de cue com os itens dados, sem limite de cópias.</summary>
        public CueDefinition CreateCue(GameplayTag tag, CueKind kind, params CueItem[] items)
        {
            return CreateCue(tag, kind, 0, CueLimitPolicy.DropNew, items);
        }

        /// <summary>Definição de cue com os itens e o limite de cópias dados.</summary>
        public CueDefinition CreateCue(
            GameplayTag tag, CueKind kind, int maxSimultaneous, CueLimitPolicy policy, params CueItem[] items)
        {
            var cue = Track(ScriptableObject.CreateInstance<CueDefinition>(), "Cue_" + tag.Name);
            Modify(cue, so =>
            {
                so.FindProperty("tag.definition").objectReferenceValue = tag.GetDefinition();
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.FindProperty("maxSimultaneous").intValue = maxSimultaneous;
                so.FindProperty("limitPolicy").enumValueIndex = (int)policy;
                SerializedProperty list = so.FindProperty("items");
                list.arraySize = items.Length;
                for (int i = 0; i < items.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = items[i];
                }
            });
            return cue;
        }

        public CueRouterAnchor CreateAnchor()
        {
            return Track(ScriptableObject.CreateInstance<CueRouterAnchor>(), "CueRouterAnchor_Test");
        }

        /// <summary>Spawner com pool que registra criações e devoluções.</summary>
        public RecordingSpawnerAsset CreateSpawner()
        {
            return Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>(), "RecordingSpawner_Test");
        }

        /// <summary>
        /// Roteador com uma biblioteca das <paramref name="cues"/>, o <paramref name="time"/> como fonte de tempo, o
        /// <paramref name="spawner"/> como padrão e a <paramref name="anchor"/>, se houver.
        /// </summary>
        public CueRouter CreateRouter(
            ManualTimeSource time, SpawnerAsset spawner, CueRouterAnchor anchor, params CueDefinition[] cues)
        {
            var library = Track(ScriptableObject.CreateInstance<CueLibrary>(), "CueLibrary_Test");
            Modify(library, so =>
            {
                SerializedProperty list = so.FindProperty("cues");
                list.arraySize = cues.Length;
                for (int i = 0; i < cues.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = cues[i];
                }
            });

            // Inativo até ter os campos: no PlayMode, o AddComponent chamaria o Awake sem as bibliotecas
            GameObject host = Track(new GameObject(), "CueRouter_Test");
            host.SetActive(false);
            var router = host.AddComponent<CueRouter>();
            Modify(router, so =>
            {
                SerializedProperty libraries = so.FindProperty("libraries");
                libraries.arraySize = 1;
                libraries.GetArrayElementAtIndex(0).objectReferenceValue = library;
                so.FindProperty("timeSource").objectReferenceValue = time;
                so.FindProperty("defaultSpawner").objectReferenceValue = spawner;
                so.FindProperty("anchor").objectReferenceValue = anchor;
            });
            host.SetActive(true);
            return router;
        }

        /// <summary>Põe os <paramref name="sounds"/> na <paramref name="cue"/>, com a seleção dada.</summary>
        public void SetSounds(CueDefinition cue, SoundSelection selection, params SoundCueItem[] sounds)
        {
            Modify(cue, so =>
            {
                so.FindProperty("soundSelection").enumValueIndex = (int)selection;
                SerializedProperty list = so.FindProperty("sounds");
                list.arraySize = sounds.Length;
                for (int i = 0; i < sounds.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = sounds[i];
                }
            });
        }

        /// <summary>Troca o prefab de áudio do <paramref name="router"/>.</summary>
        public void SetAudioPrefab(CueRouter router, GameObject prefab)
        {
            Modify(router, so => so.FindProperty("audioPrefab").objectReferenceValue = prefab);
        }

        /// <summary>Prefab de áudio de teste: <see cref="AudioSource"/> sem tocar ao nascer e <see cref="CueAudioPlayer"/>.</summary>
        public GameObject CreateAudioPrefab()
        {
            GameObject prefab = Track(new GameObject(), "CueAudioPrefab_Test");
            prefab.AddComponent<AudioSource>().playOnAwake = false;
            prefab.AddComponent<CueAudioPlayer>();
            return prefab;
        }

        /// <summary>Prefab de áudio que o pacote entrega.</summary>
        public static GameObject LoadPackageAudioPrefab()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(
                "Packages/com.spellbook/Runtime/Cues/Prefabs/CueAudioSource.prefab");
        }

        /// <summary>Clipe mono de <paramref name="seconds"/> segundos a 44,1 kHz, silencioso salvo com <paramref name="tone"/>.</summary>
        public AudioClip CreateClip(string name, float seconds, bool tone = false)
        {
            const int frequency = 44100;
            int samples = Mathf.RoundToInt(seconds * frequency);
            AudioClip clip = Track(AudioClip.Create(name, samples, 1, frequency, false), name);
            if (tone)
            {
                var data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    data[i] = 0.1f * Mathf.Sin(2f * Mathf.PI * 440f * i / frequency);
                }

                clip.SetData(data, 0);
            }

            return clip;
        }

        /// <summary>Objeto de cena vazio, destruído no <see cref="Dispose"/>.</summary>
        public GameObject CreateObject(string name)
        {
            return Track(new GameObject(), name);
        }

        public void Dispose()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_created[i]);
                else Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        private T Track<T>(T obj, string name) where T : Object
        {
            obj.name = name;
            _created.Add(obj);
            return obj;
        }

        private static void Modify(Object target, Action<SerializedObject> edit)
        {
            using (var so = new SerializedObject(target))
            {
                edit(so);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
