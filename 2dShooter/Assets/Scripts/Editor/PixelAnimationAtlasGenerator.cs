#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Builds the pre-sliced pixel-art sprite atlases in the Unity Editor.
/// This class is excluded from player builds; gameplay only loads imported Sprite assets.
/// </summary>
[InitializeOnLoad]
internal static class PixelAnimationAtlasGenerator
{
    private const int SourceSize = 48;
    private const int CellSize = 32;
    private const int FrameCount = 6;
    private const int DirectionCount = 8;
    private const int StateCount = 7;
    private const int AtlasWidth = CellSize * FrameCount;
    private const int AtlasHeight = CellSize * DirectionCount * StateCount;

    private static readonly string[] States =
    {
        "Idle", "Walk", "Run", "Shoot", "Reload", "Hit", "Death"
    };

    private static readonly string[] Directions =
    {
        "S", "SW", "W", "NW", "N", "NE", "E", "SE"
    };

    private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

    static PixelAnimationAtlasGenerator()
    {
        EditorApplication.delayCall += EnsureAtlasesExist;
    }

    [MenuItem("Tools/2D Shooter/Создать пиксельные атласы анимаций")]
    private static void GenerateFromMenu()
    {
        GenerateAll(true);
        Debug.Log("Атласы пиксельной анимации игрока и врага созданы и нарезаны на Sprite-кадры.");
    }

    private static void EnsureAtlasesExist()
    {
        GenerateAll(false);
    }

