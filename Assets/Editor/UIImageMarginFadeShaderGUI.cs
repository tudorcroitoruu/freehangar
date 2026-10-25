using UnityEditor;
using UnityEngine;

public class UIImageMarginFadeShaderGUI : ShaderGUI
{
    private const string UseNoiseKeyword = "_USE_PAINTED_EDGE_NOISE";
    private const string AnimateNoiseKeyword = "_ANIMATE_PAINTED_EDGE_NOISE";
    private const string SecondaryNoiseKeyword = "_USE_SECONDARY_PAINTED_EDGE_NOISE";

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUI.BeginChangeCheck();

        DrawMainProperties(materialEditor, properties);
        DrawEdgeProperties(materialEditor, properties);
        DrawNoiseProperties(materialEditor, properties);
        DrawUIProperties(materialEditor, properties);

        if (EditorGUI.EndChangeCheck())
            UpdateMaterialKeywords(materialEditor);

        UpdateMaterialKeywords(materialEditor);
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        base.AssignNewShaderToMaterial(material, oldShader, newShader);
        UpdateKeywords(material);
    }

    private static void UpdateMaterialKeywords(MaterialEditor materialEditor)
    {
        for (int i = 0; i < materialEditor.targets.Length; i++)
        {
            if (materialEditor.targets[i] is Material material)
                UpdateKeywords(material);
        }
    }

    private static void DrawMainProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        BeginSection("Main", "Core UI Image texture, tint, and margin fade target color.");
        DrawTextureProperty(materialEditor, properties, "_MainTex");
        DrawProperty(materialEditor, properties, "_Color");
        DrawProperty(materialEditor, properties, "_FadeColor");
        DrawProperty(materialEditor, properties, "_Saturation");
        EndSection();
    }

    private static void DrawEdgeProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        BeginSection("Edge Fade", "Controls how far each side fades inward and how soft that fade transition is.");
        DrawProperty(materialEditor, properties, "_FadeLeftSize");
        DrawProperty(materialEditor, properties, "_FadeLeftSoftness");
        DrawProperty(materialEditor, properties, "_FadeRightSize");
        DrawProperty(materialEditor, properties, "_FadeRightSoftness");
        DrawProperty(materialEditor, properties, "_FadeTopSize");
        DrawProperty(materialEditor, properties, "_FadeTopSoftness");
        DrawProperty(materialEditor, properties, "_FadeBottomSize");
        DrawProperty(materialEditor, properties, "_FadeBottomSoftness");
        EndSection();
    }

    private static void DrawNoiseProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        BeginSection("Painted Noise", "Adds brush-like irregularity to the fade boundary so the edges do not look perfectly rectangular.");

        MaterialProperty useNoise = FindOptionalProperty("_UseNoise", properties);
        DrawProperty(materialEditor, useNoise);

        if (useNoise == null || (!useNoise.hasMixedValue && useNoise.floatValue <= 0.5f))
        {
            EndSection();
            return;
        }

        DrawTextureProperty(materialEditor, properties, "_NoiseTex");
        DrawProperty(materialEditor, properties, "_NoiseScale");
        DrawProperty(materialEditor, properties, "_NoiseStrength");
        DrawVector2Property(properties, "_NoiseOffset");
        MaterialProperty tornPaper = FindOptionalProperty("_TornPaper", properties);
        DrawProperty(materialEditor, tornPaper);
        if (tornPaper != null && (tornPaper.hasMixedValue || tornPaper.floatValue > 0.5f))
        {
            DrawProperty(materialEditor, properties, "_TornPaperRoughness");
            DrawProperty(materialEditor, properties, "_TornPaperStretch");
            DrawProperty(materialEditor, properties, "_TornPaperSoftness");
            EditorGUILayout.HelpBox("Torn Paper keeps each cut connected to its edge and preserves the interior. Edge Fade Size limits the cut depth; Torn Paper Softness replaces per-edge softness. Set Fade Color alpha to 0 for a transparent cut.", MessageType.Info);
        }
        EndSection();

        BeginSection("Noise Animation", "Moves and pulses the painted noise over time for a subtle living-edge reveal effect.");

        MaterialProperty animateNoise = FindOptionalProperty("_AnimateNoise", properties);
        DrawProperty(materialEditor, animateNoise);

        if (animateNoise != null && (animateNoise.hasMixedValue || animateNoise.floatValue > 0.5f))
        {
            DrawVector2Property(properties, "_NoiseAnimationDirection");
            DrawProperty(materialEditor, properties, "_NoiseAnimationSpeed");
            DrawProperty(materialEditor, properties, "_NoisePulseAmount");
            DrawProperty(materialEditor, properties, "_NoisePulseSpeed");
            DrawProperty(materialEditor, properties, "_FadeWaveAmount");
            DrawProperty(materialEditor, properties, "_FadeWaveSpeed");
            DrawProperty(materialEditor, properties, "_IndependentEdgeAnimation");
            DrawProperty(materialEditor, properties, "_EdgeVariation");
        }
        EndSection();

        BeginSection("Secondary Noise", "Optional second noise layer that adds finer detail over the main painted edge pattern.");

        MaterialProperty secondaryNoise = FindOptionalProperty("_UseSecondaryNoise", properties);
        DrawProperty(materialEditor, secondaryNoise);

        if (secondaryNoise == null || secondaryNoise.hasMixedValue || secondaryNoise.floatValue > 0.5f)
        {
            DrawProperty(materialEditor, properties, "_SecondaryNoiseScale");
            DrawVector2Property(properties, "_SecondaryNoiseSpeed");
            DrawProperty(materialEditor, properties, "_SecondaryNoiseBlend");
        }

        EndSection();
    }

    private static void DrawUIProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        BeginSection("UI Masking", "Compatibility properties used by Unity UI masks, stencil masks, RectMask2D, and alpha clipping.");
        DrawProperty(materialEditor, properties, "_StencilComp");
        DrawProperty(materialEditor, properties, "_Stencil");
        DrawProperty(materialEditor, properties, "_StencilOp");
        DrawProperty(materialEditor, properties, "_StencilWriteMask");
        DrawProperty(materialEditor, properties, "_StencilReadMask");
        DrawProperty(materialEditor, properties, "_ColorMask");
        DrawProperty(materialEditor, properties, "_UseUIAlphaClip");
        EndSection();
    }

    private static void BeginSection(string title, string tooltip)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.boldLabel);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private static void DrawProperty(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName)
    {
        DrawProperty(materialEditor, FindOptionalProperty(propertyName, properties));
    }

    private static void DrawProperty(MaterialEditor materialEditor, MaterialProperty property)
    {
        if (property == null)
            return;

        materialEditor.ShaderProperty(property, Content(property));
    }

    private static void DrawTextureProperty(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName)
    {
        MaterialProperty property = FindOptionalProperty(propertyName, properties);
        if (property == null)
            return;

        materialEditor.TexturePropertySingleLine(Content(property), property);
    }

    private static void DrawVector2Property(MaterialProperty[] properties, string propertyName)
    {
        MaterialProperty property = FindOptionalProperty(propertyName, properties);
        if (property == null)
            return;

        EditorGUI.showMixedValue = property.hasMixedValue;
        Vector4 value = property.vectorValue;
        EditorGUI.BeginChangeCheck();
        Vector2 newValue = EditorGUILayout.Vector2Field(Content(property), new Vector2(value.x, value.y));
        if (EditorGUI.EndChangeCheck())
            property.vectorValue = new Vector4(newValue.x, newValue.y, value.z, value.w);
        EditorGUI.showMixedValue = false;
    }

    private static GUIContent Content(MaterialProperty property)
    {
        return new GUIContent(property.displayName, Tooltip(property.name));
    }

    private static string Tooltip(string propertyName)
    {
        switch (propertyName)
        {
            case "_MainTex":
                return "Sprite texture sampled by the shader. For UI Image this is normally provided by the Image Source Image, not manually assigned on the material.";
            case "_Color":
                return "Multiplies the UI Image sprite color. The alpha includes Image tint and CanvasGroup alpha.";
            case "_FadeColor":
                return "Target color at the faded margins. Alpha 0 fades to transparent; alpha 1 fades to this visible color.";
            case "_Saturation":
                return "Controls sprite color intensity. 0 makes the image black and white; 1 keeps full original color.";
            case "_FadeLeftSize":
                return "Width of the left fade band in normalized sprite UVs. Set to 0 to disable the left fade.";
            case "_FadeLeftSoftness":
                return "Smoothness of the left fade transition. Higher values spread the fade more softly.";
            case "_FadeRightSize":
                return "Width of the right fade band in normalized sprite UVs. Set to 0 to disable the right fade.";
            case "_FadeRightSoftness":
                return "Smoothness of the right fade transition. Higher values spread the fade more softly.";
            case "_FadeTopSize":
                return "Width of the top fade band in normalized sprite UVs. Set to 0 to disable the top fade.";
            case "_FadeTopSoftness":
                return "Smoothness of the top fade transition. Higher values spread the fade more softly.";
            case "_FadeBottomSize":
                return "Width of the bottom fade band in normalized sprite UVs. Set to 0 to disable the bottom fade.";
            case "_FadeBottomSoftness":
                return "Smoothness of the bottom fade transition. Higher values spread the fade more softly.";
            case "_UseNoise":
                return "Enables painted-edge noise. When off, the shader uses a clean smooth rectangular fade.";
            case "_NoiseTex":
                return "Noise texture used to roughen the fade boundary. Grayscale soft noise works best.";
            case "_NoiseScale":
                return "Controls noise pattern size. Lower values make larger brushy shapes; higher values add finer detail.";
            case "_NoiseStrength":
                return "How much the noise distorts the fade width. Keep low for subtle painted edges.";
            case "_NoiseOffset":
                return "Manual 2D offset for the noise pattern, useful to art-direct static edge placement.";
            case "_TornPaper":
                return "Samples noise only along each edge to create a continuous torn-paper contour without adding interior holes. Requires Use Noise. Existing sprite transparency is preserved.";
            case "_TornPaperRoughness":
                return "Contour irregularity, multiplied by Noise Strength. Raise both for deeper tears. Edge Fade Size bounds the affected band.";
            case "_TornPaperSoftness":
                return "Width of the contour transition relative to each edge band. Low values create a sharp paper cut; higher values soften it. Replaces per-edge softness in Torn Paper mode.";
            case "_TornPaperStretch":
                return "Stretches tears along the edge: vertically on left/right, horizontally on top/bottom. 1 keeps the original scale; larger values make longer, broader tears, smaller values add finer tears. Applies to both noise layers without changing cut depth.";
            case "_AnimateNoise":
                return "Enables and reveals animated painted-edge controls.";
            case "_NoiseAnimationDirection":
                return "2D direction in which the main noise pattern drifts while animated.";
            case "_NoiseAnimationSpeed":
                return "Speed of the main noise drift. Small values keep the edge motion subtle.";
            case "_NoisePulseAmount":
                return "Amount by which noise strength pulses over time.";
            case "_NoisePulseSpeed":
                return "Speed of the noise-strength pulse.";
            case "_FadeWaveAmount":
                return "Small animated expansion and contraction of each fade width.";
            case "_FadeWaveSpeed":
                return "Speed of the fade-width wave.";
            case "_IndependentEdgeAnimation":
                return "Offsets animation timing per edge so all four sides do not move identically.";
            case "_EdgeVariation":
                return "How different each side animation phase is when independent edge animation is enabled.";
            case "_UseSecondaryNoise":
                return "Adds a second noise layer for richer watercolor, brush, or torn-paper variation.";
            case "_SecondaryNoiseScale":
                return "Scale of the secondary noise layer. Usually higher than the main scale for extra detail.";
            case "_SecondaryNoiseSpeed":
                return "2D drift speed of the secondary noise layer.";
            case "_SecondaryNoiseBlend":
                return "How much the secondary noise layer contributes to the final painted edge.";
            case "_StencilComp":
                return "Unity UI mask stencil comparison mode. Usually controlled by UI Mask components.";
            case "_Stencil":
                return "Unity UI stencil reference value. Usually controlled by UI Mask components.";
            case "_StencilOp":
                return "Unity UI stencil operation. Usually controlled by UI Mask components.";
            case "_StencilWriteMask":
                return "Stencil bits this material may write. Usually leave at Unity UI defaults.";
            case "_StencilReadMask":
                return "Stencil bits this material may read. Usually leave at Unity UI defaults.";
            case "_ColorMask":
                return "Color channels written by the shader. 15 writes RGBA and is the normal UI value.";
            case "_UseUIAlphaClip":
                return "Enables alpha clipping for Unity UI. Leave off unless a mask workflow needs hard alpha discard.";
            default:
                return propertyName;
        }
    }

    private static MaterialProperty FindOptionalProperty(string propertyName, MaterialProperty[] properties)
    {
        for (int i = 0; i < properties.Length; i++)
        {
            if (properties[i] != null && properties[i].name == propertyName)
                return properties[i];
        }

        return null;
    }

    private static void UpdateKeywords(Material material)
    {
        SetKeyword(material, UseNoiseKeyword, IsEnabled(material, "_UseNoise"));
        SetKeyword(material, AnimateNoiseKeyword, IsEnabled(material, "_AnimateNoise"));
        SetKeyword(material, SecondaryNoiseKeyword, IsEnabled(material, "_UseSecondaryNoise"));
    }

    private static bool IsEnabled(Material material, string propertyName)
    {
        return material != null && material.HasProperty(propertyName) && material.GetFloat(propertyName) > 0.5f;
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
