using UnityEngine;

[CreateAssetMenu(fileName = "NewSurfaceType", menuName = "Climbing/Surface Type")]
public class SurfaceTypeSO : ScriptableObject
{
    [Header("Identité")]
    public string surfaceName = "Standard";
    
    [Header("Propriétés d'escalade")]
    public bool canClimb = true;
    public float gripMultiplier = 1.0f; // 1.0 = normal, 0.5 = difficile

    [Header("Paramètres de Glisse")]
    public bool isSlippery = false;
    public float slideSpeed = 2.5f;     // Vitesse de glissement de la main

    [Header("Paramètres de Maintien Limité")]
    public bool useMaxGrabTime = false; 
    public float maxGrabTime = 1.5f;    // Temps max avant de lâcher de force si activé
    
    [Header("Paramètres de Rebond")]
    public bool hasBounce = false;
    public float bounceForce = 7f; // La puissance de l'impulsion de rebond

    [Header("Audio / Visuel (Optionnel)")]
    public AudioClip grabSound;
    public Material surfaceMaterial;
}