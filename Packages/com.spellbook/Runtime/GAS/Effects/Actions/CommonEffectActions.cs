using System;
using System.Collections.Generic;
using UnityEngine;
using SpellBook.Core;
using SpellBook.Core.Inspector;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Core;
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

        protected internal override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            string source = context.Instigator != null ? context.Instigator.name : "-";
            string target = context.Owner != null ? context.Owner.name : "-";
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(LogColor)}>[DebugLogAction]</color> {Message} (Source: {source}, Target: {target})");
            return ActionStatus.Done;
        }
    }

    [Serializable, TypeCategory("Apresentação", Summary = "Cria um efeito visual no alvo e, se marcado, o remove quando o efeito sai.",
        Line = "VFX {Prefab}")]
    [MovedFrom(false, sourceClassName: "SpawnVFXEffectAction")]
    public class SpawnVFXAction : GameplayAction, IEffectActionCleanup
    {
        public GameObject Prefab;
        [Tooltip("Cria e devolve o VFX. Vazio usa o spawner padrão, por Instantiate.")]
        public SpawnerAsset Spawner;
        public string AttachPointName = "Chest";
        public bool ParentToTarget = true;
        public Vector3 Offset = Vector3.zero;
        public bool DestroyOnRemoval = true;

        protected internal override ActionStatus Execute(ActionContext context, ref ActionFrame frame)
        {
            AbilitySystemComponent target = context.Owner;
            if (Prefab == null || target == null) return ActionStatus.Done;

            Transform parent = target.transform;
            if (!string.IsNullOrEmpty(AttachPointName))
            {
                Transform found = FindRecursive(target.transform, AttachPointName);
                if (found != null) parent = found;
            }

            SpawnerAsset spawner = SpawnerAsset.OrDefault(Spawner);
            // A posição é de mundo também com parent, como o SetParent da 2.x
            GameObject vfxInstance = spawner.Spawn(Prefab, parent.position + Offset, parent.rotation, ParentToTarget ? parent : null);
            if (vfxInstance == null)
            {
                // Spawner sem instância (ex.: pool vazio que não cresce): falha de configuração, não exceção
                Debug.LogWarning($"[SpawnVFXAction] O spawner '{spawner.name}' não criou uma instância de '{Prefab.name}'.", spawner);
                return ActionStatus.Done;
            }

            ActiveEffectInstance instance = context.EffectInstance;
            if (DestroyOnRemoval && instance != null)
            {
                instance.AddActionData(this, vfxInstance);
            }
            else if (vfxInstance.GetComponent<ParticleSystem>() != null)
            {
                // Sem dono para devolver: volta ao spawner quando a partícula parar
                spawner.ReleaseWhenParticlesStop(vfxInstance);
            }
            return ActionStatus.Done;
        }

        void IEffectActionCleanup.Cleanup(ActiveEffectInstance instance, List<object> data)
        {
            SpawnerAsset spawner = SpawnerAsset.OrDefault(Spawner);
            foreach (object item in data)
            {
                if (item is GameObject vfx && vfx != null) spawner.Release(vfx);
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

