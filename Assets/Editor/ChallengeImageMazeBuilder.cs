using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// 챌린지 씬의 이미지 미로(Assets/maze.png)를 생성한다.
// 규칙:
//  - 벽 스프라이트(garo1/sero1)는 원본 픽셀 밀도를 유지한 채 "균일 스케일"로만 축소한다(찌부러뜨림 금지).
//  - 필요한 벽이 스프라이트보다 짧으면 Tiled 드로우모드가 이미지를 잘라서 표현한다(늘리지 않음).
//  - 각 벽에는 PolygonCollider2D(사각형 4점)를 붙인다.
//  - 미로는 방 벽 콜라이더 안쪽에만 채운다(경계는 Wall_Left/Right/Top/Bottom 콜라이더에서 자동 계산).
//  격자(GH/GV)는 maze.png를 픽셀 분석해 추출한 20x15 미로(S=좌하단, E=우상단).
public static class ChallengeImageMazeBuilder
{
#if UNITY_EDITOR
    const int Cols = 20;
    const int Rows = 15;

    // 가로벽: GH[gridRow 0..Rows][col 0..Cols-1], '1'=벽. gridRow 0 = 맨 아래, Rows = 맨 위.
    static readonly string[] GH = new string[]
    {
        "11111111111111111111",
        "11001000100000011010",
        "10110101000100010110",
        "01101110001110110000",
        "00011011110011101000",
        "11110001111001111101",
        "01100110111010001010",
        "01111110111101101110",
        "01111111100000010011",
        "00001111010101100000",
        "01011000110010101110",
        "11101001000100010011",
        "00101101101110111110",
        "00011110010011101000",
        "01111101000001010110",
        "11111111111111111111",
    };

    // 세로벽: GV[row 0..Rows-1][gridCol 0..Cols], '1'=벽. row 0 = 맨 아래 셀행.
    static readonly string[] GV = new string[]
    {
        "100100101001001000101",
        "101010110111111001001",
        "100010011110011010111",
        "110101000101010011111",
        "101001100001000100101",
        "100011010001101000101",
        "110000010000110110011",
        "100000001001001101001",
        "110010000011110011101",
        "111100100101010110111",
        "100101111011010101001",
        "100100100110011001001",
        "111010001011001000011",
        "111000011101100110101",
        "100000000110010001001",
    };

    // 미로 사각형(월드) 폴백값. 벽 콜라이더에서 자동 계산 실패 시 사용.
    const float FallbackLeft = -18.11f;
    const float FallbackRight = 18.29f;
    const float FallbackBottom = -9.66f;
    const float FallbackTop = 9.69f;
    const float BoundsMargin = 0.25f; // 방 벽 콜라이더 안쪽 여백

    // 플레이어 솔리드 콜라이더는 원형(지름 ~0.8)이고 도전방에선 축소되므로,
    // 통로(cellH-두께)에 넉넉히 들어간다. 보기 좋은 두께를 사용.
    const float WallThickness = 0.28f;   // 원하는 월드 두께
    const float NativeThickness = 0.57f; // 스프라이트 원본 두께(57px / 100ppu)
    const int SortingOrder = 25;

    const string GaroPath = "Assets/Sprites/room/garo1.png";
    const string SeroPath = "Assets/Sprites/room/sero1.png";
    const string ContainerName = "ImageMaze_KnitWalls";

