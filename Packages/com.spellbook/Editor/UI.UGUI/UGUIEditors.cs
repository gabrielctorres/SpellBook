using SpellBook.Core.Editor;
using UnityEditor;

namespace SpellBook.UI.UGUI.Editor
{
    /// <summary>Inspectors de uma linha das views uGUI (Spec, seção 4.10, "Um editor por tipo").</summary>
    [CustomEditor(typeof(BarView))]
    [CanEditMultipleObjects]
    internal sealed class BarViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(TextView))]
    [CanEditMultipleObjects]
    internal sealed class TextViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(TagIndicatorView))]
    [CanEditMultipleObjects]
    internal sealed class TagIndicatorViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(FloatingTextView))]
    [CanEditMultipleObjects]
    internal sealed class FloatingTextViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(SlotView))]
    [CanEditMultipleObjects]
    internal sealed class SlotViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(ListOverflowView))]
    [CanEditMultipleObjects]
    internal sealed class ListOverflowViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(TooltipView))]
    [CanEditMultipleObjects]
    internal sealed class TooltipViewEditor : SpellBookEditor { }

    [CustomEditor(typeof(TooltipLineView))]
    [CanEditMultipleObjects]
    internal sealed class TooltipLineViewEditor : SpellBookEditor { }
}
