using System;
using UnityEngine;
using SpellBook.GAS.Core;
using Sirenix.OdinInspector;

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
        [Required]
        public GameObject Prefab;
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

            GameObject vfxInstance = GameObject.Instantiate(Prefab, parent.position + Offset, parent.rotation);
            if (ParentToTarget)
            {
                vfxInstance.transform.SetParent(parent);
            }
            
            if (DestroyOnRemoval && instance != null)
            {
                instance.AddActionData(this, vfxInstance);
            }
            else
            {
                // If it's a particle system, it might destroy itself, or we give it a life
                var ps = vfxInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    GameObject.Destroy(vfxInstance, ps.main.duration + ps.main.startLifetime.constantMax);
                }
            }
        }

        public override void Cleanup(AbilitySystemComponent source, AbilitySystemComponent target, ActiveEffectInstance instance)
        {
            if (DestroyOnRemoval && instance != null)
            {
                if (instance.TryGetActionData<GameObject>(this, out var vfxInstances))
                {
                    foreach (var vfx in vfxInstances)
                    {
                        if (vfx != null) GameObject.Destroy(vfx);
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

