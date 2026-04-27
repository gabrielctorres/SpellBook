using System;
using System.Collections;
using UnityEngine;
using SpellBook.Systems.Pooling;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable]
    public class PlayCueAction : AbilityAction
    {
        public GameObject VFX_Prefab;
        public AudioClip SFX_Clip;

        [Header("Location Configuration")]
        public AbilityTargetSource TargetSource = AbilityTargetSource.Location;
        public bool AttachToTransform = false;

        public override IEnumerator Execute(AbilityContext context)
        {
            Vector3 spawnPos = context.TargetPosition;
            Transform parent = null;

            if (TargetSource == AbilityTargetSource.Caster)
            {
                spawnPos = context.Source.transform.position;
                if (AttachToTransform) parent = context.Source.transform;
            }
            else if (TargetSource == AbilityTargetSource.Target && context.Targets.Count > 0)
            {
                spawnPos = context.Targets[0].transform.position;
                if (AttachToTransform) parent = context.Targets[0].transform;
            }

            if (VFX_Prefab)
            {
                var vfx = PoolManager.Instance.Get(VFX_Prefab, spawnPos, Quaternion.identity);
                if (AttachToTransform && parent != null) vfx.transform.SetParent(parent);
            }

            if (SFX_Clip) AudioSource.PlayClipAtPoint(SFX_Clip, spawnPos);

            yield return null;
        }
    }
}


