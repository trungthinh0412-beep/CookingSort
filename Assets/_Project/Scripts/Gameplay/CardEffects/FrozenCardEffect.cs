using UnityEngine;

public sealed class FrozenCardEffect : CardEffectController
{
    [Header("Frozen Break")]
    [SerializeField] private Animator frozenAnimator;
    [SerializeField] private string breakTrigger = "Break";
    [SerializeField] private ParticleSystem[] breakParticles;

    public override void PlayActivate()
    {
        base.PlayActivate();
        PlayEvent(frozenAnimator, breakTrigger, breakParticles);
    }
}
