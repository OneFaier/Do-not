using UnityEngine;

public class HandStamina : MonoBehaviour
{
    [System.Serializable]
    public class HandSettings
    {
        [Tooltip("Durée maximale d'accroche avec une jauge pleine, en secondes.")]
        [Min(0.1f)]
        public float maxHoldTime = 5f;

        [Tooltip("Délai après le lâcher avant de récupérer, en secondes.")]
        [Min(0f)]
        public float regenDelay = 1f;

        [Tooltip("Secondes de stamina récupérées par seconde.")]
        [Min(0f)]
        public float regenSpeed = 2f;

        [Tooltip("Stamina minimale pour reprendre une prise, en secondes.")]
        [Min(0.01f)]
        public float minimumGrabStamina = 0.5f;
    }

    [Header("Réglages main gauche")]
    [SerializeField] private HandSettings leftHand = new HandSettings();

    [Header("Réglages main droite")]
    [SerializeField] private HandSettings rightHand = new HandSettings();

    [Header("Coût du vault")]
    [Tooltip("Stamina consommée sur chaque main lors d'un vault.")]
    [Min(0f)]
    [SerializeField] private float vaultStaminaCost = 1f;

    [Header("Visuels des mains")]
    [SerializeField] private Renderer leftHandRenderer;
    [SerializeField] private Renderer rightHandRenderer;

    [Header("Couleur de fatigue")]
    [SerializeField] private Color exhaustedColor = Color.red;

    [Tooltip("Proportion de stamina sous laquelle le rouge est maximal.")]
    [Range(0f, 0.99f)]
    [SerializeField] private float fullRedThreshold = 0.1f;

    [Tooltip("1 = progression linéaire. Plus élevé = rouge plus tardif.")]
    [Range(0.2f, 4f)]
    [SerializeField] private float colorProgression = 1f;

    [Header("Stamina actuelle — lecture pendant le jeu")]
    [SerializeField] private float leftCurrentStamina;
    [SerializeField] private float rightCurrentStamina;

    private float leftRegenTimer;
    private float rightRegenTimer;
    private bool initialized;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private HandVisual leftVisual;
    private HandVisual rightVisual;

    private class HandVisual
    {
        public Renderer renderer;
        public int colorProperty;
        public Color originalColor;
        public MaterialPropertyBlock propertyBlock;
    }

    public float LeftNormalized
    {
        get
        {
            EnsureInitialized();

            return Mathf.Clamp01(
                leftCurrentStamina / Mathf.Max(0.1f, leftHand.maxHoldTime));
        }
    }

    public float RightNormalized
    {
        get
        {
            EnsureInitialized();

            return Mathf.Clamp01(
                rightCurrentStamina / Mathf.Max(0.1f, rightHand.maxHoldTime));
        }
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        leftVisual = CreateVisual(leftHandRenderer);
        rightVisual = CreateVisual(rightHandRenderer);
    }

    private void LateUpdate()
    {
        UpdateHandColor(leftVisual, LeftNormalized);
        UpdateHandColor(rightVisual, RightNormalized);
    }

    private void EnsureInitialized()
    {
        if (initialized) return;

        leftCurrentStamina = Mathf.Max(0.1f, leftHand.maxHoldTime);
        rightCurrentStamina = Mathf.Max(0.1f, rightHand.maxHoldTime);

        initialized = true;
    }

    // Appelé par OmarPlayer.
    public void Tick(
        bool isLeftGripping,
        bool isRightGripping,
        float deltaTime)
    {
        EnsureInitialized();

        UpdateHand(
            leftHand,
            isLeftGripping,
            ref leftCurrentStamina,
            ref leftRegenTimer,
            deltaTime);

        UpdateHand(
            rightHand,
            isRightGripping,
            ref rightCurrentStamina,
            ref rightRegenTimer,
            deltaTime);
    }

