using System.Collections;
using UnityEngine;

public class Checkpoint : MonoBehaviour, IRuntimeResettable
{
    [SerializeField] private Transform m_spawnPoint;
    [SerializeField] private bool m_setAsInitialCheckpoint;
    [SerializeField] private bool m_isLevelEnd;

    [Header("Activation Feedback")]
    [SerializeField] private ParticleSystem[] m_activationParticles;
    [SerializeField] [Min(0f)] private float m_particlePlayDuration = 3f;
    [SerializeField] private bool m_createFallbackParticles = true;
    [SerializeField] private AudioClip m_activationClip;
    [SerializeField] [Range(0f, 1f)] private float m_activationVolume = 1f;

    private Coroutine m_particleRoutine;
    private ParticleSystem m_fallbackParticles;

    public Vector3 SpawnPosition => (m_spawnPoint != null ? m_spawnPoint : transform).position;
    public Quaternion SpawnRotation => (m_spawnPoint != null ? m_spawnPoint : transform).rotation;
    public bool IsInitialCheckpoint => m_setAsInitialCheckpoint;

    private void Awake()
    {
        EnsureActivationParticles();
        StopActivationParticles(true);
    }

    private void Start()
    {
        if (m_setAsInitialCheckpoint && CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RegisterCheckpoint(this, false);
        }
    }

    private void OnDisable()
    {
        StopActivationParticles(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || other.GetComponentInParent<CharaController>() == null)
            return;

        if (CheckpointManager.Instance != null)
        {
            CheckpointManager.Instance.RegisterCheckpoint(this);
            if (m_isLevelEnd)
            {
                CheckpointManager.Instance.CompleteLevel();
            }
        }
    }

    public void PlayActivationParticles()
    {
        EnsureActivationParticles();
        if (!isActiveAndEnabled || m_activationParticles == null || m_activationParticles.Length == 0)
            return;

        if (m_particleRoutine != null)
            StopCoroutine(m_particleRoutine);

        PlayActivationSound();
        m_particleRoutine = StartCoroutine(PlayActivationParticlesRoutine());
    }

    private void PlayActivationSound()
    {
        if (m_activationClip == null)
            return;

        AudioSource.PlayClipAtPoint(m_activationClip, SpawnPosition, m_activationVolume);
    }

    public void ResetRuntimeState()
    {
        StopActivationParticles(true);
    }

    private IEnumerator PlayActivationParticlesRoutine()
    {
        PositionFallbackParticles();

        for (int i = 0; i < m_activationParticles.Length; i++)
        {
            ParticleSystem particles = m_activationParticles[i];
            if (particles == null)
                continue;

            particles.gameObject.SetActive(true);
            particles.Clear(true);
            particles.Play(true);
        }

        float duration = Mathf.Max(0f, m_particlePlayDuration);
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        StopActivationParticles(false);
        m_particleRoutine = null;
    }

    private void StopActivationParticles(bool clear)
    {
        if (m_particleRoutine != null)
        {
            StopCoroutine(m_particleRoutine);
            m_particleRoutine = null;
        }

        if (m_activationParticles == null)
            return;

        ParticleSystemStopBehavior stopBehavior = clear
            ? ParticleSystemStopBehavior.StopEmittingAndClear
            : ParticleSystemStopBehavior.StopEmitting;

        for (int i = 0; i < m_activationParticles.Length; i++)
        {
            ParticleSystem particles = m_activationParticles[i];
            if (particles != null)
                particles.Stop(true, stopBehavior);
        }
    }

    private void EnsureActivationParticles()
    {
        if (m_activationParticles != null && m_activationParticles.Length > 0)
            return;

        m_activationParticles = GetComponentsInChildren<ParticleSystem>(true);
        if (m_activationParticles.Length > 0 || !m_createFallbackParticles)
            return;

        m_fallbackParticles = CreateFallbackParticleSystem();
        m_activationParticles = new[] { m_fallbackParticles };
    }

    private ParticleSystem CreateFallbackParticleSystem()
    {
        GameObject particlesObject = new GameObject("CheckpointActivationParticles");
        Transform particlesTransform = particlesObject.transform;
        particlesTransform.SetParent(transform, false);
        particlesTransform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
        particlesTransform.localScale = GetInverseLossyScale(transform);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.duration = Mathf.Max(0.1f, m_particlePlayDuration);
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.9f, 1f, 0.95f),
            new Color(1f, 0.82f, 0.35f, 0.95f)
        );

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 14f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.9f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.82f, 0.35f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0f)
            )
        );

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particles;
    }

    private void PositionFallbackParticles()
    {
        if (m_fallbackParticles == null)
            return;

        Transform particlesTransform = m_fallbackParticles.transform;
        particlesTransform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
        particlesTransform.localScale = GetInverseLossyScale(transform);
    }

    private static Vector3 GetInverseLossyScale(Transform target)
    {
        Vector3 scale = target != null ? target.lossyScale : Vector3.one;
        return new Vector3(
            InverseScale(scale.x),
            InverseScale(scale.y),
            InverseScale(scale.z)
        );
    }

    private static float InverseScale(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }
}
