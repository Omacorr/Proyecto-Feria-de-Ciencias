using UnityEngine;

/// <summary>
/// Emision por MaterialPropertyBlock sin clonar materiales. Escribe a la vez
/// <c>emissiveFactor</c> (shader glTF de glTFast, el de casi todos los modelos)
/// y <c>_EmissionColor</c> (URP Lit/Unlit): la que el shader no tiene se ignora.
/// Con URP Lit hace falta el keyword _EMISSION en el material; con glTF no.
/// </summary>
public static class EmissionUtil
{
    public static readonly int GltfId = Shader.PropertyToID("emissiveFactor");
    public static readonly int UrpId = Shader.PropertyToID("_EmissionColor");

    /// <summary>Escribe el mismo color en las dos propiedades (subMaterial -1 = todo el renderer).</summary>
    public static void Set(Renderer r, MaterialPropertyBlock b, Color c, int subMaterial = -1)
    {
        Set(r, b, c, c, subMaterial);
    }

    /// <summary>Escribe un color distinto por propiedad (cada una con su base propia).</summary>
    public static void Set(Renderer r, MaterialPropertyBlock b, Color gltf, Color urp, int subMaterial = -1)
    {
        if (r == null) return;
        if (subMaterial < 0) r.GetPropertyBlock(b); else r.GetPropertyBlock(b, subMaterial);
        b.SetColor(GltfId, gltf);
        b.SetColor(UrpId, urp);
        if (subMaterial < 0) r.SetPropertyBlock(b); else r.SetPropertyBlock(b, subMaterial);
    }

    /// <summary>Emision original del material en esa propiedad (negro si no la tiene).</summary>
    public static Color Read(Material m, int id)
    {
        return m != null && m.HasProperty(id) ? m.GetColor(id) : Color.black;
    }
}
