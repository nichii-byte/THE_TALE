using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EmergingHandsTrap : MonoBehaviour, IRuntimeResettable
{
    private enum ActivationMode
    {
        TriggerZone,
        Timer
    }

    [Header("Mode")]
    [SerializeField] private ActivationMode m_activationMode = ActivationMode.TriggerZone;
    [SerializeField] private bool m_restartSequenceWhenTriggeredAgain = true;
    [SerializeField] private bool m_triggerOnEnable = false;
    [SerializeField] private bool m_triggerOnRuntimeReset = false;

    [Header("References")]
    [SerializeField] private Transform m_handRoot;
    [SerializeField] private Transform[] m_additionalMovingRoots;
    [SerializeField] private Transform m_retractedPoint;
    [SerializeField] private Transform m_extendedPoint;
    [SerializeField] private Collider m_activationTrigger;
    [SerializeField] private Collider m_damageCollider;

    [Header("Fallback Motion")]
    [SerializeField] private Vector3 m_extendedLocalOffset = new Vector3(0f, 1f, 0f);

    [Header("Sequence")]
    [SerializeField] private float m_extendDuration = 0.18f;
    [SerializeField] private float m_holdDuration = 0.8f;
    [SerializeField] private float m_retractDuration = 0.22f;
    [SerializeField] private float m_retriggerCooldown = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioClip m_activationClip;
    [SerializeField] [Range(0f, 1f)] private float m_activationVolume = 1f;

    [Header("Timer")]
    [SerializeField] private float m_timerInterval = 2f;
    [SerializeField] private float m_initialTimerDelay = 0f;
    [SerializeField] private float m_triggerDelay = 0f;

    [Header("Traffic Light Sync")]
    [SerializeField] private bool m_syncTimerWithTrafficLight = false;
    [SerializeField] private TrafficLightAlternator m_trafficLight;
    [SerializeField] private bool m_autoFindTrafficLight = true;

    private Vector3 m_defaultRetractedLocalPosition;
    private Transform[] m_resolvedMovingRoots;
    private Vector3[] m_resolvedMovingRootWorldOffsets;
    private Coroutine m_sequenceRoutine;
    private Coroutine m_timerRoutine;
    private Coroutine m_trafficLightMoveRoutine;
    private TrafficLightAlternator m_subscribedTrafficLight;
    private float m_lastTriggerTime = float.NegativeInfinity;
    private readonly HashSet<Collider> m_playerActivationColliders = new HashSet<Collider>();

    private void Awake()
    {
        if (m_handRoot == null)
            m_handRoot = transform;

        if (m_damageCollider == null)
            m_damageCollider = m_handRoot.GetComponent<Collider>();

        if (m_activationTrigger == null)
            m_activationTrigger = GetComponent<Collider>();

        EnsureTriggerRelay();
        m_defaultRetractedLocalPosition = m_handRoot.localPosition;
        CacheMovingRoots();
    }

    private void OnEnable()
    {
        ResetRuntimeState(false);

        if (m_triggerOnEnable)
            TriggerTrap();
    }

    private void OnDisable()
    {
        StopRuntimeCoroutines();
        UnsubscribeFromTrafficLight();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryTriggerFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryTriggerStayFromCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        TryReleaseFromCollider(other);
    }

    public void ResetRuntimeState()
    {
        ResetRuntimeState(m_triggerOnRuntimeReset);
    }

    private void ResetRuntimeState(bool triggerAfterReset)
    {
        StopRuntimeCoroutines();
        UnsubscribeFromTrafficLight();
        m_lastTriggerTime = float.NegativeInfinity;
        m_playerActivationColliders.Clear();

        ApplyActivationTriggerState();
        RefreshActivationTriggerState();
        SetDamageState(false);
        SetHandLocalPosition(GetRetractedLocalPosition());

        if (ShouldSyncWithTrafficLight())
        {
            SetupTrafficLightSync();
        }
        else if (m_activationMode == ActivationMode.Timer && isActiveAndEnabled)
        {
            m_timerRoutine = StartCoroutine(TimerRoutine());
        }

        if (triggerAfterReset && m_triggerOnEnable)
        {
            TriggerTrap();
        }
    }

    public void TriggerTrap()
    {
        if (!isActiveAndEnabled || m_handRoot == null)
            return;

        if (Time.time < m_lastTriggerTime + Mathf.Max(0f, m_retriggerCooldown))
            return;

        if (m_sequenceRoutine != null)
        {
            if (!m_restartSequenceWhenTriggeredAgain)
                return;

            StopCoroutine(m_sequenceRoutine);
        }

        m_lastTriggerTime = Time.time;
        PlayActivationSound();
        m_sequenceRoutine = StartCoroutine(EmergeRoutine());
    }

    public void TryTriggerFromCollider(Collider other)
    {
        if (m_activationMode != ActivationMode.TriggerZone)
            return;

        if (CheckpointManager.Instance != null && CheckpointManager.Instance.IsRespawning)
            return;

        if (!IsPlayerCollider(other))
            return;

        bool wasEmpty = m_playerActivationColliders.Count == 0;
        m_playerActivationColliders.Add(other);
        if (!wasEmpty)
            return;

        TriggerTrap();
    }

    public void TryTriggerStayFromCollider(Collider other)
    {
        if (!IsPlayerCollider(other) || m_playerActivationColliders.Contains(other))
            return;

        TryTriggerFromCollider(other);
    }

    public void TryReleaseFromCollider(Collider other)
    {
        if (m_activationMode != ActivationMode.TriggerZone)
            return;

        if (!IsPlayerCollider(other))
            return;

        m_playerActivationColliders.Remove(other);
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other != null && other.GetComponentInParent<CharaController>() != null;
    }

    private void StopRuntimeCoroutines()
    {
        if (m_sequenceRoutine != null)
        {
            StopCoroutine(m_sequenceRoutine);
            m_sequenceRoutine = null;
        }

        if (m_timerRoutine != null)
        {
            StopCoroutine(m_timerRoutine);
            m_timerRoutine = null;
        }

        if (m_trafficLightMoveRoutine != null)
        {
            StopCoroutine(m_trafficLightMoveRoutine);
            m_trafficLightMoveRoutine = null;
        }
    }

    private void ApplyActivationTriggerState()
    {
        if (m_activationTrigger != null)
            m_activationTrigger.enabled = m_activationMode == ActivationMode.TriggerZone;
    }

    private void RefreshActivationTriggerState()
    {
        if (m_activationMode != ActivationMode.TriggerZone || m_activationTrigger == null || !m_activationTrigger.gameObject.activeInHierarchy)
            return;

        m_activationTrigger.enabled = false;
        m_activationTrigger.enabled = true;
        Physics.SyncTransforms();
    }

    private void EnsureTriggerRelay()
    {
        if (m_activationTrigger == null)
            return;

        GameObject triggerObject = m_activationTrigger.gameObject;
        if (triggerObject == gameObject)
            return;

        EmergingHandsTrapTriggerRelay relay = triggerObject.GetComponent<EmergingHandsTrapTriggerRelay>();
        if (relay == null)
            relay = triggerObject.AddComponent<EmergingHandsTrapTriggerRelay>();

        relay.Initialize(this);
    }

    private void SetDamageState(bool isActive)
    {
        if (m_damageCollider != null)
            m_damageCollider.enabled = isActive;
    }

    private bool ShouldSyncWithTrafficLight()
    {
        return m_activationMode == ActivationMode.Timer && m_syncTimerWithTrafficLight;
    }

    private void SetupTrafficLightSync()
    {
        TrafficLightAlternator trafficLight = ResolveTrafficLight();
        if (trafficLight == null)
        {
            Debug.LogWarning("EmergingHandsTrap: traffic light sync is enabled but no TrafficLightAlternator was found.", this);
            return;
        }

        m_subscribedTrafficLight = trafficLight;
        m_subscribedTrafficLight.StateChanged += HandleTrafficLightStateChanged;
        ApplyTrafficLightState(m_subscribedTrafficLight.IsGreenActive, true);
    }

    private TrafficLightAlternator ResolveTrafficLight()
    {
        if (m_trafficLight != null)
            return m_trafficLight;

        if (!m_autoFindTrafficLight)
            return null;

        m_trafficLight = GetComponentInParent<TrafficLightAlternator>();
        if (m_trafficLight == null)
            m_trafficLight = FindFirstObjectByType<TrafficLightAlternator>();

        return m_trafficLight;
    }

    private void UnsubscribeFromTrafficLight()
    {
        if (m_subscribedTrafficLight == null)
            return;

        m_subscribedTrafficLight.StateChanged -= HandleTrafficLightStateChanged;
        m_subscribedTrafficLight = null;
    }

    private void HandleTrafficLightStateChanged(bool isGreenActive)
    {
        ApplyTrafficLightState(isGreenActive, false);
    }

    private void ApplyTrafficLightState(bool isGreenActive, bool snap)
    {
        if (!isActiveAndEnabled || m_handRoot == null)
            return;

        if (m_trafficLightMoveRoutine != null)
        {
            StopCoroutine(m_trafficLightMoveRoutine);
            m_trafficLightMoveRoutine = null;
        }

        Vector3 targetPosition = isGreenActive ? GetRetractedLocalPosition() : GetExtendedLocalPosition();
        bool shouldDamage = !isGreenActive;

        if (!isGreenActive && !snap)
        {
            PlayActivationSound();
        }

        if (snap)
        {
            SetDamageState(false);
            SetHandLocalPosition(targetPosition);
            SetDamageState(shouldDamage);
            return;
        }

        float moveDuration = isGreenActive ? m_retractDuration : m_extendDuration;
        m_trafficLightMoveRoutine = StartCoroutine(MoveToTrafficLightStateRoutine(targetPosition, shouldDamage, moveDuration));
    }

    private IEnumerator MoveToTrafficLightStateRoutine(Vector3 targetPosition, bool shouldDamage, float duration)
    {
        SetDamageState(false);

        Vector3 startPosition = m_handRoot.localPosition;
        yield return MoveHand(startPosition, targetPosition, duration);

        SetDamageState(shouldDamage);
        m_trafficLightMoveRoutine = null;
    }

    private Vector3 GetRetractedLocalPosition()
    {
        if (m_retractedPoint != null)
            return ToHandParentLocalPosition(m_retractedPoint.position);

        return m_defaultRetractedLocalPosition;
    }

    private Vector3 GetExtendedLocalPosition()
    {
        if (m_extendedPoint != null)
            return ToHandParentLocalPosition(m_extendedPoint.position);

        return GetRetractedLocalPosition() + m_extendedLocalOffset;
    }

    private Vector3 ToHandParentLocalPosition(Vector3 worldPosition)
    {
        Transform handParent = m_handRoot.parent;
        return handParent != null ? handParent.InverseTransformPoint(worldPosition) : worldPosition;
    }

    private void SetHandLocalPosition(Vector3 localPosition)
    {
        if (m_handRoot != null)
            m_handRoot.localPosition = localPosition;

        if (m_resolvedMovingRoots == null || m_resolvedMovingRootWorldOffsets == null)
            return;

        Vector3 handWorldPosition = ToWorldPosition(m_handRoot, localPosition);
        for (int i = 0; i < m_resolvedMovingRoots.Length; i++)
        {
            Transform movingRoot = m_resolvedMovingRoots[i];
            if (movingRoot == null)
                continue;

            Vector3 targetWorldPosition = handWorldPosition + m_resolvedMovingRootWorldOffsets[i];
            Transform movingRootParent = movingRoot.parent;
            movingRoot.localPosition = movingRootParent != null
                ? movingRootParent.InverseTransformPoint(targetWorldPosition)
                : targetWorldPosition;
        }
    }

    private void CacheMovingRoots()
    {
        if (m_handRoot == null)
        {
            m_resolvedMovingRoots = null;
            m_resolvedMovingRootWorldOffsets = null;
            return;
        }

        List<Transform> movingRoots = new List<Transform>();
        if (m_additionalMovingRoots != null)
        {
            for (int i = 0; i < m_additionalMovingRoots.Length; i++)
            {
                Transform candidate = m_additionalMovingRoots[i];
                if (candidate == null || candidate == m_handRoot || movingRoots.Contains(candidate))
                    continue;

                movingRoots.Add(candidate);
            }
        }

        m_resolvedMovingRoots = movingRoots.ToArray();
        m_resolvedMovingRootWorldOffsets = new Vector3[m_resolvedMovingRoots.Length];

        Vector3 handWorldPosition = m_handRoot.position;
        for (int i = 0; i < m_resolvedMovingRoots.Length; i++)
        {
            m_resolvedMovingRootWorldOffsets[i] = m_resolvedMovingRoots[i].position - handWorldPosition;
        }
    }

    private Vector3 ToWorldPosition(Transform target, Vector3 localPosition)
    {
        if (target == null)
            return localPosition;

        Transform targetParent = target.parent;
        return targetParent != null ? targetParent.TransformPoint(localPosition) : localPosition;
    }

    private IEnumerator TimerRoutine()
    {
        if (m_initialTimerDelay > 0f)
            yield return new WaitForSeconds(m_initialTimerDelay);

        while (true)
        {
            yield return EmergeRoutine();

            float waitDuration = Mathf.Max(0.01f, m_timerInterval);
            yield return new WaitForSeconds(waitDuration);
        }
    }

    private IEnumerator EmergeRoutine()
    {
        if (m_handRoot == null)
            yield break;

        Vector3 retractedPosition = GetRetractedLocalPosition();
        Vector3 extendedPosition = GetExtendedLocalPosition();

        if (m_triggerDelay > 0f)
            yield return new WaitForSeconds(m_triggerDelay);

        SetDamageState(false);
        SetHandLocalPosition(retractedPosition);

        yield return MoveHand(retractedPosition, extendedPosition, m_extendDuration);

        SetDamageState(true);

        if (m_holdDuration > 0f)
            yield return new WaitForSeconds(m_holdDuration);

        SetDamageState(false);

        yield return MoveHand(extendedPosition, retractedPosition, m_retractDuration);
        m_sequenceRoutine = null;
    }

    private IEnumerator MoveHand(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            SetHandLocalPosition(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetHandLocalPosition(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }

        SetHandLocalPosition(to);
    }

    private void PlayActivationSound()
    {
        if (m_activationClip == null)
            return;

        Vector3 soundPosition = m_handRoot != null ? m_handRoot.position : transform.position;
        AudioSource.PlayClipAtPoint(m_activationClip, soundPosition, m_activationVolume);
    }
}
