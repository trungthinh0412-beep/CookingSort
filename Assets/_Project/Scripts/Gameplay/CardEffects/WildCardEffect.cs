using UnityEngine;

public sealed class WildCardEffect : CardEffectController
{
    [Header("Wild Transform")]
    [SerializeField] private Animator wildAnimator;
    [SerializeField] private string transformTrigger = "Transform";
    [SerializeField] private ParticleSystem[] transformParticles;

    public override void PlayActivate()
    {
        base.PlayActivate();
        PlayEvent(wildAnimator, transformTrigger, transformParticles);
    }
}
