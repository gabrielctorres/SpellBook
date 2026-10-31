using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Events.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpellBook.Events.Tests
{
    /// <summary>
    /// Find references de canal (CA24): varre assets e cenas abertas, com o papel de cada referência. Os assets e a cena
    /// fechada ficam numa pasta temporária apagada no TearDown, e as cenas abertas não mudam (risco 23).
    /// </summary>
    public class FindReferencesTests
    {
        private const string Folder = "Assets/__SpellBookTestTemp";
        private const string ChannelPath = Folder + "/Channel.asset";

        private readonly List<Object> _sceneObjects = new List<Object>();
        private VoidEventChannel _channel;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__SpellBookTestTemp");
            }

            _channel = ScriptableObject.CreateInstance<VoidEventChannel>();
            AssetDatabase.CreateAsset(_channel, ChannelPath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _sceneObjects.Where(o => o != null))
            {
                Object.DestroyImmediate(created);
            }

            _sceneObjects.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void Find_PrefabEmitterAndSoListener_ExactlyTwo_WithRoles()
        {
            const string prefabPath = Folder + "/Botao.prefab";
            const string listenerPath = Folder + "/Ouvinte.asset";
            var button = new GameObject("Botao");
            SetEmission(button.AddComponent<EventEmitter>(), _channel);
            PrefabUtility.SaveAsPrefabAsset(button, prefabPath);
            Object.DestroyImmediate(button);
            var listener = ScriptableObject.CreateInstance<TestListenerAsset>();
            listener.response = new VoidEventResponse();
            SetChannel(listener, "response.channel", _channel);
            AssetDatabase.CreateAsset(listener, listenerPath);
            AssetDatabase.SaveAssets();

            List<ChannelReference> references = ChannelReferenceFinder.Find(_channel);

            Assert.That(references, Has.Count.EqualTo(2));
            ChannelReference emits = references.Single(r => r.Role == ChannelReferenceRole.Emits);
            Assert.That(emits.Target, Is.InstanceOf<EventEmitter>());
            Assert.That(emits.Location, Is.EqualTo(prefabPath));
            ChannelReference listens = references.Single(r => r.Role == ChannelReferenceRole.Listens);
            Assert.That(listens.Target, Is.InstanceOf<TestListenerAsset>());
            Assert.That(listens.Location, Is.EqualTo(listenerPath));
        }

        [Test]
        public void Find_ClosedSceneListener_NotIncluded()
        {
            const string scenePath = Folder + "/Fechada.unity";
            // Cópia da cena aberta com o ouvinte, salva no disco e não aberta; depois o ouvinte sai da cena aberta. O
            // NewScene aditivo falha com uma cena sem título aberta, como a do Test Runner.
            var panel = new GameObject("Painel");
            _sceneObjects.Add(panel);
            AddListener(panel, _channel);
            Assert.That(EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath, true), Is.True);
            Object.DestroyImmediate(panel);
            Assert.That(AssetDatabase.GetDependencies(scenePath, false), Does.Contain(ChannelPath), "A cena fechada não referencia o canal.");

            List<ChannelReference> references = ChannelReferenceFinder.Find(_channel);

            Assert.That(references, Is.Empty);
        }

        [Test]
        public void Find_OpenSceneListener_IsListens()
        {
            var panel = new GameObject("Painel");
            _sceneObjects.Add(panel);
            EventListener listener = AddListener(panel, _channel);

            List<ChannelReference> references = ChannelReferenceFinder.Find(_channel);

            Assert.That(references, Has.Count.EqualTo(1));
            Assert.That(references[0].Target, Is.SameAs(listener));
            Assert.That(references[0].Role, Is.EqualTo(ChannelReferenceRole.Listens));
            Assert.That(references[0].Location, Is.EqualTo(panel.scene.path.Length > 0 ? panel.scene.path : panel.scene.name));
        }

        [Test]
        public void Find_RaiseChannelReactionTarget_IsEmits()
        {
            const string assetPath = Folder + "/Repassa.asset";
            var other = ScriptableObject.CreateInstance<VoidEventChannel>();
            AssetDatabase.CreateAsset(other, Folder + "/Outro.asset");
            var relay = ScriptableObject.CreateInstance<TestListenerAsset>();
            relay.response = new VoidEventResponse();
            AssetDatabase.CreateAsset(relay, assetPath);
            SetChannel(relay, "response.channel", other);
            AddRaiseChannel(relay, "response.reactions", _channel);
            AssetDatabase.SaveAssets();

            List<ChannelReference> references = ChannelReferenceFinder.Find(_channel);

            Assert.That(references, Has.Count.EqualTo(1));
            Assert.That(references[0].Target, Is.SameAs(relay));
            Assert.That(references[0].Role, Is.EqualTo(ChannelReferenceRole.Emits));
            Assert.That(references[0].Location, Is.EqualTo(assetPath));
        }

        [Test]
        public void Find_PlainChannelField_IsReferences()
        {
            const string assetPath = Folder + "/Guarda.asset";
            var holder = ScriptableObject.CreateInstance<TestListenerAsset>();
            AssetDatabase.CreateAsset(holder, assetPath);
            SetChannel(holder, "channel", _channel);
            AssetDatabase.SaveAssets();

            List<ChannelReference> references = ChannelReferenceFinder.Find(_channel);

            Assert.That(references, Has.Count.EqualTo(1));
            Assert.That(references[0].Target, Is.SameAs(holder));
            Assert.That(references[0].Role, Is.EqualTo(ChannelReferenceRole.References));
            Assert.That(references[0].Location, Is.EqualTo(assetPath));
        }

        [Test]
        public void Reveal_SelectsTarget()
        {
            Object previous = Selection.activeObject;
            try
            {
                Selection.activeObject = null;

                EventDebuggerWindow.Reveal(_channel);

                Assert.That(Selection.activeObject, Is.SameAs(_channel));
            }
            finally
            {
                Selection.activeObject = previous;
            }
        }

        private static void AddRaiseChannel(Object owner, string reactionsPath, EventChannel target)
        {
            var serialized = new SerializedObject(owner);
            SerializedProperty reactions = serialized.FindProperty(reactionsPath);
            reactions.arraySize = 1;
            SerializedProperty element = reactions.GetArrayElementAtIndex(0);
            element.managedReferenceValue = new RaiseChannelReaction();
            element.FindPropertyRelative("mode").intValue = (int)RaiseChannelReaction.Mode.RaiseVoid;
            element.FindPropertyRelative("target").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEmission(EventEmitter emitter, EventChannel channel)
        {
            var serialized = new SerializedObject(emitter);
            serialized.FindProperty("emission").managedReferenceValue = EventEmission.CreateFor(channel);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EventListener AddListener(GameObject owner, EventChannel channel)
        {
            var listener = owner.AddComponent<EventListener>();
            var serialized = new SerializedObject(listener);
            SerializedProperty responses = serialized.FindProperty("responses");
            responses.arraySize = 1;
            responses.GetArrayElementAtIndex(0).managedReferenceValue = new VoidEventResponse();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            SetChannel(listener, "responses.Array.data[0].channel", channel);
            return listener;
        }

        private static void SetChannel(Object owner, string path, EventChannel channel)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(path).objectReferenceValue = channel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
