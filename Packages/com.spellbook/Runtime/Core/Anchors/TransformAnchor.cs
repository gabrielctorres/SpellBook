using UnityEngine;

namespace SpellBook.Core
{
    /// <summary>
    /// Âncora de um <see cref="Transform"/> de cena, como um ponto de spawn, o alvo da câmera ou um HUD. Um
    /// <see cref="AnchorRegistrar"/> sem componente publica o <see cref="Transform"/> do próprio objeto, e quem lê a
    /// âncora sabe se o objeto está presente sem procurar na cena (ADR 0039, item 5).
    /// </summary>
    [HelpURL(DocUrls.Concepts + "#ancoras")]
    [CreateAssetMenu(menuName = "SpellBook/Core/Transform Anchor", fileName = "ANC_")]
    public sealed class TransformAnchor : RuntimeAnchor<Transform>
    {
    }
}
