using SpellBook.Core;
using UnityEngine;

namespace SpellBook.GAS.Core
{
    /// <summary>
    /// Âncora de um <see cref="AbilitySystemComponent"/>, como o do jogador. O ator se publica nela por um
    /// <see cref="AnchorRegistrar"/>, e a UI e outros assets acham o ator sem procurar na cena (ADR 0039, item 5).
    /// </summary>
    [HelpURL(DocUrls.Gas + "#abilitysystemanchor")]
    [CreateAssetMenu(menuName = "SpellBook/GAS/Ability System Anchor", fileName = "AbilitySystemAnchor")]
    public sealed class AbilitySystemAnchor : RuntimeAnchor<AbilitySystemComponent>
    {
    }
}
