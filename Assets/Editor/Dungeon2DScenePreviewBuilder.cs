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
        private const string PreviewStyleMarkerName = "PreviewStyle_FloorBridgeWallV4";

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
                if (IsPreviewCurrent(existing))
                {
                    EnsureCamera();
                    return;
                }

                Object.DestroyImmediate(existing);
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
                if (!force && IsPreviewCurrent(existing))
                {
                    EnsureCamera();
                    return;
                }

                Object.DestroyImmediate(existing);
            }

            Dungeon2DArtLibrary art = new Dungeon2DArtLibrary();
            GameObject root = new GameObject(PreviewRootName);
            root.transform.position = Vector3.zero;
            GameObject marker = new GameObject(PreviewStyleMarkerName);
            marker.hideFlags = HideFlags.HideInHierarchy;
            marker.transform.SetParent(root.transform, false);

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

        private static bool IsPreviewCurrent(GameObject existing)
        {
            return existing != null && existing.transform.Find(PreviewStyleMarkerName) != null;
        }

        private static void BuildRoom(Dungeon2DArtLibrary art, Transform root, int index, RoomKind kind, Vector2 center, Vector2 size)
        {
            GameObject room = new GameObject("PreviewRoom_" + (index + 1) + "_" + kind);
            room.transform.SetParent(root, false);
            room.transform.position = center;

            Rect bounds = new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
            GameObject floor = CreateSprite("PreviewRoomFloor", art.GetEnvironmentSprite(EnvironmentSpriteId.RoomFloor), room.transform, center, 0);
            ScaleSpriteToWorldSize(floor, size + Vector2.one * Dungeon2DConfig.RoomWallInset);
            float halfBridgeGap = Dungeon2DConfig.BridgeWallVisualGapWidth * 0.5f;

            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true, index > 0 ? halfBridgeGap : 0f);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true, index < Dungeon2DConfig.TargetRoomCount - 1 ? halfBridgeGap : 0f);

            if (index < Dungeon2DConfig.TargetRoomCount - 1)
            {
                CreateDoorPreview(art, room.transform, new Vector2(0f, bounds.yMax + Dungeon2DConfig.DoorVerticalOffset));
            }
        }

        private static void BuildCorridor(Dungeon2DArtLibrary art, Transform root, int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("PreviewCorridor_" + (index + 1));
            corridor.transform.SetParent(root, false);
            float centerY = (fromY + toY) * 0.5f;
            float height = Mathf.Abs(toY - fromY);
            GameObject bridge = CreateSprite("PreviewWoodenBridge", art.GetEnvironmentSprite(EnvironmentSpriteId.WoodenBridge), corridor.transform, new Vector2(0f, centerY), 1);
            ScaleSpriteToWorldSize(bridge, new Vector2(Dungeon2DConfig.BridgeVisualWidth, height + Dungeon2DConfig.BridgeVisualOverlap));
        }

        private static void BuildWallVisualLine(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal, float centerGapHalfWidth = 0f)
        {
            if (horizontal && centerGapHalfWidth > 0f)
            {
                BuildWallVisualSegment(art, parent, start, new Vector2(-centerGapHalfWidth, start.y), true);
                BuildWallVisualSegment(art, parent, new Vector2(centerGapHalfWidth, end.y), end, true);
                return;
            }

            BuildWallVisualSegment(art, parent, start, end, horizontal);
        }

        private static void BuildWallVisualSegment(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            if (length <= 0.05f)
            {
                return;
            }

            Vector2 center = (start + end) * 0.5f;
            Vector2 size = horizontal
                ? new Vector2(length, Dungeon2DConfig.WallVisualThickness)
                : new Vector2(Dungeon2DConfig.WallVisualThickness, length);
            EnvironmentSpriteId spriteId = horizontal ? EnvironmentSpriteId.WallHorizontal : EnvironmentSpriteId.WallVertical;
            GameObject wall = CreateSprite(horizontal ? "PreviewHorizontalWall" : "PreviewVerticalWall", art.GetEnvironmentSprite(spriteId), parent, center, 6);
            ScaleSpriteToWorldSize(wall, size);
        }

        private static void CreateDoorPreview(Dungeon2DArtLibrary art, Transform parent, Vector2 position)
        {
            GameObject door = CreateSprite("PreviewBridgeDoor", art.GetEnvironmentSprite(EnvironmentSpriteId.BridgeDoorOpen), parent, position, 8);
            ScaleSpriteToWorldWidth(door, Dungeon2DConfig.BridgeDoorOpenVisualWidth);
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

        private static void ScaleSpriteToWorldSize(GameObject spriteObject, Vector2 size)
        {
            if (spriteObject == null)
            {
                return;
            }

            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = renderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            spriteObject.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }

        private static void ScaleSpriteToWorldWidth(GameObject spriteObject, float width)
        {
            if (spriteObject == null)
            {
                return;
            }

            SpriteRenderer renderer = spriteObject.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null || renderer.sprite.bounds.size.x <= 0f)
            {
                return;
            }

            float scale = width / renderer.sprite.bounds.size.x;
            spriteObject.transform.localScale = Vector3.one * scale;
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