    [MenuItem("Tools/Challenge/Build Image Maze")]
    public static void Build()
    {
        GameObject container = GameObject.Find(ContainerName);
        if (container == null)
        {
            Debug.LogError("[Maze] '" + ContainerName + "' 오브젝트를 씬에서 찾지 못했습니다.");
            return;
        }

        EnsureFullRect(GaroPath);
        EnsureFullRect(SeroPath);
        Sprite garo = LoadSprite(GaroPath);
        Sprite sero = LoadSprite(SeroPath);
        if (garo == null || sero == null)
        {
            Debug.LogError("[Maze] garo1/sero1 스프라이트를 로드하지 못했습니다.");
            return;
        }

        for (int i = container.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(container.transform.GetChild(i).gameObject);

        float Left, Right, Bottom, Top;
        ComputeBounds(out Left, out Right, out Bottom, out Top);

        bool[,] hWall = new bool[Rows + 1, Cols];
        bool[,] vWall = new bool[Rows, Cols + 1];
        ParseBits(hWall, vWall);

        float cellW = (Right - Left) / Cols;
        float cellH = (Top - Bottom) / Rows;
        float s = WallThickness / NativeThickness;

        int count = 0;

        for (int r = 0; r <= Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (hWall[r, c])
                {
                    float cx = Left + cellW * (c + 0.5f);
                    float cy = Bottom + cellH * r;
                    CreateWall(container.transform, garo, true, cx, cy, cellW, s, "H_" + r + "_" + c);
                    count++;
                }

        for (int r = 0; r < Rows; r++)
            for (int c = 0; c <= Cols; c++)
                if (vWall[r, c])
                {
                    float cx = Left + cellW * c;
                    float cy = Bottom + cellH * (r + 0.5f);
                    CreateWall(container.transform, sero, false, cx, cy, cellH, s, "V_" + r + "_" + c);
                    count++;
                }

        EditorSceneManager.MarkSceneDirty(container.scene);
        Debug.Log("[Maze] 미로 생성 완료. 벽 " + count + "개, cellW=" + cellW.ToString("0.00") + " cellH=" + cellH.ToString("0.00") + " scale=" + s.ToString("0.000"));
    }

    [MenuItem("Tools/Challenge/Clear Image Maze")]
    public static void Clear()
    {
        GameObject container = GameObject.Find(ContainerName);
        if (container == null) return;
        for (int i = container.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(container.transform.GetChild(i).gameObject);
        EditorSceneManager.MarkSceneDirty(container.scene);
        Debug.Log("[Maze] 미로 제거 완료.");
    }

    static void CreateWall(Transform parent, Sprite sprite, bool horizontal, float cx, float cy, float length, float s, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(cx, cy, 0f);
        go.transform.localScale = new Vector3(s, s, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.color = Color.white;
        sr.sortingOrder = SortingOrder;

        // size는 스프라이트 로컬 단위(스케일 적용 전). 길이/s 로 두면 최종 월드 길이=length.
        float lenUnits = length / s;
        if (horizontal)
            sr.size = new Vector2(lenUnits, NativeThickness);
        else
            sr.size = new Vector2(NativeThickness, lenUnits);

        float hx = (horizontal ? lenUnits : NativeThickness) * 0.5f;
        float hy = (horizontal ? NativeThickness : lenUnits) * 0.5f;
        PolygonCollider2D poly = go.AddComponent<PolygonCollider2D>();
        poly.pathCount = 1;
        poly.SetPath(0, new Vector2[]
        {
            new Vector2(-hx, -hy),
            new Vector2(hx, -hy),
            new Vector2(hx, hy),
            new Vector2(-hx, hy),
        });
    }

    // 방 벽 콜라이더(Wall_Left/Right/Top/Bottom)에서 내부 영역을 읽어 여백을 두고 미로 사각형을 계산.
    static void ComputeBounds(out float left, out float right, out float bottom, out float top)
    {
        left = FallbackLeft; right = FallbackRight; bottom = FallbackBottom; top = FallbackTop;
        Collider2D cl = FindWallCollider("Wall_Left");
        Collider2D cr = FindWallCollider("Wall_Right");
        Collider2D cb = FindWallCollider("Wall_Bottom");
        Collider2D ct = FindWallCollider("Wall_Top");
        if (cl == null || cr == null || cb == null || ct == null)
        {
            Debug.LogWarning("[Maze] 방 벽 콜라이더를 못 찾아 폴백 경계를 사용합니다.");
            return;
        }
        left = cl.bounds.max.x + BoundsMargin;
        right = cr.bounds.min.x - BoundsMargin;
        bottom = cb.bounds.max.y + BoundsMargin;
        top = ct.bounds.min.y - BoundsMargin;
    }

    static Collider2D FindWallCollider(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Collider2D>() : null;
    }

    static void ParseBits(bool[,] hWall, bool[,] vWall)
    {
        for (int r = 0; r <= Rows; r++)
        {
            string line = r < GH.Length ? GH[r] : "";
            for (int c = 0; c < Cols; c++)
                hWall[r, c] = c < line.Length && line[c] == '1';
        }
        for (int r = 0; r < Rows; r++)
        {
            string line = r < GV.Length ? GV[r] : "";
            for (int c = 0; c <= Cols; c++)
                vWall[r, c] = c < line.Length && line[c] == '1';
        }
    }

    static Sprite LoadSprite(string path)
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (Object o in all)
            if (o is Sprite sp)
                return sp;
        return null;
    }

    static void EnsureFullRect(string path)
    {
        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        TextureImporterSettings settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
    }
#endif
}
