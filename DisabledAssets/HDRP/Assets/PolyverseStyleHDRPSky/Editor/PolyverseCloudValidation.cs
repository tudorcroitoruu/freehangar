using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Liviu.Environment.HDRP.Editor
{
    internal static class PolyverseCloudValidation
    {
        [MenuItem("Tools/Polyverse Style Sky/Preview Clouds Overhead")]
        private static void PreviewOverhead()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            var viewpoint = new GameObject("Overhead cloud preview") { hideFlags = HideFlags.HideAndDontSave };
            viewpoint.transform.SetPositionAndRotation(view.camera.transform.position, Quaternion.Euler(-90f, 0f, 0f));
            view.AlignViewToObject(viewpoint.transform);
            UnityEngine.Object.DestroyImmediate(viewpoint);
            view.sceneViewState.alwaysRefresh = true;
            view.Repaint();
        }

        [MenuItem("Tools/Polyverse Style Sky/Validate Cloud Animation")]
        private static void Validate()
        {
            var shader = Shader.Find("Hidden/HDRP/Sky/PolyverseStyleSky");
            var material = new Material(shader);
            var target = RenderTexture.GetTemporary(256, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            try
            {
                material.SetFloat("_SkyIntensity", 1f);
                material.SetColor("_SkyColor", Color.black);
                material.SetColor("_EquatorColor", Color.black);
                material.SetColor("_GroundColor", Color.black);
                material.SetFloat("_EquatorHeight", 0.5f);
                material.SetFloat("_EquatorSmoothness", 0.5f);
                material.SetVector("_SkyboxRotationAxis", Vector3.up);
                material.SetInt("_EnableClouds", 1);
                material.SetFloat("_CloudsOpacity", 1f);
                material.SetFloat("_CloudsFade", 1f);
                material.SetFloat("_CloudsHorizonFade", 0.1f);
                material.SetVector("_CloudsLayer", new Vector4(1000f, 3000f, 8000f, 0.35f));
                material.SetFloat("_CloudsBreakup", 0.8f);
                material.SetFloat("_CloudsMaxHeight", 90f);
                material.SetVector("_CloudsRotationAxis", Vector3.up);
                material.SetColor("_CloudsLightColor", Color.white);
                material.SetColor("_CloudsShadowColor", Color.white);
                material.SetVector("_CloudsSunDirection", new Vector3(-0.6f, 0.7f, 0.35f).normalized);
                material.SetVector("_CloudsSunTint", Vector3.one);
                material.SetVector("_CloudsLighting", new Vector4(0.9f, 0.18f, 0.45f, 0.18f));
                var matrix = Matrix4x4.zero;
                matrix.m00 = 2f / target.width;
                matrix.m11 = -1.2f / target.height;
                matrix.m20 = -1f;
                matrix.m21 = -0.05f;
                matrix.m22 = 1f;
                material.SetMatrix("_PixelCoordToViewDirWS", matrix);
                material.SetVector("_CloudsShape", new Vector4(0.45f, 3f, 0.08f, 0.35f));
                material.SetVector("_CloudsAnimation", new Vector4(0, 0, 0.025f, 0.35f));
                var first = Render(material, target, 0f, "clouds-evolution-0");
                var evolved = Render(material, target, 20f, "clouds-evolution-20");
                float evolution = Difference(first, evolved);

                // Verify every procedural-cloud pixel follows the same global opacity and fade.
                material.SetFloat("_CloudsOpacity", 0.5f);
                float opacityError = MaximumScaledDifference(first, Render(material, target, 0f), 0.5f);
                material.SetFloat("_CloudsOpacity", 1f);
                material.SetFloat("_CloudsFade", 0.5f);
                float fadeError = MaximumScaledDifference(first, Render(material, target, 0f), 0.5f);
                material.SetFloat("_CloudsFade", 0f);
                float zeroFade = Average(Render(material, target, 0f));
                material.SetFloat("_CloudsFade", 1f);
                material.SetFloat("_CloudsOpacity", 0f);
                float zeroOpacity = Average(Render(material, target, 0f));
                material.SetFloat("_CloudsOpacity", 1f);

                material.SetVector("_CloudsAnimation", new Vector4(0.003f, 0.001f, 0, 0.35f));
                var windStart = Render(material, target, 0f);
                var windEnd = Render(material, target, 20f);
                float wind = Difference(windStart, windEnd);
                material.SetVector("_CloudsAnimation", new Vector4(0, 0, 0, 0.35f));
                float frozen = Difference(Render(material, target, 0f), Render(material, target, 20f));
                material.SetInt("_EnableCloudsRotation", 1);
                material.SetFloat("_CloudsRotationSpeed", 2f);
                float rotation = Difference(Render(material, target, 0f), Render(material, target, 20f));
                material.SetInt("_EnableCloudsRotation", 0);

                // Full coverage isolates the height mask from random cloud distribution.
                material.SetVector("_CloudsShape", new Vector4(1, 3f, 0.08f, 0.35f));
                material.SetFloat("_CloudsMaxHeight", 20f);
                var heightMatrix = Matrix4x4.zero;
                heightMatrix.m20 = 1f;
                heightMatrix.m21 = -Mathf.Tan(10f * Mathf.Deg2Rad);
                material.SetMatrix("_PixelCoordToViewDirWS", heightMatrix);
                float horizonBand = Average(Render(material, target, 0f));
                heightMatrix.m21 = -Mathf.Tan(30f * Mathf.Deg2Rad);
                material.SetMatrix("_PixelCoordToViewDirWS", heightMatrix);
                float aboveLimit = Average(Render(material, target, 0f));
                heightMatrix.m20 = 0f;
                heightMatrix.m21 = -1f;
                material.SetMatrix("_PixelCoordToViewDirWS", heightMatrix);
                float limitedZenith = Average(Render(material, target, 0f));
                material.SetFloat("_CloudsMaxHeight", 0f);
                material.SetMatrix("_PixelCoordToViewDirWS", matrix);
                float zeroHeight = Average(Render(material, target, 0f));
                material.SetFloat("_CloudsMaxHeight", 90f);

                material.SetVector("_CloudsShape", new Vector4(0.8f, 3f, 0.065f, 0.65f));
                material.SetVector("_CloudsAnimation", new Vector4(0, 0, 0.025f, 0.35f));
                var topMatrix = Matrix4x4.zero;
                topMatrix.m00 = 5f / target.width;
                topMatrix.m12 = 5f / target.height;
                topMatrix.m20 = -2.5f;
                topMatrix.m21 = -1f;
                topMatrix.m22 = -2.5f;
                material.SetMatrix("_PixelCoordToViewDirWS", topMatrix);
                var topStart = Render(material, target, 0f, "clouds-zenith-0");
                var topEnd = Render(material, target, 20f, "clouds-zenith-20");
                float zenithCoverage = Average(topStart);
                material.SetFloat("_CloudsBreakup", 0f);
                var intact = Render(material, target, 20f);
                material.SetFloat("_CloudsBreakup", 1f);
                float breakupDifference = Difference(intact, Render(material, target, 20f));
                material.SetFloat("_CloudsBreakup", 0.8f);
                int erodedPixels = 0;
                int retainedPixels = 0;
                for (int pixel = 0; pixel < topStart.Length; pixel++)
                {
                    if (topStart[pixel].r > 192 && topEnd[pixel].r < 64) erodedPixels++;
                    if (topStart[pixel].r > 192 && topEnd[pixel].r > 192) retainedPixels++;
                }
                material.SetVector("_CloudsAnimation", new Vector4(0, 0, 0, 0.35f));
                // Cross X/Z cell boundaries from both sides. No latitude/longitude grid remains.
                float boundaryJump = 0f;
                const float cellSize = 500f; // altitude * 1.5 / scale
                for (int axis = 0; axis < 2; axis++)
                {
                    for (int boundary = -6; boundary <= 6; boundary++)
                    {
                        var boundaryMatrix = Matrix4x4.zero;
                        boundaryMatrix.m21 = -1000f;
                        if (axis == 0)
                        {
                            boundaryMatrix.m12 = 8000f / target.height;
                            boundaryMatrix.m22 = -4000f;
                            boundaryMatrix.m20 = boundary * cellSize - 0.01f;
                        }
                        else
                        {
                            boundaryMatrix.m10 = 8000f / target.height;
                            boundaryMatrix.m20 = -4000f;
                            boundaryMatrix.m22 = boundary * cellSize - 0.01f;
                        }
                        material.SetMatrix("_PixelCoordToViewDirWS", boundaryMatrix);
                        var before = Render(material, target, 0f);
                        if (axis == 0) boundaryMatrix.m20 += 0.02f;
                        else boundaryMatrix.m22 += 0.02f;
                        material.SetMatrix("_PixelCoordToViewDirWS", boundaryMatrix);
                        boundaryJump = Mathf.Max(boundaryJump, MaximumDifference(before, Render(material, target, 0f)));
                    }
                }
                var distantMatrix = Matrix4x4.zero;
                distantMatrix.m20 = 20000f;
                distantMatrix.m21 = -1000f;
                distantMatrix.m22 = 10000f;
                material.SetMatrix("_PixelCoordToViewDirWS", distantMatrix);
                float distantCoverage = Average(Render(material, target, 0f));
                material.SetMatrix("_PixelCoordToViewDirWS", matrix);
                material.SetColor("_CloudsLightColor", new Color(1f, 0.96f, 0.88f));
                material.SetColor("_CloudsShadowColor", new Color(0.64f, 0.70f, 0.80f));
                var lit = Render(material, target, 0f, "clouds-lighting-reference");
                material.SetVector("_CloudsSunDirection", new Vector3(0.6f, -0.7f, -0.35f).normalized);
                float sunDirectionDifference = Difference(lit, Render(material, target, 0f));
                material.SetVector("_CloudsSunDirection", new Vector3(-0.6f, 0.7f, 0.35f).normalized);
                material.SetVector("_CloudsLighting", new Vector4(0f, 0.18f, 0.45f, 0f));
                float directLightDifference = Difference(lit, Render(material, target, 0f));
                material.SetVector("_CloudsLighting", new Vector4(0.9f, 0.18f, 0.45f, 0.18f));
                material.SetColor("_CloudsLightColor", Color.white);
                material.SetColor("_CloudsShadowColor", Color.white);
                material.SetVector("_CloudsShape", new Vector4(0, 3f, 0.08f, 0.35f));
                float clear = Average(Render(material, target, 0f));
                material.SetVector("_CloudsShape", new Vector4(1, 3f, 0.08f, 0.35f));
                float overcast = Average(Render(material, target, 0f));
                matrix.m11 = -matrix.m11;
                matrix.m21 = -matrix.m21;
                material.SetMatrix("_PixelCoordToViewDirWS", matrix);
                float belowHorizon = Average(Render(material, target, 0f));

                // Authored cubemaps must bypass procedural coverage and evolution settings.
                var cube = new Cubemap(1, TextureFormat.RGBA32, false);
                try
                {
                    foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX,
                                 CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                        cube.SetPixel(face, 0, 0, Color.white);
                    cube.Apply();
                    material.SetTexture("_CloudsCubemap", cube);
                    material.SetFloat("_HasCloudsCubemap", 1f);
                    material.SetVector("_CloudsShape", new Vector4(0, 3f, 0.08f, 0.35f));
                    float cubemap = Average(Render(material, target, 20f));
                    bool passed = evolution > 0.005f && wind > 0.005f && frozen < 0.00001f
                                  && opacityError < 0.005f && fadeError < 0.005f
                                  && zeroFade < 0.001f && zeroOpacity < 0.001f
                                  && sunDirectionDifference > 0.005f && directLightDifference > 0.005f
                                  && rotation > 0.005f && horizonBand > 0.99f
                                  && aboveLimit < 0.001f && limitedZenith < 0.001f && zeroHeight < 0.001f
                                  && zenithCoverage > 0.005f && distantCoverage < 0.001f && breakupDifference > 0.0003f
                                  && erodedPixels > 20 && retainedPixels > 20
                                  && boundaryJump < 0.03f && clear < 0.001f && overcast > 0.9f && belowHorizon < 0.001f && cubemap > 0.99f;
                    string report = $"passed={passed}\nevolutionDifference={evolution}\nwindDifference={wind}\nfrozenDifference={frozen}\nmaximumBoundaryJump={boundaryJump}\nzenithCoverage={zenithCoverage}\ndistantCoverage={distantCoverage}\nbreakupDifference={breakupDifference}\nerodedPixels={erodedPixels}\nretainedPixels={retainedPixels}\nclearCoverage={clear}\nfullCoverage={overcast}\nbelowHorizon={belowHorizon}\ncubemapCoverage={cubemap}\n";
                    report += $"rotationDifference={rotation}\nhorizonBand={horizonBand}\naboveHeightLimit={aboveLimit}\nlimitedZenith={limitedZenith}\nzeroHeight={zeroHeight}\n";
                    report += $"opacityError={opacityError}\nfadeError={fadeError}\nzeroFade={zeroFade}\nzeroOpacity={zeroOpacity}\n";
                    report += $"sunDirectionDifference={sunDirectionDifference}\ndirectLightDifference={directLightDifference}\n";
                    File.WriteAllText("Library/PolyverseCloudValidation.txt", report);
                    if (!passed) throw new InvalidOperationException(report);
                    Debug.Log("[Polyverse Clouds] GPU animation validation passed.\n" + report);
                }
                finally { UnityEngine.Object.DestroyImmediate(cube); }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static Color32[] Render(Material material, RenderTexture target, float time, string imageName = null)
        {
            material.SetFloat("_SkyTime", time);
            RenderTexture.active = target;
            GL.Clear(false, true, Color.black);
            if (!material.SetPass(material.FindPass("FragBaking")))
                throw new InvalidOperationException("The cloud shader baking pass could not be compiled/bound.");
            Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                if (imageName != null) File.WriteAllBytes("Library/" + imageName + ".png", texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static float Difference(Color32[] a, Color32[] b)
        {
            double difference = 0;
            for (int i = 0; i < a.Length; i++) difference += Math.Abs(a[i].r - b[i].r) / 255.0;
            return (float)(difference / a.Length);
        }

        private static float Average(Color32[] pixels)
        {
            double sum = 0;
            foreach (var pixel in pixels) sum += pixel.r / 255.0;
            return (float)(sum / pixels.Length);
        }

        private static float MaximumDifference(Color32[] a, Color32[] b)
        {
            int maximum = 0;
            for (int i = 0; i < a.Length; i++) maximum = Math.Max(maximum, Math.Abs(a[i].r - b[i].r));
            return maximum / 255f;
        }

        private static float MaximumScaledDifference(Color32[] full, Color32[] scaled, float multiplier)
        {
            float maximum = 0f;
            for (int i = 0; i < full.Length; i++)
                maximum = Mathf.Max(maximum, Mathf.Abs(full[i].r * multiplier - scaled[i].r));
            return maximum / 255f;
        }
    }
}
