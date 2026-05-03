using UnityEngine;
using UnityEngine.Rendering;

public static class RuntimeShaderResolver
{
    const string PreferredShaderName = "Puzzle/Color";
    const string PreferredShaderResourcePath = "Shaders/PuzzleColor";
    static bool _loggedMissing;

    public static Shader ResolveColorShader()
    {
        // Resources 路径优先，确保打包后一定可被引用。
        var sh = Resources.Load<Shader>(PreferredShaderResourcePath);
        if (IsValid(sh)) return sh;

        sh = Shader.Find(PreferredShaderName);
        if (IsValid(sh)) return sh;

        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && IsValid(rp.defaultShader)) return rp.defaultShader;

        sh = Shader.Find("Universal Render Pipeline/Lit");
        if (IsValid(sh)) return sh;

        sh = Shader.Find("Standard");
        if (IsValid(sh)) return sh;

        sh = Shader.Find("Unlit/Color");
        if (IsValid(sh)) return sh;

        sh = Shader.Find("Sprites/Default");
        if (IsValid(sh)) return sh;

        sh = Shader.Find("UI/Default");
        if (IsValid(sh)) return sh;

        sh = Shader.Find("Hidden/InternalErrorShader");
        if (!_loggedMissing)
        {
            _loggedMissing = true;
            Debug.LogError("[RuntimeShaderResolver] No supported runtime shader found. Check Graphics/URP settings and build shader stripping.");
        }
        return sh;
    }

    static bool IsValid(Shader sh)
    {
        return sh != null && sh.isSupported;
    }
}
