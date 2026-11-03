using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Cues
{
    /// <summary>
    /// Âncora em que o <see cref="CueRouter"/> da cena se publica. Assets, como habilidades e efeitos, apontam a âncora
    /// para tocar cues sem procurar o roteador.
    /// </summary>
    [HelpURL(DocUrls.Cues + "#cuerouteranchor")]
    [CreateAssetMenu(menuName = "SpellBook/Cues/Cue Router Anchor", fileName = "CueRouterAnchor")]
    public sealed class CueRouterAnchor : RuntimeAnchor<CueRouter>
    {
    }
}
