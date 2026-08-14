using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class ToiletLightFlickerMoths : MonoBehaviour
{
    [SerializeField] private Light targetLight;
    [SerializeField] private Material mothMaterial;
    [SerializeField] private Transform mothOrigin;
    [SerializeField] private Vector3 mothLocalOffset = Vector3.zero;
    [SerializeField] private Vector2 flickerInterval = new Vector2(0.06f, 0.24f);
    [SerializeField] private Vector2 intensityMultiplier = new Vector2(0.35f, 1.08f);
    [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.08f;
    [SerializeField, Min(0f)] private float intensityLerpSpeed = 14f;
    [SerializeField] private Color mothColor = new Color(1f, 0.9f, 0.58f, 0.95f);

    private ParticleSystem moths;
    private float baseIntensity;
    private float nextFlickerTime;
    private float targetIntensity;
#if UNITY_EDITOR
    private bool editorRefreshQueued;
#endif

    private void OnEnable()
    {
        EnsureLight();

        if (Application.isPlaying)
        {
            EnsureMoths();
            InitializeFlicker();
            return;
        }

        QueueEditorRefresh();
    }

    private void Awake()
    {
        EnsureLight();

        if (Application.isPlaying)
        {
            EnsureMoths();
            InitializeFlicker();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (targetLight == null)
        {
            return;
        }

        if (Time.time >= nextFlickerTime)
        {
            ChooseNextIntensity();
            ScheduleNextFlicker();
        }

        targetLight.intensity = Mathf.Lerp(
            targetLight.intensity,
            targetIntensity,
            Time.deltaTime * intensityLerpSpeed);
    }

    private void OnDisable()
    {
        if (targetLight != null && baseIntensity > 0f)
        {
            targetLight.intensity = baseIntensity;
        }
    }

    private void OnValidate()
    {
        EnsureLight();
        flickerInterval.x = Mathf.Max(0.01f, flickerInterval.x);
        flickerInterval.y = Mathf.Max(flickerInterval.x, flickerInterval.y);
        intensityMultiplier.x = Mathf.Max(0f, intensityMultiplier.x);
        intensityMultiplier.y = Mathf.Max(intensityMultiplier.x, intensityMultiplier.y);
        intensityLerpSpeed = Mathf.Max(0f, intensityLerpSpeed);
        QueueEditorRefresh();
    }

    private void InitializeFlicker()
    {
        if (targetLight == null)
        {
            enabled = false;
            return;
        }

        baseIntensity = targetLight.intensity;
        targetIntensity = baseIntensity;
        ScheduleNextFlicker();
    }

    private void EnsureLight()
    {
        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
        }
    }

    private void EnsureMoths()
    {
        Transform mothTransform = mothOrigin != null ? mothOrigin : transform.Find("Moths");
        if (mothTransform == null)
        {
            GameObject mothObject = new GameObject("Moths");
            mothTransform = mothObject.transform;
            mothTransform.SetParent(transform, false);
            mothTransform.localPosition = mothLocalOffset;
        }

        mothOrigin = mothTransform;
        mothTransform.localPosition = mothLocalOffset;
        moths = mothTransform.GetComponent<ParticleSystem>();
        if (moths == null)
        {
            moths = mothTransform.gameObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer renderer = mothTransform.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            renderer = mothTransform.gameObject.AddComponent<ParticleSystemRenderer>();
        }

        ConfigureMoths(renderer);
    }

    private void ConfigureMoths(ParticleSystemRenderer renderer)
    {
        if (moths.isPlaying)
        {
            moths.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ParticleSystem.MainModule main = moths.main;
        main.duration = 6f;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 3.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-35f * Mathf.Deg2Rad, 35f * Mathf.Deg2Rad);
        main.startColor = mothColor;
        main.gravityModifier = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 26;

        ParticleSystem.EmissionModule emission = moths.emission;
        emission.enabled = true;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = moths.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = 0.42f;
        shape.donutRadius = 0.16f;

        ParticleSystem.VelocityOverLifetimeModule velocity = moths.velocityOverLifetime;
        velocity.enabled = true;
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
        velocity.radial = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(0.75f, 1.35f);

        ParticleSystem.NoiseModule noise = moths.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 1.9f;
        noise.scrollSpeed = 0.75f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = moths.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 0.72f), 0f),
                new GradientColorKey(mothColor, 0.45f),
                new GradientColorKey(new Color(0.42f, 0.32f, 0.18f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.15f),
                new GradientAlphaKey(0.86f, 0.82f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.TrailModule trails = moths.trails;
        trails.enabled = false;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 0.18f;
        renderer.sharedMaterial = mothMaterial;

        if (!moths.isPlaying)
        {
            moths.Play();
        }
    }

    private void QueueEditorRefresh()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || editorRefreshQueued)
        {
            return;
        }

        editorRefreshQueued = true;
        UnityEditor.EditorApplication.delayCall += RefreshEditorPreview;
#endif
    }

#if UNITY_EDITOR
    private void RefreshEditorPreview()
    {
        editorRefreshQueued = false;

        if (this == null || Application.isPlaying)
        {
            return;
        }

        EnsureLight();
        EnsureMoths();
    }
#endif

    private void ChooseNextIntensity()
    {
        if (Random.value < blackoutChance)
        {
            targetIntensity = baseIntensity * Random.Range(0.02f, 0.08f);
            return;
        }

        targetIntensity = baseIntensity * Random.Range(intensityMultiplier.x, intensityMultiplier.y);
    }

    private void ScheduleNextFlicker()
    {
        nextFlickerTime = Time.time + Random.Range(flickerInterval.x, flickerInterval.y);
    }
}
