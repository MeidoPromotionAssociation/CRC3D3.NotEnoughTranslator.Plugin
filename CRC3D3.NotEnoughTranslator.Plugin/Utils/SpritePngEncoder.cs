using UnityEngine;

namespace CRC3D3.NotEnoughTranslator.Plugin.Utils;

/// <summary>
/// 按精灵网格从纹理还原单张 PNG，兼容旋转和紧密打包的图集子图。
/// Reconstruct an individual PNG from the sprite mesh, including rotated and tightly packed atlas entries.
/// </summary>
internal static class SpritePngEncoder
{
    /// <summary>
    /// 在 Unity 主线程读取精灵占用的纹理区域，按原始尺寸编码带透明通道的 PNG。
    /// Read the sprite's texture region on Unity's main thread and encode a PNG at its original size with alpha.
    /// </summary>
    internal static byte[] Encode(Sprite sprite)
    {
        var texture = sprite.texture;
        var rect = sprite.rect;
        var size = new Vector2Int(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
        var vertices = sprite.vertices;
        var uv = sprite.uv;
        var triangles = sprite.triangles;
        if (texture == null || size.x <= 0 || size.y <= 0 || uv.Length == 0 || triangles.Length == 0)
            return null;

        var textureSize = new Vector2(texture.width, texture.height);
        for (var i = 0; i < uv.Length; i++) uv[i] = Vector2.Scale(uv[i], textureSize);
        var region = GetRegion(uv, texture.width, texture.height);
        var source = ReadRegion(texture, region);

        var alphaTexture = sprite.associatedAlphaSplitTexture;
        if (alphaTexture != null)
        {
            var alpha = ReadRegion(alphaTexture, region);
            for (var i = 0; i < source.Length; i++) source[i].a = alpha[i].r;
        }

        var pivot = sprite.pivot;
        var pixelsPerUnit = sprite.pixelsPerUnit;
        for (var i = 0; i < vertices.Length; i++) vertices[i] = vertices[i] * pixelsPerUnit + pivot;

        var pixels = new Color32[checked(size.x * size.y)];
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var a = triangles[i];
            var b = triangles[i + 1];
            var c = triangles[i + 2];
            CopyTriangle(vertices[a], vertices[b], vertices[c], uv[a], uv[b], uv[c], region, source, size, pixels);
        }

        var output = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
        try
        {
            output.SetPixels32(pixels);
            return output.EncodeToPNG();
        }
        finally
        {
            Object.Destroy(output);
        }
    }

    /// <summary>
    /// 从精灵的纹理坐标计算需要读回的最小像素范围。
    /// Find the smallest pixel region needed by the sprite's texture coordinates.
    /// </summary>
    private static RectInt GetRegion(Vector2[] uv, int width, int height)
    {
        var minimum = uv[0];
        var maximum = uv[0];
        foreach (var point in uv)
        {
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }

        var left = Mathf.Clamp(Mathf.FloorToInt(minimum.x), 0, width - 1);
        var bottom = Mathf.Clamp(Mathf.FloorToInt(minimum.y), 0, height - 1);
        var right = Mathf.Clamp(Mathf.CeilToInt(maximum.x), left + 1, width);
        var top = Mathf.Clamp(Mathf.CeilToInt(maximum.y), bottom + 1, height);
        return new RectInt(left, bottom, right - left, top - bottom);
    }

    /// <summary>
    /// 通过临时渲染纹理读取指定区域，支持关闭 Read/Write 的图集。
    /// Read a region through a temporary render texture, including atlases with Read/Write disabled.
    /// </summary>
    private static Color32[] ReadRegion(Texture2D texture, RectInt region)
    {
        var previousTarget = RenderTexture.active;
        var previousSrgbWrite = GL.sRGBWrite;
        var readWrite = texture.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
        var target = RenderTexture.GetTemporary(region.width, region.height, 0,
            RenderTextureFormat.ARGB32, readWrite);
        Texture2D readback = null;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear && target.sRGB;
            var textureSize = new Vector2(texture.width, texture.height);
            var scale = new Vector2(region.width / textureSize.x, region.height / textureSize.y);
            var offset = new Vector2(region.x / textureSize.x, region.y / textureSize.y);
            Graphics.Blit(texture, target, scale, offset);
            RenderTexture.active = target;

            readback = new Texture2D(region.width, region.height, TextureFormat.RGBA32, false, !target.sRGB);
            readback.ReadPixels(new Rect(0, 0, region.width, region.height), 0, 0, false);
            return readback.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previousTarget;
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.ReleaseTemporary(target);
            if (readback != null) Object.Destroy(readback);
        }
    }

    /// <summary>
    /// 将网格三角形覆盖的像素映射到图集，三角形外保持透明。
    /// Map pixels covered by the sprite mesh to atlas texels, leaving pixels outside the mesh transparent.
    /// </summary>
    private static void CopyTriangle(Vector2 a, Vector2 b, Vector2 c, Vector2 uvA, Vector2 uvB, Vector2 uvC,
        RectInt region, Color32[] source, Vector2Int size, Color32[] output)
    {
        var edgeB = b - a;
        var edgeC = c - a;
        var area = edgeB.x * edgeC.y - edgeB.y * edgeC.x;
        if (Mathf.Approximately(area, 0)) return;

        var left = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 0.5f));
        var right = Mathf.Min(size.x - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) - 0.5f));
        var bottom = Mathf.Max(0, Mathf.CeilToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - 0.5f));
        var top = Mathf.Min(size.y - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)) - 0.5f));

        var uvEdgeB = uvB - uvA;
        var uvEdgeC = uvC - uvA;
        const float edgeTolerance = 0.0001f;
        for (var y = bottom; y <= top; y++)
        for (var x = left; x <= right; x++)
        {
            var offset = new Vector2(x + 0.5f, y + 0.5f) - a;
            var weightB = (offset.x * edgeC.y - offset.y * edgeC.x) / area;
            var weightC = (edgeB.x * offset.y - edgeB.y * offset.x) / area;
            if (weightB < -edgeTolerance || weightC < -edgeTolerance || weightB + weightC > 1 + edgeTolerance)
                continue;

            var uv = uvA + weightB * uvEdgeB + weightC * uvEdgeC;
            var sourceX = Mathf.Clamp(Mathf.FloorToInt(uv.x) - region.x, 0, region.width - 1);
            var sourceY = Mathf.Clamp(Mathf.FloorToInt(uv.y) - region.y, 0, region.height - 1);
            output[y * size.x + x] = source[sourceY * region.width + sourceX];
        }
    }
}
