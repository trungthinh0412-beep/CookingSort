using UnityEngine;

public sealed class IronCardEffect : CardEffectController
{
    [Header("Iron Move")]
    [SerializeField] private Animator ironAnimator;
    [SerializeField] private string impactTrigger = "Impact";
    [SerializeField] private ParticleSystem[] impactParticles;

    public override void PlayMove()
    {
        base.PlayMove();
        PlayEvent(ironAnimator, impactTrigger, impactParticles);
    }
}
