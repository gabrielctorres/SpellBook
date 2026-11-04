using SpellBook.Core.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SpellBook.UI.Editor
{
    /// <summary>Inspectors de uma linha dos tipos do UI (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(UIUpdateDriver))]
    [CanEditMultipleObjects]
    internal sealed class UIUpdateDriverEditor : SpellBookEditor { }

    [CustomEditor(typeof(UITheme))]
    [CanEditMultipleObjects]
    internal sealed class UIThemeEditor : SpellBookEditor { }

    [CustomEditor(typeof(BarBinder))]
    [CanEditMultipleObjects]
    internal sealed class BarBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(TextBinder))]
    [CanEditMultipleObjects]
    internal sealed class TextBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(TagIndicatorBinder))]
    [CanEditMultipleObjects]
    internal sealed class TagIndicatorBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(FloatingTextStyle))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextStyleEditor : SpellBookEditor { }

    [CustomEditor(typeof(FloatingTextBinder))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(UIItemContext))]
    [CanEditMultipleObjects]
    internal sealed class UIItemContextEditor : SpellBookEditor { }

    [CustomEditor(typeof(FixedKeyLabelProvider))]
    [CanEditMultipleObjects]
    internal sealed class FixedKeyLabelProviderEditor : SpellBookEditor { }

    [CustomEditor(typeof(AbilitySlotBinder))]
    [CanEditMultipleObjects]
    internal sealed class AbilitySlotBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(AbilityBarBinder))]
    [CanEditMultipleObjects]
    internal sealed class AbilityBarBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(CastBarBinder))]
    [CanEditMultipleObjects]
    internal sealed class CastBarBinderEditor : UIBinderEditor { }

    [CustomEditor(typeof(TooltipBinder))]
    [CanEditMultipleObjects]
    internal sealed class TooltipBinderEditor : SpellBookEditor { }

    [CustomEditor(typeof(TooltipBinderAnchor))]
    [CanEditMultipleObjects]
    internal sealed class TooltipBinderAnchorEditor : SpellBookEditor { }

    /// <summary>Ensina o resolvedor a reconhecer a cena de prévia do Prefab Mode, que não é a cena do jogo.</summary>
    [InitializeOnLoad]
    internal static class ActorResolverPreviewScene
    {
        static ActorResolverPreviewScene()
        {
            ActorResolver.IsInPreviewScene = go => EditorSceneManager.IsPreviewScene(go.scene);
        }
    }
}
