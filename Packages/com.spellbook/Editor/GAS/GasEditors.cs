using SpellBook.Core.Editor;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Projectiles;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Editor.Actions;
using SpellBook.GAS.Thinkers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace SpellBook.GAS.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do GAS (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(AbilitySystemComponent))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySystemComponentEditor : SpellBookEditor { }

    [CustomEditor(typeof(AbilitySystemAnchor))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySystemAnchorEditor : SpellBookEditor { }

    [CustomEditor(typeof(AttributeSet))]
    [CanEditMultipleObjects]
    internal sealed class AttributeSetEditor : SpellBookEditor { }

    [CustomEditor(typeof(CooldownReductionPolicy))]
    [CanEditMultipleObjects]
    internal sealed class CooldownReductionPolicyEditor : SpellBookEditor { }

    [CustomEditor(typeof(AttributeCategory))]
    [CanEditMultipleObjects]
    internal sealed class AttributeCategoryEditor : SpellBookEditor { }

    [CustomEditor(typeof(Formula), true)]
    [CanEditMultipleObjects]
    internal sealed class FormulaEditor : SpellBookEditor { }

    /// <summary>Inspector do <see cref="Projectile"/>: o padrão e a aba "Ao terminar" (R22).</summary>
    [CustomEditor(typeof(Projectile))]
    [CanEditMultipleObjects]
    internal sealed class ProjectileEditor : SpellBookEditor
    {
        protected override void BuildAfterDefault(VisualElement root)
        {
            ActionEventsView.HideDefaultFields(root, typeof(Projectile));
            root.Add(new ActionEventsView(serializedObject, "Sem ações \"Ao terminar\": o projétil só volta ao pool."));
        }
    }

    /// <summary>Ensina o thinker a reconhecer a cena de prévia do Prefab Mode, que não é a cena do jogo.</summary>
    [InitializeOnLoad]
    internal static class GameplayThinkerPreviewScene
    {
        static GameplayThinkerPreviewScene()
        {
            GameplayThinker.IsInPreviewScene = go => EditorSceneManager.IsPreviewScene(go.scene);
        }
    }
}
