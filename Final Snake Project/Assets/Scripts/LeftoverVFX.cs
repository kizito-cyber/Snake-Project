using UnityEngine;
using System.Collections;

[RequireComponent(typeof(ParticleSystem))]
public class LeftoverVFX : MonoBehaviour
{
    [Header("References")]
    public SpriteRenderer body;    // main circle (child)
    public SpriteRenderer halo;    // optional additive halo sprite
    public ParticleSystem burst;

    [Header("Pulse (runtime)")]
    [Tooltip("How big the subtle breathing is (relative).")]
    public float pulseAmplitude = 0.12f;
    [Tooltip("How fast the breathing is.")]
    public float pulseSpeed = 2.0f;

    // internal
    private float _bodyBaseLocal = 1f;   // base local scale of the body sprite (child)
    private float _haloBaseLocal = 1f;
    private float _phase = 0f;           // random phase so instances are out of sync
    private float _targetPulseAmplitude; // target amplitude (used by spawn coroutine)
    private Coroutine _spawnAmpRoutine;

    void Awake()
    {
        // local base scales (these are *child* local scales, not the parent's transform)
        if (body != null) _bodyBaseLocal = body.transform.localScale.x;
        if (halo != null) _haloBaseLocal = halo.transform.localScale.x;

        // random phase so multiple leftovers don't breathe together
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _targetPulseAmplitude = pulseAmplitude;
    }

    /// <summary>
    /// Call to configure color & play the spawn VFX.
    /// Optional spawnPopScale sets an initial pop scale multiplier applied to the child's local scale (0-1 for small pop).
    /// </summary>
    public void PlaySpawn(Color color, float spawnPopScale = 0.7f)
    {
        if (body != null) body.color = color;
        if (halo != null) halo.color = new Color(color.r, color.g, color.b, 0.8f);

        // tint particle system
        var main = burst.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);

        burst.Play();

        // tiny pop scale applied to child local scale (we lerp it up in ScaleUpRoutine)
        if (body != null)
            body.transform.localScale = Vector3.one * (_bodyBaseLocal * spawnPopScale);

        StopAllCoroutines();
        StartCoroutine(ScaleUpRoutine());

        // temporary stronger pulse right after spawn, then ease back to normal subtle breathing
        if (_spawnAmpRoutine != null) StopCoroutine(_spawnAmpRoutine);
        _spawnAmpRoutine = StartCoroutine(SpawnPulseRoutine());
    }

    private IEnumerator ScaleUpRoutine()
    {
        float t = 0f;
        float duration = 0.12f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0.7f, 1f, t / duration);
            if (body != null) body.transform.localScale = Vector3.one * (_bodyBaseLocal * s);
            yield return null;
        }
    }

    // temporary pulse amplitude bump then ease back
    private IEnumerator SpawnPulseRoutine()
    {
        float startAmp = pulseAmplitude * 2.0f; // a stronger short pulse on spawn
        float endAmp = _targetPulseAmplitude;
        float time = 0f;
        float dur = 0.35f;
        while (time < dur)
        {
            time += Time.deltaTime;
            pulseAmplitude = Mathf.Lerp(startAmp, endAmp, time / dur);
            yield return null;
        }

        pulseAmplitude = _targetPulseAmplitude;
    }

    void Update()
    {
        // We apply pulse to the child local scale only (so parent/root scaling remains authoritative).
        // That avoids collisions with DOTween scaling on the root.
        if (body != null)
        {
            float m = 1f + Mathf.Sin((Time.time + _phase) * pulseSpeed) * pulseAmplitude * 0.5f;
            body.transform.localScale = Vector3.one * (_bodyBaseLocal * m);
        }

        if (halo != null)
        {
            // halo breathes slightly larger and linked to same phase
            float haloM = 1.0f + pulseAmplitude * 2f * Mathf.Abs(Mathf.Sin((Time.time + _phase) * pulseSpeed * 0.5f));
            halo.transform.localScale = Vector3.one * (_haloBaseLocal * haloM);
        }
    }

    /// <summary>
    /// If you want to randomize phase externally (optional).
    /// </summary>
    public void SetRandomPhase()
    {
        _phase = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// If you change the body/halo child scale at runtime, call this to refresh base values.
    /// </summary>
    public void RefreshBaseLocalScales()
    {
        if (body != null) _bodyBaseLocal = body.transform.localScale.x;
        if (halo != null) _haloBaseLocal = halo.transform.localScale.x;
    }
}
