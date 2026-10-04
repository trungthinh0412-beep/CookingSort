using UnityEngine;

public abstract class CardEffectController : MonoBehaviour
{
    [Header("Shared Animator")]
    [SerializeField] private Animator effectAnimator;
    [SerializeField] private string spawnTrigger = "Spawn";
    [SerializeField] private string selectedBool = "Selected";
    [SerializeField] private string moveTrigger = "Move";
    [SerializeField] private string activateTrigger = "Activate";
    [SerializeField] private string destroyTrigger = "Destroy";

    [Header("Shared Particles")]
    [SerializeField] private ParticleSystem[] spawnParticles;
    [SerializeField] private ParticleSystem[] selectedParticles;
    [SerializeField] private ParticleSystem[] moveParticles;
    [SerializeField] private ParticleSystem[] activateParticles;
    [SerializeField] private ParticleSystem[] destroyParticles;

    public virtual void PlaySpawn()
    {
        PlayEvent(effectAnimator, spawnTrigger, spawnParticles);
    }

    public virtual void PlaySelected(bool selected)
    {
        TrySetBool(effectAnimator, selectedBool, selected);
        if (selected)
            PlayParticles(selectedParticles);
    }

    public virtual void PlayMove()
    {
        PlayEvent(effectAnimator, moveTrigger, moveParticles);
    }

    public virtual void PlayActivate()
    {
        PlayEvent(effectAnimator, activateTrigger, activateParticles);
    }

    public virtual void PlayCountdown(int remainingMoves)
    {
    }

    public virtual void PlayDestroy()
    {
        PlayEvent(effectAnimator, destroyTrigger, destroyParticles);
    }

    protected static void PlayEvent(
        Animator animator,
        string trigger,
        ParticleSystem[] particles)
    {
        TrySetTrigger(animator, trigger);
        PlayParticles(particles);
    }

    protected static void PlayParticles(ParticleSystem[] particles)
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            if (!particle.gameObject.activeSelf)
                particle.gameObject.SetActive(true);
            particle.Play(true);
        }
    }

    protected static void TrySetTrigger(Animator animator, string parameter)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameter) ||
            !HasParameter(animator, parameter, AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.SetTrigger(parameter);
    }

    protected static void TrySetBool(
        Animator animator,
        string parameter,
        bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameter) ||
            !HasParameter(animator, parameter, AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(parameter, value);
    }

    protected static void TrySetInteger(
        Animator animator,
        string parameter,
        int value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameter) ||
            !HasParameter(animator, parameter, AnimatorControllerParameterType.Int))
        {
            return;
        }

        animator.SetInteger(parameter, value);
    }

    private static bool HasParameter(
        Animator animator,
        string parameter,
        AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter item = parameters[i];
            if (item.type == type && item.name == parameter)
                return true;
        }

        return false;
    }
}
