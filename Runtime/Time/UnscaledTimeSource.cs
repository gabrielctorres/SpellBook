using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Time
{
    /// <summary>Tempo não escalado da Unity: ignora o <c>Time.timeScale</c>. Serve para UI e menus.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Time/Unscaled Time Source")]
    [HelpURL(DocUrls.Time + "#unscaledtimesource")]
    public sealed class UnscaledTimeSource : TimeSourceAsset
    {
        public override double ElapsedTime => UnityEngine.Time.unscaledTimeAsDouble;
        public override float DeltaTime => UnityEngine.Time.unscaledDeltaTime;
    }
}
