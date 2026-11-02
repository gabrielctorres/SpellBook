using System;
using System.Collections;
using SpellBook.Core.Inspector;
using UnityEngine;
using SpellBook.Core;

namespace SpellBook.GAS.Abilities.Actions
{
    [Serializable, TypeCategory("Apresentação", Summary = "Cria um efeito visual e toca um som em quem lançou, no alvo ou no ponto.")]
    public class PlayCueAction : AbilityAction
    {
        public GameObject VFX_Prefab;
        [Tooltip("Cria o VFX. Vazio usa o spawner padrão, por Instantiate. O VFX não é devolvido (até o módulo Cues).")]
        public SpawnerAsset Spawner;
        public AudioClip SFX_Clip;

        [Header("Location Configuration")]
        public AbilityTargetSource TargetSource = AbilityTargetSource.Location;
        public bool AttachToTransform = false;

        public override IEnumerator Execute(AbilityContext context)
        {
            Vector3 spawnPos = context.TargetPosition;
            Transform parent = null;
            var targets = context.GetValidTargets();

            if (TargetSource == AbilityTargetSource.Caster && context.Source != null)
            {
                spawnPos = context.Source.transform.position;
                if (AttachToTransform) parent = context.Source.transform;
            }
            else if (TargetSource == AbilityTargetSource.Target && targets.Count > 0)
            {
                spawnPos = targets[0].transform.position;
                if (AttachToTransform) parent = targets[0].transform;
            }

            if (VFX_Prefab)
            {
                // parent só é preenchido com AttachToTransform ligado
                SpawnerAsset.OrDefault(Spawner).Spawn(VFX_Prefab, spawnPos, Quaternion.identity, parent);
            }

            if (SFX_Clip) AudioSource.PlayClipAtPoint(SFX_Clip, spawnPos);

            yield return null;
        }
    }
}


