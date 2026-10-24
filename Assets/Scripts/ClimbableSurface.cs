using UnityEngine;

[ExecuteAlways]
public class ClimbableSurface : MonoBehaviour
{
    [SerializeField] private SurfaceTypeSO surfaceType;

    public SurfaceTypeSO SurfaceType => surfaceType;

    // S'exécute automatiquement dans l'éditeur Unity dès qu'on modifie une valeur
    private void OnValidate()
    {
        UpdateVisuals();
    }

    public void SetSurfaceType(SurfaceTypeSO newType)
    {
        surfaceType = newType;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && surfaceType != null && surfaceType.surfaceMaterial != null)
        {
            // On utilise sharedMaterial en mode Éditeur pour éviter de dupliquer les matériaux en mémoire
            renderer.sharedMaterial = surfaceType.surfaceMaterial;
        }
    }
}