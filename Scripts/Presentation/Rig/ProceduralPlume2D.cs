using Godot;

/// <summary>无贴图冠翎：弯曲细分网格定义轮廓，Shader 绘制羽枝和随风弯曲；每根都有独立材质。</summary>
public partial class ProceduralPlume2D : MeshInstance2D
{
    public ShaderMaterial WindMaterial { get; private set; }
    public void Build(System.Collections.Generic.IReadOnlyList<Vector2> points, Vector4 profile)
    {
        // 首点后每三点组成一段贝塞尔曲线，支持拱起、下垂与回卷连续衔接。
        int curves = (points.Count - 1) / 3;
        if (curves < 1 || points.Count != curves * 3 + 1)
            throw new System.ArgumentException("冠翎曲线需要 3n+1 个控制点。");
        int segments = curves * 48;
        const int columns = 6;
        Vector2 root = points[0];
        Position = root;
        var vertices = new Vector3[(segments + 1) * (columns + 1)];
        var uv = new Vector2[vertices.Length];
        var indices = new int[segments * columns * 6];
        for (int row = 0; row <= segments; row++)
        {
            float t = row / (float)segments;
            int curve = Mathf.Min(curves - 1, (int)(t * curves));
            float local = t * curves - curve, u = 1 - local;
            int p = curve * 3;
            Vector2 start = points[p], first = points[p + 1], second = points[p + 2], tip = points[p + 3];
            Vector2 center = u * u * u * start + 3 * u * u * local * first
                + 3 * u * local * local * second + local * local * local * tip - root;
            Vector2 tangent = (3 * u * u * (first - start) + 6 * u * local * (second - first)
                + 3 * local * local * (tip - second)).Normalized();
            Vector2 normal = new(-tangent.Y, tangent.X);
            // 根部细而结实，中段展开，末梢收尖；小齿沿羽枝方向错落，避免胶带般的直边。
            float width = .32f * (1 - t) + profile.X * .5f * Mathf.Pow(1 - t, .8f)
                * (1 - Mathf.Exp(-t * 22)) * (1 + .35f * Mathf.Sin(t * Mathf.Tau * 1.2f));
            for (int column = 0; column <= columns; column++)
            {
                float across = column / (float)columns * 2 - 1;
                float barb = Mathf.PosMod(t * 24 + (across > 0 ? .35f : 0), 1);
                float fringe = 1 - .16f * Mathf.Pow(Mathf.Abs(across), 4) * Mathf.Pow(barb, 3);
                Vector2 point = center + normal * across * width * fringe;
                int index = row * (columns + 1) + column;
                vertices[index] = new Vector3(point.X, point.Y, 0);
                uv[index] = new Vector2(t, across);
                if (row == segments || column == columns) continue;
                int offset = (row * columns + column) * 6, next = index + columns + 1;
                indices[offset] = index; indices[offset + 1] = next; indices[offset + 2] = index + 1;
                indices[offset + 3] = index + 1; indices[offset + 4] = next; indices[offset + 5] = next + 1;
            }
        }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh(); mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;
        WindMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Resources/Shaders/ProceduralPlume.gdshader") };
        WindMaterial.SetShaderParameter("rate", profile.Y);
        WindMaterial.SetShaderParameter("amplitude", profile.Z);
        WindMaterial.SetShaderParameter("phase", profile.W);
        Material = WindMaterial;
    }

    public void SetWind(float time, float weight, Vector2 direction)
    {
        WindMaterial.SetShaderParameter("motion_time", time);
        WindMaterial.SetShaderParameter("motion_weight", weight);
        WindMaterial.SetShaderParameter("flow", direction);
    }
}
