using System.Collections;
using UnityEngine;

public static class EnemyTelegraph
{
    static Sprite squareSprite;
    static Sprite circleSprite;

    public static GameObject CreateBox(string name, Vector2 center, Vector2 size, float angleDegrees, Color fill, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(center.x, center.y, 0f);
        root.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

        SpriteRenderer fillRenderer = AddRect(root.transform, "Fill", Vector2.zero, size, fill, sortingOrder);
        fillRenderer.sortingOrder = sortingOrder;
        AddDashedRect(root.transform, size, WithAlpha(fill, 0.95f), sortingOrder + 1);
        return root;
    }

    public static GameObject CreateCircle(string name, Vector2 center, float radius, Color fill, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(center.x, center.y, 0f);

        SpriteRenderer fillRenderer = root.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = CircleSprite();
        fillRenderer.color = fill;
        fillRenderer.sortingOrder = sortingOrder;
        root.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        AddDashedCircle(root.transform, radius, WithAlpha(fill, 0.95f), sortingOrder + 1);
        return root;
    }

    public static GameObject CreateLine(string name, Vector2 start, Vector2 end, float width, Color color, int sortingOrder = 60)
    {
        Vector2 delta = end - start;
        Vector2 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        return CreateBox(name, center, new Vector2(delta.magnitude, width), angle, color, sortingOrder);
    }

    public static GameObject CreatePolygon(string name, Vector2[] worldPoints, Color fill, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        if (worldPoints == null || worldPoints.Length < 3)
            return root;

        Vector2 center = Vector2.zero;
        for (int i = 0; i < worldPoints.Length; i++)
            center += worldPoints[i];
        center /= worldPoints.Length;
        root.transform.position = new Vector3(center.x, center.y, 0f);

        Vector3[] vertices = new Vector3[worldPoints.Length];
        for (int i = 0; i < worldPoints.Length; i++)
            vertices[i] = worldPoints[i] - center;

        int[] triangles = new int[(worldPoints.Length - 2) * 3];
        for (int i = 0; i < worldPoints.Length - 2; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh mesh = new Mesh { name = name + "_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        MeshFilter filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.material.color = fill;
        renderer.sortingOrder = sortingOrder;

        for (int i = 0; i < worldPoints.Length; i++)
        {
            Vector2 a = worldPoints[i] - center;
            Vector2 b = worldPoints[(i + 1) % worldPoints.Length] - center;
            CreateLineSegment(root.transform, "PolygonEdge_" + i, a, b, 0.12f, WithAlpha(fill, 0.95f), sortingOrder + 1);
        }

        return root;
    }

    public static GameObject CreateFan(string name, Vector2 origin, Vector2 direction, float radius, float angleDegrees, Color color, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        root.transform.position = origin;

        int segments = 12;
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startAngle = centerAngle - angleDegrees * 0.5f;
        Vector2 previous = origin;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = (startAngle + angleDegrees * t) * Mathf.Deg2Rad;
            Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (i > 0)
                CreateLineSegment(root.transform, "FanEdge_" + i, previous - origin, point - origin, 0.12f, color, sortingOrder);

            previous = point;
        }

        CreateLineSegment(root.transform, "FanLeft", Vector2.zero, previous - origin, 0.12f, color, sortingOrder + 1);
        float rightAngle = startAngle * Mathf.Deg2Rad;
        Vector2 right = new Vector2(Mathf.Cos(rightAngle), Mathf.Sin(rightAngle)) * radius;
        CreateLineSegment(root.transform, "FanRight", Vector2.zero, right, 0.12f, color, sortingOrder + 1);
        return root;
    }

    // A filled fan made of radial slices ordered from one edge to the other so it can be
    // swept in (revealed) to read as a swinging strike. Root has no renderer of its own.
    public static GameObject CreateFilledFan(string name, Vector2 origin, Vector2 direction, float radius, float angleDegrees, Color color, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(origin.x, origin.y, 0f);
        float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, centerAngle);

        int slices = Mathf.Max(8, Mathf.CeilToInt(angleDegrees / 3f));
        float half = angleDegrees * 0.5f;
        float step = angleDegrees / slices;
        float sliceWidth = radius * (step * Mathf.Deg2Rad) * 1.7f;
        for (int i = 0; i < slices; i++)
        {
            float localAngle = -half + step * (i + 0.5f);
            float rad = localAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            SpriteRenderer slice = AddRect(root.transform, "FanSlice_" + i, dir * (radius * 0.5f), new Vector2(radius, sliceWidth), color, sortingOrder);
            slice.transform.localRotation = Quaternion.Euler(0f, 0f, localAngle);
        }

        return root;
    }

    // A filled straight strip made of segments ordered start -> end so it can be wiped out
    // to read as a lashing thrust. Root has no renderer of its own.
    public static GameObject CreateFilledStrip(string name, Vector2 start, Vector2 end, float width, Color color, int sortingOrder, int segments)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(start.x, start.y, 0f);
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        int count = Mathf.Max(2, segments);
        float segLen = length / count;
        for (int i = 0; i < count; i++)
            AddRect(root.transform, "Seg_" + i, new Vector2(segLen * (i + 0.5f), 0f), new Vector2(segLen * 1.04f, width), color, sortingOrder);

