GrainTypesValidation.Run();
GrainSpatialValidation.Run();
var shader=Shader.Find("Freehangar/HDRP/Grain Kernels");
var messages=UnityEditor.ShaderUtil.GetShaderMessages(shader);
UnityEditor.SceneView.RepaintAll();
return "Mixed-grain and spatial validation passed. Shader diagnostics: "+string.Join(";",messages.Select(m=>m.severity+":"+m.message));
