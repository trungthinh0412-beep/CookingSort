using UnityEngine;

public sealed class DowngradeCardEffect : CardEffectController
{
    [Header("Downgrade Consume")]
    [SerializeField] private Animator downgradeAnimator;
    [SerializeField] private string consumeTrigger = "Consume";
    [SerializeField] private ParticleSystem[] consumeParticles;

    public override void PlayActivate()
    {
        base.PlayActivate();
        PlayEvent(downgradeAnimator, consumeTrigger, consumeParticles);
    }
}
