using UnityEngine;
using UnityEngine.UI;

public class ClimbingCrosshair : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private OmarPlayer climbingController;
    [SerializeField] private Graphic crosshairGraphic;

    [Header("Couleurs")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color inRangeColor = Color.green;

    private void Update()
    {
        if (!climbingController || !crosshairGraphic) return;

        crosshairGraphic.color = climbingController.HasGrabbableTarget()
            ? inRangeColor
            : defaultColor;
    }
}