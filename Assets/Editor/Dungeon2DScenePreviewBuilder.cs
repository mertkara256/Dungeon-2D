#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon2D.EditorTools
{
    [InitializeOnLoad]
    public static class Dungeon2DScenePreviewBuilder
    {
        public const string PreviewRootName = "Dungeon2D_EditModePreview";

        private const int TargetRoomCount = 10;
        private const float DefaultRoomWidth = 18f;
        private const float DefaultRoomHeight = 13f;
        private const float BossRoomWidth = 24f;
        private const float BossRoomHeight = 18f;
        private const float CorridorLength = 5f;
        private const float DoorWidth = 4.1f;
        private const float TileSize = 2f;

        static Dungeon2DScenePreviewBuilder()
        {
            EditorApplication.delayCall += EnsurePreview;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
        }

        [MenuItem("Tools/Dungeon 2D/Rebuild Scene Preview")]
        public static void RebuildPreviewMenu()
        {
            RebuildPreview(true);
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            EditorApplication.delayCall += EnsurePreview;
        }

        private static void EnsurePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject existing = GameObject.Find(PreviewRootName);
            if (existing != null)
            {
                EnsureCamera();
                return;
            }

            RebuildPreview(false);
        }

        private static void RebuildPreview(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameObject existing = GameObject.Find(PreviewRootName);
            if (existing != null)
            {
                if (!force)
                {
                    EnsureCamera();
                    return;
                }

                Object.DestroyImmediate(existing);
            }

            Dungeon2DArtLibrary art = new Dungeon2DArtLibrary();
            GameObject root = new GameObject(PreviewRootName);
            root.transform.position = Vector3.zero;

            float previousCenterY = 0f;
            float previousHeight = 0f;
            bool hasPrevious = false;

            for (int i = 0; i < TargetRoomCount; i++)
            {
                RoomKind kind = DetermineRoomKind(i);
                Vector2 size = GetRoomSize(kind);
                float centerY = hasPrevious
                    ? previousCenterY + previousHeight * 0.5f + CorridorLength + size.y * 0.5f
                    : 0f;

                if (hasPrevious)
                {
                    BuildCorridor(art, root.transform, i - 1, previousCenterY + previousHeight * 0.5f, centerY - size.y * 0.5f);
                }

                BuildRoom(art, root.transform, i, kind, new Vector2(0f, centerY), size);
                previousCenterY = centerY;
                previousHeight = size.y;
                hasPrevious = true;
            }

            EnsureCamera();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void BuildRoom(Dungeon2DArtLibrary art, Transform root, int index, RoomKind kind, Vector2 center, Vector2 size)
        {
            GameObject room = new GameObject("PreviewRoom_" + (index + 1) + "_" + kind);
            room.transform.SetParent(root, false);
            room.transform.position = center;

            Rect bounds = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            int columns = Mathf.CeilToInt(size.x / TileSize);
            int rows = Mathf.CeilToInt(size.y / TileSize);

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector2 position = new Vector2(bounds.xMin + TileSize * 0.5f + x * TileSize, bounds.yMin + TileSize * 0.5f + y * TileSize);
                    EnvironmentSpriteId spriteId = EnvironmentSpriteId.Floor;
                    int noise = Mathf.Abs((x * 31 + y * 17 + index * 13) % 7);
                    if (noise == 0)
                    {
                        spriteId = EnvironmentSpriteId.CrackedFloor;
                    }
                    else if (noise == 3)
                    {
                        spriteId = EnvironmentSpriteId.MossFloor;
                    }

                    CreateSprite("PreviewFloor", art.GetEnvironmentSprite(spriteId), room.transform, position, 0);
                }
            }

            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true);

            if (index > 0)
            {
                CreateDoorPreview(art, room.transform, new Vector2(0f, bounds.yMin - 0.22f));
            }

            if (index < TargetRoomCount - 1)
            {
                CreateDoorPreview(art, room.transform, new Vector2(0f, bounds.yMax + 0.22f));
            }
        }

        private static void BuildCorridor(Dungeon2DArtLibrary art, Transform root, int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("PreviewCorridor_" + (index + 1));
            corridor.transform.SetParent(root, false);
            int rows = Mathf.CeilToInt(Mathf.Abs(toY - fromY) / TileSize);

            for (int y = 0; y < rows; y++)
            {
                Vector2 position = new Vector2(0f, Mathf.Min(fromY, toY) + TileSize * 0.5f + y * TileSize);
                CreateSprite("PreviewCorridorFloor", art.GetEnvironmentSprite(EnvironmentSpriteId.CrackedFloor), corridor.transform, position, 0);
            }

            for (int y = 0; y <= rows; y++)
            {
                float rowY = Mathf.Min(fromY, toY) + y * TileSize;
                CreateSprite("PreviewCorridorWallLeft", art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(-DoorWidth * 0.5f - 0.45f, rowY), 7);
                CreateSprite("PreviewCorridorWallRight", art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(DoorWidth * 0.5f + 0.45f, rowY), 7);
            }
        }

        private static void BuildWallVisualLine(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / TileSize));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0f : i / (float)count;
                Vector2 position = Vector2.Lerp(start, end, t);
                GameObject wall = CreateSprite("PreviewWall", art.GetEnvironmentSprite(EnvironmentSpriteId.Wall), parent, position, 6);
                wall.transform.localScale = Vector3.one * 0.92f;
            }
        }

        private static void CreateDoorPreview(Dungeon2DArtLibrary art, Transform parent, Vector2 position)
        {
            GameObject door = CreateSprite("PreviewDoor", art.GetEnvironmentSprite(EnvironmentSpriteId.Archway), parent, position, 8);
            door.transform.localScale = new Vector3(1.55f, 0.72f, 1f);
            SpriteRenderer renderer = door.GetComponent<SpriteRenderer>();
            renderer.color = new Color(0.82f, 0.82f, 0.86f, 0.72f);
        }

        private static GameObject CreateSprite(string name, Sprite sprite, Transform parent, Vector2 position, int sortingOrder)
        {
            GameObject spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, true);
            spriteObject.transform.position = position;
            SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return spriteObject;
        }

        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.026f, 0.03f, 0.038f, 1f);
        }

        private static RoomKind DetermineRoomKind(int index)
        {
            if (index == TargetRoomCount - 1)
            {
                return RoomKind.Boss;
            }

            if (index > 0 && index % 3 == 0)
            {
                return RoomKind.Upgrade;
            }

            if (index > 0 && index % 4 == 0)
            {
                return RoomKind.Treasure;
            }

            return RoomKind.Combat;
        }

        private static Vector2 GetRoomSize(RoomKind kind)
        {
            return kind == RoomKind.Boss
                ? new Vector2(BossRoomWidth, BossRoomHeight)
                : new Vector2(DefaultRoomWidth, DefaultRoomHeight);
        }
    }
}
#endif
