using System;
using UnityEngine;
using SpellBook.Core;
using SpellBook.GAS.Core;

namespace SpellBook.GAS.Effects
{
    [Serializable]
    public class DebugLogEffectAction : EffectAction
    {
        public string Message = "Effect Triggered!";
        public Color LogColor = Color.white;

        public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null)
        {
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(LogColor)}>[EffectAction]</color> {Message} (Source: {source.name}, Target: {target.name})");
        }
    }

    [Serializable]
    public class SpawnVFXEffectAction : EffectAction
    {
        public GameObject Prefab;
        [Tooltip("Cria e devolve o VFX. Vazio usa o spawner padrão, por Instantiate.")]
        public SpawnerAsset Spawner;
        public string AttachPointName = "Chest";
        public bool ParentToTarget = true;
        public Vector3 Offset = Vector3.zero;
        public bool DestroyOnRemoval = true;

        public override void Execute(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance = null)
        {
            if (Prefab == null) return;

            Transform parent = target.transform;
            // Basic attach point search (could be improved with a dedicated component)
            if (!string.IsNullOrEmpty(AttachPointName))
            {
                Transform found = FindRecursive(target.transform, AttachPointName);
                if (found != null) parent = found;
            }

            SpawnerAsset spawner = SpawnerAsset.OrDefault(Spawner);
            // A posição é de mundo também com parent, como o SetParent da 2.x
            GameObject vfxInstance = spawner.Spawn(Prefab, parent.position + Offset, parent.rotation, ParentToTarget ? parent : null);

            if (DestroyOnRemoval && instance != null)
            {
                instance.AddActionData(this, vfxInstance);
            }
            else if (vfxInstance.GetComponent<ParticleSystem>() != null)
            {
                // Sem dono para devolver: volta ao spawner quando a partícula parar
                spawner.ReleaseWhenParticlesStop(vfxInstance);
            }
        }

        public override void Cleanup(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance)
        {
            if (DestroyOnRemoval && instance != null)
            {
                if (instance.TryGetActionData<GameObject>(this, out var vfxInstances))
                {
                    SpawnerAsset spawner = SpawnerAsset.OrDefault(Spawner);
                    foreach (var vfx in vfxInstances)
                    {
                        if (vfx != null) spawner.Release(vfx);
                    }
                }
            }
        }

        private Transform FindRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

