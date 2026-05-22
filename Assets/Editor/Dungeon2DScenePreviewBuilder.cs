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
        private const string PreviewStyleMarkerName = "PreviewStyle_FloorBridgeWallV6";

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
            float cornerInset = Dungeon2DConfig.WallCornerSize * 0.5f;

            BuildWallContactShadowLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false, 1f);
            BuildWallContactShadowLine(art, room.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false, -1f);
            BuildWallContactShadowLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true, 1f, index > 0 ? halfBridgeGap : 0f);
            BuildWallContactShadowLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true, -1f, index < Dungeon2DConfig.TargetRoomCount - 1 ? halfBridgeGap : 0f);

            BuildWallCornerPillars(art, room.transform, bounds);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin, bounds.yMin + cornerInset), new Vector2(bounds.xMin, bounds.yMax - cornerInset), false, 0f, false, false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMax, bounds.yMin + cornerInset), new Vector2(bounds.xMax, bounds.yMax - cornerInset), false, 0f, true, false);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin + cornerInset, bounds.yMin), new Vector2(bounds.xMax - cornerInset, bounds.yMin), true, index > 0 ? halfBridgeGap : 0f, false, true);
            BuildWallVisualLine(art, room.transform, new Vector2(bounds.xMin + cornerInset, bounds.yMax), new Vector2(bounds.xMax - cornerInset, bounds.yMax), true, index < Dungeon2DConfig.TargetRoomCount - 1 ? halfBridgeGap : 0f, false, false);

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

        private static void BuildWallVisualLine(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal, float centerGapHalfWidth = 0f, bool flipX = false, bool flipY = false)
        {
            if (horizontal && centerGapHalfWidth > 0f)
            {
                BuildWallVisualSegment(art, parent, start, new Vector2(-centerGapHalfWidth, start.y), true, flipX, flipY);
                BuildWallVisualSegment(art, parent, new Vector2(centerGapHalfWidth, end.y), end, true, flipX, flipY);
                return;
            }

            BuildWallVisualSegment(art, parent, start, end, horizontal, flipX, flipY);
        }

        private static void BuildWallVisualSegment(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal, bool flipX, bool flipY)
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

            SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = flipX;
                renderer.flipY = flipY;
            }
        }

        private static void BuildWallCornerPillars(Dungeon2DArtLibrary art, Transform parent, Rect bounds)
        {
            BuildWallCornerPillar(art, parent, new Vector2(bounds.xMin, bounds.yMin), false, true);
            BuildWallCornerPillar(art, parent, new Vector2(bounds.xMax, bounds.yMin), true, true);
            BuildWallCornerPillar(art, parent, new Vector2(bounds.xMin, bounds.yMax), false, false);
            BuildWallCornerPillar(art, parent, new Vector2(bounds.xMax, bounds.yMax), true, false);
        }

        private static void BuildWallCornerPillar(Dungeon2DArtLibrary art, Transform parent, Vector2 position, bool flipX, bool flipY)
        {
            GameObject pillar = CreateSprite("PreviewWallCornerPillar", art.GetEnvironmentSprite(EnvironmentSpriteId.WallCornerPillar), parent, position, 7);
            ScaleSpriteToWorldSize(pillar, Vector2.one * Dungeon2DConfig.WallCornerSize);

            SpriteRenderer renderer = pillar.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = flipX;
                renderer.flipY = flipY;
            }
        }

        private static void BuildWallContactShadowLine(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal, float inwardSign, float centerGapHalfWidth = 0f)
        {
            if (horizontal && centerGapHalfWidth > 0f)
            {
                BuildWallContactShadowSegment(art, parent, start, new Vector2(-centerGapHalfWidth, start.y), true, inwardSign);
                BuildWallContactShadowSegment(art, parent, new Vector2(centerGapHalfWidth, end.y), end, true, inwardSign);
                return;
            }

            BuildWallContactShadowSegment(art, parent, start, end, horizontal, inwardSign);
        }

        private static void BuildWallContactShadowSegment(Dungeon2DArtLibrary art, Transform parent, Vector2 start, Vector2 end, bool horizontal, float inwardSign)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            if (length <= 0.05f)
            {
                return;
            }

            float width = Dungeon2DConfig.WallContactShadowWidth;
            Vector2 center = (start + end) * 0.5f;
            center += horizontal
                ? Vector2.up * inwardSign * width * 0.5f
                : Vector2.right * inwardSign * width * 0.5f;

            Vector2 size = horizontal ? new Vector2(length, width) : new Vector2(width, length);
            EnvironmentSpriteId spriteId = horizontal ? EnvironmentSpriteId.WallShadowHorizontal : EnvironmentSpriteId.WallShadowVertical;
            GameObject shadow = CreateSprite(horizontal ? "PreviewHorizontalWallContactShadow" : "PreviewVerticalWallContactShadow", art.GetEnvironmentSprite(spriteId), parent, center, 2);
            ScaleSpriteToWorldSize(shadow, size);

            SpriteRenderer renderer = shadow.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = !horizontal && inwardSign < 0f;
                renderer.flipY = horizontal && inwardSign > 0f;
            }
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