    private static void GenerateAll(bool force)
    {
        string folder = "Assets/Resources/Animations";
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), folder));

        GenerateAtlas("Player", Path.Combine(folder, "PlayerDirectionalAtlas.png"), force);
        GenerateAtlas("Enemy", Path.Combine(folder, "EnemyDirectionalAtlas.png"), force);
        AssetDatabase.Refresh();
    }

    private static void GenerateAtlas(string prefix, string assetPath, bool force)
    {
        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        TextureImporter currentImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (!force && File.Exists(absolutePath) && currentImporter != null &&
            currentImporter.spriteImportMode == SpriteImportMode.Multiple &&
            currentImporter.maxTextureSize >= 16384 &&
            currentImporter.filterMode == FilterMode.Point)
            return;

        Color32[] atlasPixels = BuildAtlas(prefix);
        Texture2D texture = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false);
        texture.name = prefix + "DirectionalAtlas";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(atlasPixels);
        texture.Apply(false, false);
        byte[] pngBytes = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);

        File.WriteAllBytes(absolutePath, pngBytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Не удалось настроить импорт атласа: " + assetPath);
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.maxTextureSize = 16384;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();

        ApplySpriteSlices(assetPath, prefix);
    }

    private static void ApplySpriteSlices(string assetPath, string prefix)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError("Не удалось получить Sprite importer после настройки атласа: " + assetPath);
            return;
        }

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
        {
            Debug.LogError("Unity не предоставил Sprite Editor Data Provider для атласа: " + assetPath);
            return;
        }

        dataProvider.InitSpriteEditorDataProvider();
        SpriteRect[] spriteRects = new SpriteRect[StateCount * DirectionCount * FrameCount];
        List<SpriteNameFileIdPair> nameFileIds = new List<SpriteNameFileIdPair>(spriteRects.Length);
        int index = 0;

        for (int state = 0; state < StateCount; state++)
        {
            for (int direction = 0; direction < DirectionCount; direction++)
            {
                int row = state * DirectionCount + direction;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    string spriteName = prefix + "_" + States[state] + "_" + Directions[direction] + "_" + frame.ToString("00");
                    GUID spriteId = GUID.Generate();
                    SpriteRect spriteRect = new SpriteRect
                    {
                        name = spriteName,
                        spriteID = spriteId,
                        rect = new Rect(frame * CellSize, AtlasHeight - (row + 1) * CellSize, CellSize, CellSize),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                    spriteRects[index++] = spriteRect;
                    nameFileIds.Add(new SpriteNameFileIdPair(spriteName, spriteId));
                }
            }
        }

        dataProvider.SetSpriteRects(spriteRects);
        ISpriteNameFileIdDataProvider nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdProvider != null)
            nameFileIdProvider.SetNameFileIdPairs(nameFileIds.ToArray());

        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static Color32[] BuildAtlas(string prefix)
    {
        Color32[] pixels = new Color32[AtlasWidth * AtlasHeight];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Transparent;

        for (int state = 0; state < StateCount; state++)
        {
            for (int direction = 0; direction < DirectionCount; direction++)
            {
                int row = state * DirectionCount + direction;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    Color32[] source = DrawFrame(prefix == "Enemy", state, frame);
                    if (state == 6 && frame >= 4)
                        source = Rotate(source, SourceSize, SourceSize, (frame - 3) * 18f);
                    source = Rotate(source, SourceSize, SourceSize, -direction * 45f);
                    source = ResizeNearest(source, SourceSize, SourceSize, CellSize, CellSize);

                    int destinationY = AtlasHeight - (row + 1) * CellSize;
                    for (int y = 0; y < CellSize; y++)
                    {
                        for (int x = 0; x < CellSize; x++)
                        {
                            int destinationIndex = (destinationY + CellSize - 1 - y) * AtlasWidth + frame * CellSize + x;
                            pixels[destinationIndex] = source[y * CellSize + x];
                        }
                    }
                }
            }
        }

        return pixels;
    }

    private static Color32[] DrawFrame(bool enemy, int state, int frame)
    {
        Palette p = enemy ? Palette.Enemy : Palette.Player;
        Color32[] pixels = new Color32[SourceSize * SourceSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Transparent;

        int[] walkSteps = { -3, -2, 0, 2, 3, 2 };
        int[] runSteps = { -5, -3, 1, 5, 3, -2 };
        int[] idleBob = { 0, -1, 0, 1, 0, 0 };
        int[] reloadShift = { 0, 0, 1, 3, 2, 0 };
        int[] recoil = { 0, 1, 2, 1, 0, 0 };

        int leg = state == 1 ? walkSteps[frame] : state == 2 ? runSteps[frame] : 0;
        int arm = -leg / 2;
        int bob = state == 0 ? idleBob[frame] : (state == 1 || state == 2 ? idleBob[frame] - (state == 2 ? 1 : 0) : 0);
        int hitLean = state == 5 ? (frame < 2 ? 0 : frame < 4 ? 2 : 1) : 0;
        int reload = state == 4 ? reloadShift[frame] : 0;
        int kick = state == 3 ? recoil[frame] : 0;
        int x = -hitLean;
        int y = bob;

        // Shadow, pack, legs and boots.
        Ellipse(pixels, 24, 42, 13, 3, new Color32(14, 17, 18, 125));
        Rect(pixels, 12 + x, 16 + y, 19 + x, 29 + y, p.Outline);
        Rect(pixels, 14 + x, 17 + y, 18 + x, 27 + y, p.Pack);
        Rect(pixels, 16 + x, 18 + y, 17 + x, 24 + y, p.HelmetHi);

        int leftLeg = state == 0 && frame == 2 ? 1 : leg;
        int rightLeg = -leg;
        Rect(pixels, 17 + x + leftLeg, 28 + y, 23 + x + leftLeg, 40 + y, p.Outline);
        Rect(pixels, 18 + x + leftLeg, 29 + y, 22 + x + leftLeg, 37 + y, p.Pants);
        Rect(pixels, 19 + x + leftLeg, 34 + y, 22 + x + leftLeg, 37 + y, p.PantsHi);
        Rect(pixels, 15 + x + leftLeg, 38 + y, 23 + x + leftLeg, 44 + y, p.Outline);
        Rect(pixels, 16 + x + leftLeg, 39 + y, 22 + x + leftLeg, 42 + y, p.Boots);

        Rect(pixels, 25 + x + rightLeg, 28 + y, 31 + x + rightLeg, 40 + y, p.Outline);
        Rect(pixels, 26 + x + rightLeg, 29 + y, 30 + x + rightLeg, 37 + y, p.Pants);
        Rect(pixels, 26 + x + rightLeg, 34 + y, 29 + x + rightLeg, 37 + y, p.PantsHi);
        Rect(pixels, 24 + x + rightLeg, 38 + y, 32 + x + rightLeg, 44 + y, p.Outline);
        Rect(pixels, 25 + x + rightLeg, 39 + y, 31 + x + rightLeg, 42 + y, p.Boots);

        // Torso, vest, shoulder pads and arms.
        Rect(pixels, 14 + x, 15 + y, 34 + x, 32 + y, p.Outline);
        Rect(pixels, 16 + x, 16 + y, 32 + x, 30 + y, p.Vest);
        Rect(pixels, 17 + x, 17 + y, 23 + x, 25 + y, p.VestHi);
        Rect(pixels, 26 + x, 18 + y, 30 + x, 24 + y, p.Helmet);
        Rect(pixels, 18 + x, 26 + y, 30 + x, 28 + y, p.Outline);
        Rect(pixels, 19 + x, 27 + y, 29 + x, 28 + y, p.Strap);

        Rect(pixels, 10 + x, 17 + y + arm / 2, 17 + x, 24 + y + arm / 2, p.Outline);
        Rect(pixels, 11 + x, 18 + y + arm / 2, 16 + x, 22 + y + arm / 2, p.VestHi);
        Rect(pixels, 30 + x, 17 + y - arm / 2, 36 + x, 24 + y - arm / 2, p.Outline);
        Rect(pixels, 31 + x, 18 + y - arm / 2, 35 + x, 22 + y - arm / 2, p.VestHi);

        // Helmet and face.
        Ellipse(pixels, 24 + x, 12 + y, 8, 8, p.Outline);
        Ellipse(pixels, 24 + x, 11 + y, 7, 7, p.Helmet);
        Ellipse(pixels, 24 + x, 8 + y, 5, 4, p.HelmetHi);
        Rect(pixels, 19 + x, 13 + y, 29 + x, 16 + y, p.Skin);
        Rect(pixels, 20 + x, 13 + y, 28 + x, 14 + y, p.SkinHi);
        Rect(pixels, 18 + x, 10 + y, 30 + x, 12 + y, p.Outline);
        Rect(pixels, 21 + x, 10 + y, 27 + x, 11 + y, p.HelmetHi);
        Rect(pixels, 16 + x, 10 + y, 18 + x, 13 + y, p.Strap);
        Rect(pixels, 30 + x, 10 + y, 32 + x, 13 + y, p.Strap);
        Rect(pixels, 28 + x, 21 + y, 30 + x, 24 + y, p.Accent);

        // Rifle, support hand and animated reload.
        int gunY = 25 + y + reload / 2 - kick;
        Rect(pixels, 28 + x - kick, gunY, 34 + x - kick, 43 + y - kick, p.Outline);
        Rect(pixels, 29 + x - kick, gunY + 1, 33 + x - kick, 41 + y - kick, p.Gun);
        Rect(pixels, 30 + x - kick, gunY + 3, 32 + x - kick, 39 + y - kick, p.GunHi);
        Rect(pixels, 27 + x - kick, gunY + 6, 35 + x - kick, gunY + 8, p.Outline);
        Rect(pixels, 28 + x - kick, gunY + 6, 34 + x - kick, gunY + 7, p.GunHi);
        Rect(pixels, 30 + x, 28 + y, 32 + x, 32 + y, p.Outline);

        if (state == 4 && frame >= 2 && frame <= 4)
        {
            Rect(pixels, 29 + x, 25 + y + reload, 34 + x, 29 + y + reload, p.Skin);
            Rect(pixels, 27 + x, 30 + y + reload, 30 + x, 34 + y + reload, p.Outline);
        }

        if (state == 3 && (frame == 1 || frame == 2 || frame == 4))
        {
            Line(pixels, 31 + x - kick, 42 + y - kick, 24 + x - kick, 46 + y, 2, p.Flash);
            Rect(pixels, 29 + x - kick, 42 + y - kick, 33 + x - kick, 44 + y - kick, p.FlashHi);
        }

        if (state == 5 && frame >= 2 && frame <= 4)
        {
            Line(pixels, 14 + x, 18 + y, 19 + x, 24 + y, 2, p.Hurt);
            Rect(pixels, 31 + x, 17 + y, 35 + x, 20 + y, p.Hurt);
        }

        if (state == 6 && frame >= 4)
            Rect(pixels, 14, 26 + y, 19, 29 + y, p.Hurt);

        return pixels;
    }

    private static void Rect(Color32[] pixels, int x1, int y1, int x2, int y2, Color32 color)
    {
        int left = Mathf.Max(0, Mathf.Min(x1, x2));
        int right = Mathf.Min(SourceSize - 1, Mathf.Max(x1, x2));
        int top = Mathf.Max(0, Mathf.Min(y1, y2));
        int bottom = Mathf.Min(SourceSize - 1, Mathf.Max(y1, y2));
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                pixels[y * SourceSize + x] = color;
    }

    private static void Ellipse(Color32[] pixels, int cx, int cy, int rx, int ry, Color32 color)
    {
        for (int y = Mathf.Max(0, cy - ry); y <= Mathf.Min(SourceSize - 1, cy + ry); y++)
        {
            for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(SourceSize - 1, cx + rx); x++)
            {
                float dx = (x - cx) / Mathf.Max(1f, rx);
                float dy = (y - cy) / Mathf.Max(1f, ry);
                if (dx * dx + dy * dy <= 1f)
                    pixels[y * SourceSize + x] = color;
            }
        }
    }

    private static void Line(Color32[] pixels, int x1, int y1, int x2, int y2, int width, Color32 color)
    {
        int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x1, x2, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y1, y2, t));
            Ellipse(pixels, x, y, Mathf.Max(1, width / 2), Mathf.Max(1, width / 2), color);
        }
    }

    private static Color32[] Rotate(Color32[] source, int width, int height, float angleDegrees)
    {
        Color32[] destination = new Color32[source.Length];
        for (int i = 0; i < destination.Length; i++) destination[i] = Transparent;
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 color = source[y * width + x];
                if (color.a == 0) continue;
                float dx = x - centerX;
                float dy = y - centerY;
                int nx = Mathf.RoundToInt(centerX + dx * cosine - dy * sine);
                int ny = Mathf.RoundToInt(centerY + dx * sine + dy * cosine);
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    destination[ny * width + nx] = color;
            }
        }

        return destination;
    }

    private static Color32[] ResizeNearest(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        Color32[] destination = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            int sourceY = Mathf.Min(sourceHeight - 1, y * sourceHeight / height);
            for (int x = 0; x < width; x++)
            {
                int sourceX = Mathf.Min(sourceWidth - 1, x * sourceWidth / width);
                destination[y * width + x] = source[sourceY * sourceWidth + sourceX];
            }
        }
        return destination;
    }

    private sealed class Palette
    {
        public readonly Color32 Outline, Helmet, HelmetHi, Vest, VestHi, Pants, PantsHi;
        public readonly Color32 Skin, SkinHi, Boots, Strap, Gun, GunHi, Pack, Accent, Flash, FlashHi, Hurt;

        private Palette(bool enemy)
        {
            Outline = enemy ? C(16,20,19) : C(18,23,29);
            Helmet = enemy ? C(48,65,37) : C(38,59,78);
            HelmetHi = enemy ? C(112,136,69) : C(91,128,154);
            Vest = enemy ? C(62,83,43) : C(49,83,105);
            VestHi = enemy ? C(132,151,83) : C(104,143,162);
            Pants = enemy ? C(52,58,38) : C(48,64,76);
            PantsHi = enemy ? C(101,110,68) : C(91,112,126);
            Skin = enemy ? C(187,133,99) : C(207,151,112);
            SkinHi = enemy ? C(224,168,123) : C(235,184,140);
            Boots = enemy ? C(22,25,22) : C(24,29,34);
            Strap = enemy ? C(168,129,67) : C(169,132,78);
            Gun = enemy ? C(27,31,29) : C(29,34,39);
            GunHi = enemy ? C(121,127,113) : C(122,133,136);
            Pack = enemy ? C(42,57,31) : C(38,68,81);
            Accent = enemy ? C(194,76,58) : C(75,180,192);
            Flash = C(255,202,77);
            FlashHi = C(255,245,180);
            Hurt = C(230,60,55);
        }

        private static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        public static readonly Palette Player = new Palette(false);
        public static readonly Palette Enemy = new Palette(true);
    }
}
#endif
