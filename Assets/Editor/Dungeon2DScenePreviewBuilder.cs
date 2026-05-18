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
        public const string PreviewRootName = Dungeon2DConfig.EditModePreviewName;

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

            for (int i = 0; i < Dungeon2DConfig.TargetRoomCount; i++)
            {
                RoomKind kind = Dungeon2DConfig.DetermineRoomKind(i);
                Vector2 size = Dungeon2DConfig.GetRoomSize(kind);
                float centerY = hasPrevious
                    ? previousCenterY + previousHeight * 0.5f + Dungeon2DConfig.CorridorLength + size.y * 0.5f
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
            int columns = Mathf.CeilToInt(size.x / Dungeon2DConfig.TileSize);
            int rows = Mathf.CeilToInt(size.y / Dungeon2DConfig.TileSize);

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector2 position = new Vector2(
                        bounds.xMin + Dungeon2DConfig.TileSize * 0.5f + x * Dungeon2DConfig.TileSize,
                        bounds.yMin + Dungeon2DConfig.TileSize * 0.5f + y * Dungeon2DConfig.TileSize);
                    EnvironmentSpriteId spriteId = Dungeon2DConfig.GetFloorSpriteId(x, y, index);

                    CreateSprite("PreviewFloor", art.GetEnvironmentSprite(spriteId), room.transform, position, 0);
                }
            }

            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true);

            if (index > 0)
            {
                CreateDoorPreview(art, room.transform, new Vector2(0f, bounds.yMin - Dungeon2DConfig.DoorVerticalOffset));
            }

            if (index < Dungeon2DConfig.TargetRoomCount - 1)
            {
                CreateDoorPreview(art, room.transform, new Vector2(0f, bounds.yMax + Dungeon2DConfig.DoorVerticalOffset));
            }
        }

        private static void BuildCorridor(Dungeon2DArtLibrary art, Transform root, int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("PreviewCorridor_" + (index + 1));
            corridor.transform.SetParent(root, false);
            int rows = Mathf.CeilToInt(Mathf.Abs(toY - fromY) / Dungeon2DConfig.TileSize);

            for (int y = 0; y < rows; y++)
            {
                Vector2 position = new Vector2(0f, Mathf.Min(fromY, toY) + Dungeon2DConfig.TileSize * 0.5f + y * Dungeon2DConfig.TileSize);
                CreateSprite("PreviewCorridorFloor", art.GetEnvironmentSprite(EnvironmentSpriteId.CrackedFloor), corridor.transform, position, 0);
            }

            for (int y = 0; y <= rows; y++)
            {
                float rowY = Mathf.Min(fromY, toY) + y * Dungeon2DConfig.TileSize;
                CreateSprite("PreviewCorridorWallLeft", art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(-Dungeon2DConfig.DoorWidth * 0.5f - Dungeon2DConfig.CorridorWallVisualInset, rowY), 7);
                CreateSprite("PreviewCorridorWallRight", art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(Dungeon2DConfig.DoorWidth * 0.5f + Dungeon2DConfig.CorridorWallVisualInset, rowY), 7);
            }
        }

        private static void BuildWallVisualLine(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / Dungeon2DConfig.TileSize));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0f : i / (float)count;
                Vector2 position = Vector2.Lerp(start, end, t);
                GameObject wall = CreateSprite("PreviewWall", art.GetEnvironmentSprite(EnvironmentSpriteId.Wall), parent, position, 6);
                wall.transform.localScale = Vector3.one * Dungeon2DConfig.WallVisualScale;
            }
        }

        private static void CreateDoorPreview(Dungeon2DArtLibrary art, Transform parent, Vector2 position)
        {
            GameObject door = CreateSprite("PreviewDoor", art.GetEnvironmentSprite(EnvironmentSpriteId.Archway), parent, position, 8);
            door.transform.localScale = new Vector3(Dungeon2DConfig.DoorVisualWidth, Dungeon2DConfig.DoorOpenVisualHeight, 1f);
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
            camera.orthographicSize = Dungeon2DConfig.CameraOrthographicSize;
            camera.transform.position = new Vector3(0f, 0f, Dungeon2DConfig.CameraDepth);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Dungeon2DConfig.CameraBackgroundColor;
        }
    }
}
#endif
