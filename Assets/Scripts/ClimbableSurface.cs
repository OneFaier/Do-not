using UnityEngine;

public class ClimbableSurface : MonoBehaviour
{
    [SerializeField] private SurfaceTypeSO surfaceType;

    public SurfaceTypeSO SurfaceType => surfaceType;

    public void SetSurfaceType(SurfaceTypeSO newType)
    {
        surfaceType = newType;
        
        // Optionnel : change le matériau visuel de l'objet dynamiquement si défini
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && surfaceType != null && surfaceType.surfaceMaterial != null)
        {
            renderer.material = surfaceType.surfaceMaterial;
        }
    }
}