    private void UpdateHand(
        HandSettings settings,
        bool isGripping,
        ref float currentStamina,
        ref float regenTimer,
        float deltaTime)
    {
        float capacity = Mathf.Max(0.1f, settings.maxHoldTime);

        currentStamina = Mathf.Clamp(currentStamina, 0f, capacity);

        if (isGripping)
        {
            // Une seconde accrochée consomme une seconde de stamina.
            currentStamina = Mathf.Max(0f, currentStamina - deltaTime);
            regenTimer = Mathf.Max(0f, settings.regenDelay);
            return;
        }

        float recoveryTime = deltaTime;

        // Attend la fin du délai avant de restaurer la stamina.
        if (regenTimer > 0f)
        {
            float waitingTime = Mathf.Min(regenTimer, deltaTime);

            regenTimer -= waitingTime;
            recoveryTime -= waitingTime;
        }

        currentStamina = Mathf.Min(
            capacity,
            currentStamina
                + Mathf.Max(0f, settings.regenSpeed) * recoveryTime);
    }

    public bool CanGrab(int mouseButton)
    {
        EnsureInitialized();

        HandSettings settings = mouseButton == 0
            ? leftHand
            : rightHand;

        float currentStamina = mouseButton == 0
            ? leftCurrentStamina
            : rightCurrentStamina;

        float requiredStamina = Mathf.Clamp(
            settings.minimumGrabStamina,
            0.01f,
            Mathf.Max(0.1f, settings.maxHoldTime));

        return currentStamina >= requiredStamina;
    }

    public bool IsEmpty(int mouseButton)
    {
        EnsureInitialized();

        return mouseButton == 0
            ? leftCurrentStamina <= 0f
            : rightCurrentStamina <= 0f;
    }

    public void NotifyReleased(int mouseButton)
    {
        EnsureInitialized();

        if (mouseButton == 0)
            leftRegenTimer = Mathf.Max(0f, leftHand.regenDelay);
        else
            rightRegenTimer = Mathf.Max(0f, rightHand.regenDelay);
    }

    public void ConsumeVaultStamina()
    {
        EnsureInitialized();

        float cost = Mathf.Max(0f, vaultStaminaCost);

        if (cost <= 0f) return;

        // Consomme le coût sur les deux mains,
        // même si une seule était accrochée.
        leftCurrentStamina = Mathf.Max(
            0f,
            leftCurrentStamina - cost);

        rightCurrentStamina = Mathf.Max(
            0f,
            rightCurrentStamina - cost);

        // Relance le délai de récupération des deux mains.
        NotifyReleased(0);
        NotifyReleased(1);
    }

    private HandVisual CreateVisual(Renderer handRenderer)
    {
        if (!handRenderer || !handRenderer.sharedMaterial)
            return null;

        Material material = handRenderer.sharedMaterial;

        int property;

        if (material.HasProperty(BaseColorId))
        {
            property = BaseColorId;
        }
        else if (material.HasProperty(ColorId))
        {
            property = ColorId;
        }
        else
        {
            Debug.LogWarning(
                "Le matériau de cette main ne possède pas de couleur compatible.",
                handRenderer);

            return null;
        }

        return new HandVisual
        {
            renderer = handRenderer,
            colorProperty = property,
            originalColor = material.GetColor(property),
            propertyBlock = new MaterialPropertyBlock()
        };
    }

    private void UpdateHandColor(
        HandVisual visual,
        float staminaNormalized)
    {
        if (visual == null || !visual.renderer)
            return;

        float fatigue = 1f - Mathf.InverseLerp(
            fullRedThreshold,
            1f,
            staminaNormalized);

        fatigue = Mathf.Pow(fatigue, colorProgression);

        Color targetColor = Color.Lerp(
            visual.originalColor,
            exhaustedColor,
            fatigue);

        // Conserve l'opacité d'origine.
        targetColor.a = visual.originalColor.a;

        visual.renderer.GetPropertyBlock(visual.propertyBlock);

        visual.propertyBlock.SetColor(
            visual.colorProperty,
            targetColor);

        visual.renderer.SetPropertyBlock(visual.propertyBlock);
    }
}