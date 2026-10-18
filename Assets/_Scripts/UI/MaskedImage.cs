using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class MaskedImage : Image
{
    public override Material materialForRendering
    {
        get
        {
            Material material = new Material(base.materialForRendering);
            material.SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
            return material;
        }
    }
}