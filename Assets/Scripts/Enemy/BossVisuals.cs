using UnityEngine;

// Programmer-art visuals for the Minotaur middle boss: sewing blueprints, body-part
// marks, sewing pins, dashed thread and doll silhouettes. Everything is generated from
// runtime sprites so no art assets are required.
public static class BossVisuals
{
    static Sprite squareSprite;
    static Sprite circleSprite;
    static Sprite ringSprite;

    public static readonly Color PaperColor = new Color(0.93f, 0.88f, 0.74f, 0.98f);
    public static readonly Color PaperLineColor = new Color(0.30f, 0.24f, 0.18f, 0.9f);
    public static readonly Color InkColor = new Color(0.27f, 0.21f, 0.16f, 1f);
    public static readonly Color MarkOk = new Color(0.18f, 0.62f, 0.28f, 1f);
    public static readonly Color MarkBad = new Color(0.86f, 0.16f, 0.16f, 1f);
    public static readonly Color PinColor = new Color(0.82f, 0.82f, 0.86f, 1f);
    public static readonly Color PinHeadColor = new Color(0.86f, 0.24f, 0.30f, 1f);

    // ---- sprite factories -------------------------------------------------

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
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : clear);
            }

        texture.Apply();
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    public static Sprite RingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float outer = size * 0.5f - 1f;
        float inner = outer - size * 0.16f;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= outer && distance >= inner ? Color.white : clear);
            }

        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return ringSprite;
    }

    // ---- primitive builders ----------------------------------------------

    public static SpriteRenderer CreateRect(Transform parent, string name, Vector3 localPos, Vector2 size, Color color, int order, float rotation = 0f)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    public static SpriteRenderer CreateCircle(Transform parent, string name, Vector3 localPos, float diameter, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(diameter, diameter, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = CircleSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    public static SpriteRenderer CreateRing(Transform parent, string name, Vector3 localPos, float diameter, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(diameter, diameter, 1f);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = RingSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    public static GameObject CreateDashedLine(Transform parent, string name, Vector2 start, Vector2 end, float width, Color color, int order, float dash = 0.34f, float gap = 0.2f)
    {
        GameObject root = new GameObject(name);
        if (parent != null)
            root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(start.x, start.y, 0f);
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        root.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        float offset = 0f;
        int index = 0;
        while (offset < length)
        {
            float segment = Mathf.Min(dash, length - offset);
            CreateRect(root.transform, "Dash_" + index, new Vector3(offset + segment * 0.5f, 0f, 0f), new Vector2(segment, width), color, order);
            offset += dash + gap;
            index++;
        }

        return root;
    }

    static void AddDashedBorder(Transform parent, Vector2 size, Color color, int order)
    {
        float halfX = size.x * 0.5f;
        float halfY = size.y * 0.5f;
        CreateDashedLine(parent, "Border_T", new Vector2(-halfX, halfY), new Vector2(halfX, halfY), 0.05f, color, order);
        CreateDashedLine(parent, "Border_B", new Vector2(-halfX, -halfY), new Vector2(halfX, -halfY), 0.05f, color, order);
        CreateDashedLine(parent, "Border_L", new Vector2(-halfX, -halfY), new Vector2(-halfX, halfY), 0.05f, color, order);
        CreateDashedLine(parent, "Border_R", new Vector2(halfX, -halfY), new Vector2(halfX, halfY), 0.05f, color, order);
    }

    // ---- composite props --------------------------------------------------

    // A sheet of sewing-blueprint paper with a dashed border and a couple of faint guides.
    public static GameObject CreatePaper(string name, Vector2 worldPos, Vector2 size, int order)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        CreateRect(root.transform, "Sheet", Vector3.zero, size, PaperColor, order);
        CreateRect(root.transform, "Shadow", new Vector3(0.12f, -0.12f, 0.01f), size, new Color(0f, 0f, 0f, 0.18f), order - 1);
        AddDashedBorder(root.transform, size * 0.92f, PaperLineColor, order + 1);
        return root;
    }

    // Small body-part icon used inside the judgement blueprint.
    public static GameObject CreatePartIcon(Transform parent, string name, BodySlot slot, Vector3 localPos, float scale, Color color, int order)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;

        switch (slot)
        {
            case BodySlot.EyeLeft:
            case BodySlot.EyeRight:
                CreateRing(root.transform, "Eye", Vector3.zero, 0.5f * scale, color, order);
                CreateCircle(root.transform, "Pupil", Vector3.zero, 0.18f * scale, color, order + 1);
                break;
            case BodySlot.ArmLeft:
            case BodySlot.ArmRight:
                CreateRect(root.transform, "Upper", new Vector3(0f, 0.12f * scale, 0f), new Vector2(0.16f * scale, 0.4f * scale), color, order, 18f);
                CreateRect(root.transform, "Lower", new Vector3(0.12f * scale, -0.2f * scale, 0f), new Vector2(0.16f * scale, 0.36f * scale), color, order, -28f);
                break;
            default:
                CreateRect(root.transform, "Thigh", new Vector3(0f, 0.14f * scale, 0f), new Vector2(0.18f * scale, 0.42f * scale), color, order);
                CreateRect(root.transform, "Shin", new Vector3(0.04f * scale, -0.22f * scale, 0f), new Vector2(0.18f * scale, 0.34f * scale), color, order, -12f);
                CreateRect(root.transform, "Foot", new Vector3(0.12f * scale, -0.4f * scale, 0f), new Vector2(0.3f * scale, 0.1f * scale), color, order);
                break;
        }

        return root;
    }

    public static GameObject CreateOkMark(Transform parent, Vector3 localPos, float scale, int order)
    {
        GameObject root = new GameObject("OkMark");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        CreateRing(root.transform, "O", Vector3.zero, 0.78f * scale, MarkOk, order);
        return root;
    }

    public static GameObject CreateXMark(Transform parent, Vector3 localPos, float scale, int order)
    {
        GameObject root = new GameObject("XMark");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        CreateRect(root.transform, "Slash_A", Vector3.zero, new Vector2(0.78f * scale, 0.14f * scale), MarkBad, order, 45f);
        CreateRect(root.transform, "Slash_B", Vector3.zero, new Vector2(0.78f * scale, 0.14f * scale), MarkBad, order, -45f);
        return root;
    }

    // A large sewing pin standing on the desk: long needle plus a coloured ball head.
    public static GameObject CreatePin(string name, Vector2 worldPos, int order, float scale = 1f)
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

        CreateRect(root.transform, "Needle", new Vector3(0f, 0.55f * scale, 0f), new Vector2(0.1f * scale, 1.5f * scale), PinColor, order);
        CreateRect(root.transform, "Tip", new Vector3(0f, -0.25f * scale, 0f), new Vector2(0.14f * scale, 0.32f * scale), new Color(0.6f, 0.6f, 0.64f, 1f), order, 0f);
        CreateCircle(root.transform, "Head", new Vector3(0f, 1.42f * scale, 0f), 0.5f * scale, PinHeadColor, order + 1);
        CreateCircle(root.transform, "HeadShine", new Vector3(-0.08f * scale, 1.5f * scale, 0f), 0.16f * scale, new Color(1f, 1f, 1f, 0.7f), order + 2);
        return root;
    }

    // A doll silhouette drawn on a blueprint. A null missingSlot means the doll is whole;
    // otherwise the matching limb/eye is omitted and marked with a faint red cross.
    public static GameObject CreateDollSilhouette(Transform parent, string name, Vector3 localPos, float scale, BodySlot? missingSlot, int order)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;

        Color ink = InkColor;

        CreateCircle(root.transform, "Head", new Vector3(0f, 0.95f * scale, 0f), 0.66f * scale, ink, order);
        CreateRect(root.transform, "Body", new Vector3(0f, 0.05f * scale, 0f), new Vector2(0.92f * scale, 1.05f * scale), ink, order);

        DrawLimb(root.transform, "ArmLeft", new Vector3(-0.62f * scale, 0.1f * scale, 0f), new Vector2(0.22f * scale, 0.82f * scale), ink, order, missingSlot == BodySlot.ArmLeft, scale);
        DrawLimb(root.transform, "ArmRight", new Vector3(0.62f * scale, 0.1f * scale, 0f), new Vector2(0.22f * scale, 0.82f * scale), ink, order, missingSlot == BodySlot.ArmRight, scale);
        DrawLimb(root.transform, "LegLeft", new Vector3(-0.24f * scale, -0.95f * scale, 0f), new Vector2(0.26f * scale, 0.86f * scale), ink, order, missingSlot == BodySlot.LegLeft, scale);
        DrawLimb(root.transform, "LegRight", new Vector3(0.24f * scale, -0.95f * scale, 0f), new Vector2(0.26f * scale, 0.86f * scale), ink, order, missingSlot == BodySlot.LegRight, scale);

        DrawEye(root.transform, "EyeLeft", new Vector3(-0.18f * scale, 1.02f * scale, 0f), ink, order + 1, missingSlot == BodySlot.EyeLeft, scale);
        DrawEye(root.transform, "EyeRight", new Vector3(0.18f * scale, 1.02f * scale, 0f), ink, order + 1, missingSlot == BodySlot.EyeRight, scale);
        return root;
    }

    static void DrawLimb(Transform parent, string name, Vector3 localPos, Vector2 size, Color color, int order, bool missing, float scale)
    {
        if (missing)
        {
            CreateXMark(parent, localPos, 0.7f * scale, order + 2);
            return;
        }

        CreateRect(parent, name, localPos, size, color, order);
    }

    static void DrawEye(Transform parent, string name, Vector3 localPos, Color color, int order, bool missing, float scale)
    {
        if (missing)
        {
            CreateXMark(parent, localPos, 0.42f * scale, order + 1);
            return;
        }

        CreateCircle(parent, name, localPos, 0.18f * scale, new Color(0.95f, 0.92f, 0.86f, 1f), order);
        CreateCircle(parent, name + "_Pupil", localPos, 0.09f * scale, color, order + 1);
    }
}
