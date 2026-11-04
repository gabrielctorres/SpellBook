using System;
using UnityEngine;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using UnityEngine.Scripting.APIUpdating;

namespace SpellBook.GAS.Effects
{
    [Serializable, TypeCategory("Depuração", Summary = "Escreve uma mensagem colorida no Console quando o efeito roda.",
        Line = "Log: {Message}")]
    [MovedFrom(false, sourceClassName: "DebugLogEffectAction")]
    public class DebugLogAction : GameplayAction
    {
        public string Message = "Effect Triggered!";
        public Color LogColor = Color.white;

        public override bool UsesTargets => false;

        protected override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            string source = context.Instigator != null ? context.Instigator.name : "-";
            string target = context.Owner != null ? context.Owner.name : "-";
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(LogColor)}>[DebugLogAction]</color> {Message} (Source: {source}, Target: {target})");
            return ActionStatus.Done;
        }
    }
}
