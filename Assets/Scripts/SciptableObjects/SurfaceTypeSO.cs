using UnityEngine;

[CreateAssetMenu(fileName = "NewSurfaceType", menuName = "Climbing/Surface Type")]
public class SurfaceTypeSO : ScriptableObject
{
    [Header("Identité")]
    public string surfaceName = "Standard";
    
    [Header("Propriétés d'escalade")]
    public bool canClimb = true;
    public float gripMultiplier = 1.0f; // Ex: 1.0 = normal, 0.5 = glissant
    
    [Header("Audio / Visuel (Optionnel)")]
    public AudioClip grabSound;
    public Material surfaceMaterial;
}