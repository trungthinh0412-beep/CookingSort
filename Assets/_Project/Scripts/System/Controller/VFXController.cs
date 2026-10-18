using System;
using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;
using Random = UnityEngine.Random;


public class VFXController : SingletonDontDestroy<VFXController>
{
    [SerializeField] private VisualEffectConfig vfxConfig;
    
    public void SpawnEffect(EffectName effectName, Vector3 position, Transform parent)
    {
        VisualEffectData vfxData = vfxConfig.GetVisualEffectData(effectName);
        if (vfxData != null && vfxData.GetRandomEffect() != null)
        {
            GameObject randomEffect = vfxData.GetRandomEffect();
            GameObject effect = LeanPool.Spawn(randomEffect, position, randomEffect.transform.rotation, parent);
            effect.transform.localPosition = position;
        }
        else
        {
            Debug.LogWarning("<color=Red> Missing visual effect </color>");
        }
    }
    
    public void SpawnEffect(EffectName effectName, Vector3 position, Transform parent, float timeDestroy)
    {
        VisualEffectData vfxData = vfxConfig.GetVisualEffectData(effectName);
        if (vfxData != null && vfxData.GetRandomEffect() != null)
        {
            GameObject randomEffect = vfxData.GetRandomEffect();
            GameObject effect = LeanPool.Spawn(randomEffect, position, randomEffect.transform.rotation, parent);
            effect.transform.localPosition = position;
            LeanPool.Despawn(effect, timeDestroy);
        }
        else
        {
            Debug.LogWarning("<color=Red> Missing visual effect </color>");
        }
    }

    public void SpawnEffect(
        EffectName effectName,
        Vector3 position,
        Transform parent,
        float timeDestroy,
        float burstMultiplier)
    {
        VisualEffectData vfxData = vfxConfig.GetVisualEffectData(effectName);
        GameObject effectPrefab = vfxData != null
            ? vfxData.GetRandomEffect()
            : null;
        if (effectPrefab == null)
        {
            Debug.LogWarning("<color=Red> Missing visual effect </color>");
            return;
        }

        GameObject effect = LeanPool.Spawn(
            effectPrefab,
            position,
            effectPrefab.transform.rotation,
            parent
        );
        effect.transform.localPosition = position;
        EmitAdditionalBurst(effect, burstMultiplier);
        LeanPool.Despawn(effect, timeDestroy);
    }

    private static void EmitAdditionalBurst(
        GameObject effect,
        float burstMultiplier)
    {
        if (effect == null || burstMultiplier <= 1f)
            return;

        ParticleSystem[] particleSystems =
            effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            int burstCount = emission.burstCount;
            if (burstCount <= 0)
                continue;

            ParticleSystem.Burst[] bursts =
                new ParticleSystem.Burst[burstCount];
            int writtenCount = emission.GetBursts(bursts);
            int additionalParticles = 0;
            for (int burstIndex = 0; burstIndex < writtenCount; burstIndex++)
            {
                float configuredCount = Mathf.Max(
                    bursts[burstIndex].count.constantMin,
                    bursts[burstIndex].count.constantMax
                );
                additionalParticles += Mathf.RoundToInt(
                    configuredCount * (burstMultiplier - 1f)
                );
            }

            if (additionalParticles > 0)
                particleSystem.Emit(additionalParticles);
        }
    }
    
    public void SpawnEffect(EffectName effectName, Vector3 position, Transform parent, Vector3 localScale)
    {
        VisualEffectData vfxData = vfxConfig.GetVisualEffectData(effectName);
        if (vfxData != null && vfxData.GetRandomEffect() != null)
        {
            GameObject randomEffect = vfxData.GetRandomEffect();
            GameObject effect = LeanPool.Spawn(randomEffect, parent);
            effect.transform.localPosition = position;
            effect.transform.localScale = localScale;
        }
        else
        {
            Debug.LogWarning("<color=Red> Missing visual effect </color>");
        }
    }
    
    public void SpawnEffect(EffectName effectName, Vector3 position, Transform parent, Vector3 localScale, float timeDestroy)
    {
        VisualEffectData vfxData = vfxConfig.GetVisualEffectData(effectName);
        if (vfxData != null && vfxData.GetRandomEffect() != null)
        {
            GameObject randomEffect = vfxData.GetRandomEffect();
            GameObject effect = LeanPool.Spawn(randomEffect, parent);
            effect.transform.localPosition = position;
            effect.transform.localScale = localScale;
            LeanPool.Despawn(effect, timeDestroy);
        }
        else
        {
            Debug.LogWarning("<color=Red> Missing visual effect </color>");
        }
    }
}

[Serializable]
public class VisualEffectData
{
    public EffectName name;
    public List<GameObject> effects;


    public GameObject GetRandomEffect()
    {
        return effects[Random.Range(0, effects.Count)];
    }
}

public enum EffectName
{
    SparkleGold,
    SparkleDiamond,
    SparkleHeart,
    MatchItem,
    BombExplosion,
    MysteryBoxDisappear,
}
