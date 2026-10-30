using SpellBook.Core;
using UnityEngine;

namespace SpellBook.Time
{
    /// <summary>Tempo escalado da Unity: segue o <c>Time.timeScale</c>.</summary>
    [CreateAssetMenu(menuName = "SpellBook/Time/Scaled Time Source")]
    [HelpURL(DocUrls.Time + "#scaledtimesource")]
    public sealed class ScaledTimeSource : TimeSourceAsset
    {
        public override double ElapsedTime => UnityEngine.Time.timeAsDouble;
        public override float DeltaTime => UnityEngine.Time.deltaTime;
    }
}
