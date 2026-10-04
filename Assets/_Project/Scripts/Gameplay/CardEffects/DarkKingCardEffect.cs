using UnityEngine;

public sealed class DarkKingCardEffect : CardEffectController
{
    [Header("Dark King Countdown")]
    [SerializeField] private Animator darkKingAnimator;
    [SerializeField] private string remainingMovesParameter = "RemainingMoves";
    [SerializeField] private string countdownTrigger = "Countdown";
    [SerializeField] private ParticleSystem[] countdownParticles;

    [Header("Dark King Explosion")]
    [SerializeField] private string explodeTrigger = "Explode";
    [SerializeField] private ParticleSystem[] explosionParticles;

    public override void PlayCountdown(int remainingMoves)
    {
        base.PlayCountdown(remainingMoves);
        TrySetInteger(
            darkKingAnimator,
            remainingMovesParameter,
            remainingMoves
        );
        PlayEvent(darkKingAnimator, countdownTrigger, countdownParticles);
    }

    public override void PlayDestroy()
    {
        base.PlayDestroy();
        PlayEvent(darkKingAnimator, explodeTrigger, explosionParticles);
    }
}