        return root;
    }

    // A dotted, pixel-art style thread built from a chain of small squares with a slight
    // wobble and alternating shade. Dots are ordered start -> end so revealing them in order
    // reads as a thread stretching out. Root has no renderer of its own.
    public static GameObject CreateThread(string name, Vector2 start, Vector2 end, float width, Color color, int sortingOrder = 60)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(start.x, start.y, 0f);
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        float dotSize = Mathf.Max(0.06f, width);
        float spacing = dotSize * 0.85f;
        int dotCount = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        for (int i = 0; i < dotCount; i++)
        {
            float along = Mathf.Min(length, i * spacing);
            float wobble = Mathf.Sin(i * 1.7f) * dotSize * 0.4f;
            float jitter = 0.7f + 0.55f * Mathf.Abs(Mathf.Sin(i * 2.3f));
            Color dotColor = color;
            dotColor.a *= (i % 2 == 0) ? 1f : 0.78f;
            AddRect(root.transform, "ThreadDot_" + i, new Vector2(along, wobble), new Vector2(dotSize * jitter, dotSize * jitter), dotColor, sortingOrder);
        }

        return root;
    }

    // Reveals the first `fraction` of a telegraph's child renderers (in creation order),
    // hiding the rest. Used to sweep/wipe/extend a filled telegraph over time.
    public static void SetRevealFraction(GameObject root, float fraction)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int visible = Mathf.RoundToInt(Mathf.Clamp01(fraction) * renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].enabled = i < visible;
    }

    // Sets every child renderer's alpha to an absolute value, so a uniformly coloured
    // telegraph can be faded out without compounding across frames.
    public static void SetUniformAlpha(GameObject root, float alpha)
    {
        if (root == null)
            return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        float a = Mathf.Clamp01(alpha);
        for (int i = 0; i < renderers.Length; i++)
        {
            Color c = renderers[i].color;
            c.a = a;
            renderers[i].color = c;
        }

        MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i].material == null)
                continue;

            Color c = meshRenderers[i].material.color;
            c.a = a;
            meshRenderers[i].material.color = c;
        }
    }

    public static IEnumerator Blink(GameObject target, int blinkCount, float interval)
    {
        if (target == null)
            yield break;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        int cycles = Mathf.Max(1, blinkCount) * 2;
        float wait = Mathf.Max(0.03f, interval);
        for (int i = 0; i < cycles; i++)
        {
            bool enabled = i % 2 == 0;
            for (int r = 0; r < renderers.Length; r++)
                if (renderers[r] != null)
                    renderers[r].enabled = enabled;

            yield return new WaitForSeconds(wait);
        }

        for (int r = 0; r < renderers.Length; r++)
            if (renderers[r] != null)
                renderers[r].enabled = true;
    }

    public static bool PointInOrientedBox(Vector2 point, Vector2 center, Vector2 size, float angleDegrees)
    {
        Quaternion inverse = Quaternion.Euler(0f, 0f, -angleDegrees);
        Vector2 local = inverse * (point - center);
        return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f;
    }

    public static bool PointInFan(Vector2 point, Vector2 origin, Vector2 direction, float radius, float angleDegrees)
    {
        Vector2 toPoint = point - origin;
        if (toPoint.sqrMagnitude > radius * radius)
            return false;

        if (toPoint.sqrMagnitude <= 0.0001f)
            return true;

        return Vector2.Angle(direction.normalized, toPoint.normalized) <= angleDegrees * 0.5f;
    }

    public static Sprite SquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }

    public static Sprite CircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.5f - 1f;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : clear);
            }
        }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    static SpriteRenderer AddRect(Transform parent, string name, Vector2 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject rect = new GameObject(name);
        rect.transform.SetParent(parent, false);
        rect.transform.localPosition = position;
        rect.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = rect.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    static void AddDashedRect(Transform parent, Vector2 size, Color color, int sortingOrder)
    {
        float dashLength = 0.45f;
        float gap = 0.28f;
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, size.y * 0.5f), Vector2.right, size.x, dashLength, gap, color, sortingOrder);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.right, size.x, dashLength, gap, color, sortingOrder);
        AddDashedEdge(parent, new Vector2(-size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dashLength, gap, color, sortingOrder);
        AddDashedEdge(parent, new Vector2(size.x * 0.5f, -size.y * 0.5f), Vector2.up, size.y, dashLength, gap, color, sortingOrder);
    }

    static void AddDashedCircle(Transform parent, float radius, Color color, int sortingOrder)
    {
        int dashCount = 24;
        for (int i = 0; i < dashCount; i += 2)
        {
            float a0 = i / (float)dashCount * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)dashCount * Mathf.PI * 2f;
            Vector2 start = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
            Vector2 end = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            CreateLineSegment(parent, "CircleDash_" + i, start, end, 0.08f, color, sortingOrder);
        }
    }

    static void AddDashedEdge(Transform parent, Vector2 start, Vector2 direction, float length, float dashLength, float gap, Color color, int sortingOrder)
    {
        int index = 0;
        float offset = 0f;
        while (offset < length)
        {
            float segment = Mathf.Min(dashLength, length - offset);
            Vector2 center = start + direction * (offset + segment * 0.5f);
            Vector2 size = Mathf.Abs(direction.x) > 0f ? new Vector2(segment, 0.08f) : new Vector2(0.08f, segment);
            AddRect(parent, "Dash_" + index, center, size, color, sortingOrder);
            offset += dashLength + gap;
            index++;
        }
    }

    static void CreateLineSegment(Transform parent, string name, Vector2 start, Vector2 end, float width, Color color, int sortingOrder)
    {
        Vector2 delta = end - start;
        Vector2 center = (start + end) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        SpriteRenderer renderer = AddRect(parent, name, center, new Vector2(delta.magnitude, width), color, sortingOrder);
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
