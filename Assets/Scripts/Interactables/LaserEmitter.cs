using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserEmitter : MonoBehaviour, IActivatable, IResettable
{
    [Header("Laser")]
    public Transform origin;
    public Transform directionReference;
    public float maxDistance = 30f;
    public LayerMask hitMask = ~0;
    public bool startsEnabled = true;
    public bool killPlayer = true;

    [Header("Visual")]
    public LineRenderer lineRenderer;
    public float lineWidth = 0.05f;

    private bool laserEnabled;
    private bool initialEnabled;
    private readonly RaycastHit[] hits = new RaycastHit[32];

    public bool IsActive => laserEnabled;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (origin == null)
            origin = transform;

        if (directionReference == null)
            directionReference = origin;

        ConfigureLineRenderer();
    }

    private void Start()
    {
        CaptureInitialState();
        laserEnabled = startsEnabled;
    }

    private void Update()
    {
        UpdateLaser();
    }

    public void EnableLaser()
    {
        Activate();
    }

    public void DisableLaser()
    {
        Deactivate();
    }

    public void Activate()
    {
        laserEnabled = true;
        if (lineRenderer != null)
            lineRenderer.enabled = true;
    }

    public void Deactivate()
    {
        laserEnabled = false;
        if (lineRenderer != null)
            lineRenderer.enabled = false;
    }

    public void Toggle()
    {
        if (laserEnabled)
            Deactivate();
        else
            Activate();
    }

    public void CaptureInitialState()
    {
        initialEnabled = startsEnabled;
    }

    public void ResetState()
    {
        laserEnabled = initialEnabled;

        if (lineRenderer != null)
            lineRenderer.enabled = laserEnabled;
    }

    private void UpdateLaser()
    {
        if (lineRenderer == null)
            return;

        if (!laserEnabled)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;

        Vector3 start = origin != null ? origin.position : transform.position;
        Vector3 direction = directionReference != null ? directionReference.forward : transform.forward;
        Vector3 end = start + direction.normalized * maxDistance;

        int hitCount = Physics.RaycastNonAlloc(start, direction, hits, maxDistance, hitMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (TryKillPlayer(hit.collider))
            {
                end = hit.point;
                break;
            }

            if (TryFindLaserBlocker(hit.collider, out ILaserBlocker blocker))
            {
                if (blocker.CanBlockLaser)
                {
                    end = hit.point;
                    break;
                }

                continue;
            }

            if (hit.collider.GetComponentInParent<MovableObject>() != null)
                continue;

            end = hit.point;
            break;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    private bool TryKillPlayer(Collider collider)
    {
        if (!killPlayer)
            return false;

        PlayerRespawn player = collider.GetComponentInParent<PlayerRespawn>();
        if (player == null)
            return false;

        player.Die();
        return true;
    }

    private static bool TryFindLaserBlocker(Collider collider, out ILaserBlocker blocker)
    {
        LaserBlocker laserBlocker = collider.GetComponentInParent<LaserBlocker>();
        if (laserBlocker != null)
        {
            blocker = laserBlocker;
            return true;
        }

        MovableObject movableObject = collider.GetComponentInParent<MovableObject>();
        if (movableObject != null)
        {
            blocker = movableObject;
            return true;
        }

        blocker = null;
        return false;
    }

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.positionCount = 2;
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
