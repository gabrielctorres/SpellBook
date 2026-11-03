using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpellBook.Teams.Editor;
using SpellBook.Testing;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.Teams.Tests
{
    /// <summary>Inspector do Teams: drawer da máscara e tabela da matriz (Design 3, CA17).</summary>
    public class TeamsInspectorTests
    {
        private TeamsFactory factory;
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> generatedBefore = new List<string>();

        [SetUp]
        public void SetUp()
        {
            factory = new TeamsFactory();
            generatedBefore.Clear();
            generatedBefore.AddRange(GeneratedTheme.Where(path => AssetDatabase.LoadMainAssetAtPath(path) != null));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created.Where(o => o != null))
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
            factory.Dispose();
            Undo.ClearAll();
            DeleteGeneratedTheme();
        }

        [UnityTest]
        public IEnumerator MaskDrawer_FourToggles_WriteFlags()
        {
            MaskHost host = CreateHost(TeamRelationMask.Allied | TeamRelationMask.Hostile);
            List<Toggle> toggles = null;
            yield return ShowMask(new SerializedObject(host), shown => toggles = shown);

            toggles[0].value = true;
            toggles[2].value = false;

            Assert.That(toggles.Select(t => t.label), Is.EqualTo(new[] { "Self", "Aliado", "Hostil", "Neutro" }));
            Assert.That(toggles.Select(t => t.value), Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(host.Mask, Is.EqualTo(TeamRelationMask.Self | TeamRelationMask.Allied));
        }

        [UnityTest]
        public IEnumerator MaskDrawer_AfterUndo_TogglesFollowMask()
        {
            MaskHost host = CreateHost(TeamRelationMask.Allied | TeamRelationMask.Hostile);
            List<Toggle> toggles = null;
            yield return ShowMask(new SerializedObject(host), shown => toggles = shown);
            Undo.IncrementCurrentGroup();
            toggles[2].value = false;

            Undo.PerformUndo();
            yield return WaitUntil(() => toggles[2].value);

            Assert.That(host.Mask, Is.EqualTo(TeamRelationMask.Allied | TeamRelationMask.Hostile));
            Assert.That(toggles.Select(t => t.value), Is.EqualTo(new[] { false, true, true, false }));
        }

        [UnityTest]
        public IEnumerator MaskDrawer_MultipleTargets_ShowsMixed_WritesOnlyTheFlag()
        {
            MaskHost first = CreateHost(TeamRelationMask.Allied | TeamRelationMask.Hostile);
            MaskHost second = CreateHost(TeamRelationMask.Self | TeamRelationMask.Hostile);
            List<Toggle> toggles = null;
            yield return ShowMask(new SerializedObject(new Object[] { first, second }), shown => toggles = shown);
            bool[] mixedBefore = toggles.Select(t => t.showMixedValue).ToArray();

            toggles[3].value = true;

            Assert.That(mixedBefore, Is.EqualTo(new[] { true, true, false, false }));
            Assert.That(toggles[2].value, Is.True);
            Assert.That(first.Mask, Is.EqualTo(TeamRelationMask.Allied | TeamRelationMask.Hostile | TeamRelationMask.Neutral));
            Assert.That(second.Mask, Is.EqualTo(TeamRelationMask.Self | TeamRelationMask.Hostile | TeamRelationMask.Neutral));
        }

        [UnityTest]
        public IEnumerator MaskDrawer_MultipleTargets_AfterUndo_MixedReturns()
        {
            MaskHost first = CreateHost(TeamRelationMask.Allied);
            MaskHost second = CreateHost(TeamRelationMask.Self);
            List<Toggle> toggles = null;
            yield return ShowMask(new SerializedObject(new Object[] { first, second }), shown => toggles = shown);
            Undo.IncrementCurrentGroup();
            toggles[0].value = true;

            Undo.PerformUndo();
            yield return WaitUntil(() => toggles[0].showMixedValue);

            Assert.That(first.Mask, Is.EqualTo(TeamRelationMask.Allied));
            Assert.That(second.Mask, Is.EqualTo(TeamRelationMask.Self));
            Assert.That(toggles.Select(t => t.showMixedValue), Is.EqualTo(new[] { true, true, false, false }));
        }

        [Test]
        public void MatrixEditor_TableHasCellPerPair_EmptyCellShowsDefault()
        {
            TeamDefinition red = factory.CreateTeam("Red");
            TeamDefinition blue = factory.CreateTeam("Blue");
            TeamRelationMatrix matrix = factory.CreateMatrix(TeamRelation.Neutral, red, blue);
            factory.SetCell(matrix, red, blue, TeamRelation.Hostile);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(matrix);
            created.Add(editor);

            VisualElement root = editor.CreateInspectorGUI();
            List<VisualElement> cells = root.Query(className: TeamRelationMatrixEditor.CellClass).ToList();
            List<Button> mirrorAll = root.Query<Button>(TeamRelationMatrixEditor.MirrorAllName).ToList();

            Assert.That(cells, Has.Count.EqualTo(4));
            AssertCell(cells[0], TeamRelation.Allied, isDefault: true);
            AssertCell(cells[1], TeamRelation.Hostile, isDefault: false);
            AssertCell(cells[2], TeamRelation.Neutral, isDefault: true);
            AssertCell(cells[3], TeamRelation.Allied, isDefault: true);
            Assert.That(mirrorAll, Has.Count.EqualTo(1));
            Assert.That(cells.All(c => c.Q<Button>(TeamRelationMatrixEditor.MirrorName) != null), Is.True);
        }

        private MaskHost CreateHost(TeamRelationMask mask)
        {
            var host = ScriptableObject.CreateInstance<MaskHost>();
            created.Add(host);
            var serialized = new SerializedObject(host);
            serialized.FindProperty("mask").intValue = (int)mask;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return host;
        }

        /// <summary>
        /// Mostra o campo da máscara num painel, ligado como no Inspector, e devolve as caixas do drawer. O painel é o
        /// de um <see cref="UIDocument"/>, que não pede dispositivo gráfico (a janela do Editor pede, e o batchmode roda
        /// com <c>-nographics</c>). A Unity grava o tema padrão em <c>Assets/UI Toolkit/</c> ao criar o painel; o
        /// TearDown apaga.
        /// </summary>
        private IEnumerator ShowMask(SerializedObject serialized, Action<List<Toggle>> shown)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var host = new GameObject("MaskPanel");
            created.Add(host);
            created.Add(settings);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = settings;
            yield return WaitUntil(() => document.rootVisualElement?.panel != null);

            var field = new PropertyField(serialized.FindProperty("mask"));
            document.rootVisualElement.Add(field);
            field.Bind(serialized);
            yield return WaitUntil(() => field.Query<Toggle>().ToList().Count == 4);
            shown(field.Query<Toggle>().ToList());
        }

        /// <summary>Espera o laço do Editor (a ligação confere as propriedades nele) até a condição, com um teto de quadros.</summary>
        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            for (int frame = 0; frame < MaxFrames && !condition(); frame++)
            {
                yield return null;
            }
        }

        private const int MaxFrames = 300;

        /// <summary>
        /// O tema padrão que a Unity grava em <c>Assets/</c> ao criar o painel de runtime, com as pastas dele, do arquivo
        /// para a raiz.
        /// </summary>
        private static readonly string[] GeneratedTheme =
        {
            "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss",
            "Assets/UI Toolkit/UnityThemes",
            "Assets/UI Toolkit",
        };

        /// <summary>Apaga o tema e as pastas que o teste criou e que ficaram vazias; o que já existia fica.</summary>
        private void DeleteGeneratedTheme()
        {
            foreach (string path in GeneratedTheme.Where(path => !generatedBefore.Contains(path)))
            {
                bool isFolder = AssetDatabase.IsValidFolder(path);
                if (isFolder && AssetDatabase.FindAssets(string.Empty, new[] { path }).Length > 0)
                {
                    return;
                }

                if (isFolder || AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void AssertCell(VisualElement cell, TeamRelation expected, bool isDefault)
        {
            EnumField field = cell.Q<EnumField>();
            Assert.That(field.value, Is.EqualTo(expected));
            Assert.That(cell.ClassListContains(TeamRelationMatrixEditor.DefaultCellClass), Is.EqualTo(isDefault));
        }
    }

    /// <summary>Objeto com um campo de máscara, para o drawer.</summary>
    public sealed class MaskHost : ScriptableObject
    {
        [SerializeField] private TeamRelationMask mask = TeamRelationMask.Allied | TeamRelationMask.Hostile;

        public TeamRelationMask Mask => mask;
    }
}
