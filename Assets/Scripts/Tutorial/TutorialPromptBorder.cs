using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TutorialPromptBorder : MaskableGraphic
{
    [SerializeField, Min(1f)] float dashLength = 34f;
    [SerializeField, Min(0f)] float gapLength = 18f;
    [SerializeField, Min(1f)] float thickness = 5f;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = rectTransform.rect;
        AddDashedEdge(vertexHelper, new Vector2(rect.xMin, rect.yMax), Vector2.right, rect.width);
        AddDashedEdge(vertexHelper, new Vector2(rect.xMin, rect.yMin), Vector2.right, rect.width);
        AddDashedEdge(vertexHelper, new Vector2(rect.xMin, rect.yMin), Vector2.up, rect.height);
        AddDashedEdge(vertexHelper, new Vector2(rect.xMax, rect.yMin), Vector2.up, rect.height);
    }

    void AddDashedEdge(VertexHelper vertexHelper, Vector2 start, Vector2 direction, float length)
    {
        float step = Mathf.Max(1f, dashLength + gapLength);
        for (float offset = 0f; offset < length; offset += step)
        {
            float segmentLength = Mathf.Min(dashLength, length - offset);
            Vector2 center = start + direction * (offset + segmentLength * 0.5f);
            Vector2 size = Mathf.Abs(direction.x) > 0f
                ? new Vector2(segmentLength, thickness)
                : new Vector2(thickness, segmentLength);
            AddQuad(vertexHelper, center, size);
        }
    }

    void AddQuad(VertexHelper vertexHelper, Vector2 center, Vector2 size)
    {
        Vector2 half = size * 0.5f;
        int startIndex = vertexHelper.currentVertCount;
        Color32 vertexColor = color;
        vertexHelper.AddVert(new Vector3(center.x - half.x, center.y - half.y), vertexColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(center.x - half.x, center.y + half.y), vertexColor, Vector2.up);
        vertexHelper.AddVert(new Vector3(center.x + half.x, center.y + half.y), vertexColor, Vector2.one);
        vertexHelper.AddVert(new Vector3(center.x + half.x, center.y - half.y), vertexColor, Vector2.right);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }
}
