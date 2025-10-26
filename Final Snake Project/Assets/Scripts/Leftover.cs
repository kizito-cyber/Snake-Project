using UnityEngine;
using System.Collections;
using DG.Tweening;

[RequireComponent(typeof(Collider2D))]
public class Leftover : MonoBehaviour
{
    public int value = 1;
    [HideInInspector] public int leftoverId;
    public int ownerActor;

    private Collider2D col2d;
    public FoodData foodData; // your existing struct/class
    private Vector3 initialScale;
    public LeftoverVFX vfx;

    [Header("Fade")]
    [Tooltip("How long the leftover fades out when its lifetime expires.")]
    public float fadeDuration = 1.2f;
    [Tooltip("How much to scale down during fade (multiplies current scale).")]
    public float fadeScaleMultiplier = 0.6f;

    [Header("Lifetime jitter (stagger fades)")]
    [Tooltip("Multiply base lifetime by a random value between min and max (e.g. 0.85 - 1.15).")]
    public float lifetimeMultiplierMin = 0.85f;
    public float lifetimeMultiplierMax = 1.15f;
    [Tooltip("Add an extra random delay in seconds (0..value) before fade starts.")]
    public float additionalLifetimeJitter = 0.6f;

    void Awake()
    {
        col2d = GetComponent<Collider2D>();
    }

    void OnEnable()
    {
        // pick a random uniform scale from foodData range
        float uniform = Random.Range(foodData.minScale.x, foodData.maxScale.x);
        initialScale = Vector3.one * uniform;

        // apply initial random scale. Later Initialize(...) will multiply further by 'value' factor if used.
        transform.localScale = initialScale;

        // restore visuals (in case reused from pool after fading)
        ResetVisuals();

        // make sure VFX has out-of-sync breathing
        if (vfx != null)
        {
            vfx.SetRandomPhase();
            vfx.RefreshBaseLocalScales(); // ensure child base scales are correct
        }

        // gentle root pulse (whole object) - this scales the root; VFX applies relative child pulsing on top
        AnimateRootPulse(transform.localScale);

        // start auto-return (now with per-instance jitter so fades don't all happen at once)
        StartCoroutine(AutoReturn());
    }

    void OnDisable()
    {
        StopAllCoroutines();

        // kill tweens on transform and sprite renderers to avoid lingering tweens
        transform.DOKill();
        if (vfx != null)
        {
            if (vfx.body != null) DOTween.Kill(vfx.body);
            if (vfx.halo != null) DOTween.Kill(vfx.halo);
        }
    }

    private void AnimateRootPulse(Vector3 orig)
    {
        // small yoyo scale on the root transform
        transform
          .DOScale(orig * 1.08f, 1f)
          .SetLoops(-1, LoopType.Yoyo)
          .SetEase(Ease.InOutSine);
    }

    IEnumerator AutoReturn()
    {
        // compute a jittered lifetime so each leftover fades at a slightly different time
        float multiplier = Random.Range(lifetimeMultiplierMin, lifetimeMultiplierMax);
        float jitter = Random.Range(0f, additionalLifetimeJitter);

        float waitTime = foodData.lifetime * multiplier + jitter;

        // safety clamp so it never becomes zero or negative
        waitTime = Mathf.Max(0.05f, waitTime);

        yield return new WaitForSeconds(waitTime);

        // start fade sequence
        yield return StartCoroutine(FadeThenReturn());
    }

    private IEnumerator FadeThenReturn()
    {
        // disable collider so it can't be picked up during fade
        if (col2d != null) col2d.enabled = false;

        // stop particle emission so burst doesn't keep emitting during fade
        if (vfx != null && vfx.burst != null)
            vfx.burst.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        // get sprite renderers to fade (fallback to a root SpriteRenderer if vfx missing)
        SpriteRenderer bodySR = (vfx != null && vfx.body != null) ? vfx.body : GetComponent<SpriteRenderer>();
        SpriteRenderer haloSR = (vfx != null && vfx.halo != null) ? vfx.halo : null;

        // build a DOTween sequence: fade body + halo and shrink root
        Sequence seq = DOTween.Sequence();
        if (bodySR != null) seq.Join(bodySR.DOFade(0f, fadeDuration));
        if (haloSR != null) seq.Join(haloSR.DOFade(0f, fadeDuration));
        seq.Join(transform.DOScale(transform.localScale * fadeScaleMultiplier, fadeDuration).SetEase(Ease.InOutQuad));

        seq.Play();

        // wait for completion
        yield return seq.WaitForCompletion();

        // finally return to pool (pool manager will deactivate the GameObject)
        LeftoverPoolManager.Instance.ReturnLeftover(leftoverId);
    }

    /// <summary>
    /// Configure the leftover (value) and tint. This multiplies the existing random base scale
    /// so both 'random spawn size' and 'value-based size' apply together.
    /// </summary>
    public void Initialize(int value, Color tint)
    {
        this.value = value;

        // scale based on value (keeps original random variation from OnEnable)
        float valueScale = 0.6f + Mathf.Clamp01(value / 5f) * 0.9f; // tune to taste
        transform.localScale = transform.localScale * valueScale;

        // tell VFX to play spawn pop + breathing (PlaySpawn uses a short pop then loops)
        if (vfx != null)
            vfx.PlaySpawn(tint, spawnPopScale: 0.7f);
    }

    /// <summary>
    /// Restore alpha, scale, and collider when reusing from the pool.
    /// Called from OnEnable so re-used leftovers become visible again.
    /// </summary>
    public void ResetVisuals()
    {
        // restore root scale
        transform.localScale = initialScale;

        // restore sprite alphas
        if (vfx != null)
        {
            if (vfx.body != null)
            {
                Color c = vfx.body.color;
                c.a = 1f;
                vfx.body.color = c;
            }
            if (vfx.halo != null)
            {
                Color c = vfx.halo.color;
                c.a = 0.8f; // match PlaySpawn default halo alpha
                vfx.halo.color = c;
            }

            // optionally restart particle emission if desired (we usually let PlaySpawn handle it)
            // if (vfx.burst != null) vfx.burst.Play();
        }
        else
        {
            // fallback: if you have a root SpriteRenderer, ensure alpha restored
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 1f;
                sr.color = c;
            }
        }

        // re-enable collider so it can be picked up again
        if (col2d != null) col2d.enabled = true;
    }
}
