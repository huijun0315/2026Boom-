using UnityEngine;

[CreateAssetMenu(fileName = "SkinConfig", menuName = "Boom/Skin Config")]
public class SkinConfig : ScriptableObject
{
    [Header("Meta")]
    public string skinId = "skin_1";
    public string displayName = "Skin 1";

    [Header("Cube")]
    public Material cubeBodyMaterial;
    public Material cubeStickerMaterial;
    public AudioClip cubeRotateSfx;

    [Header("Pipe (Reserved)")]
    public Material pipeBaseMaterial;
}
