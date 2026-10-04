using UnityEngine;

public sealed class ChainCardEffect : CardEffectController
{
    [Header("Chain Highlight")]
    [SerializeField] private Animator chainAnimator;
    [SerializeField] private string linkedBool = "Linked";
    [SerializeField] private ParticleSystem[] linkedParticles;

    public override void PlaySelected(bool selected)
    {
        base.PlaySelected(selected);
        TrySetBool(chainAnimator, linkedBool, selected);
        if (selected)
            PlayParticles(linkedParticles);
    }
}
