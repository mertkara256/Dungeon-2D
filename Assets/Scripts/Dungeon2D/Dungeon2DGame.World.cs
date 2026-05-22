using UnityEngine;

namespace Dungeon2D
{
    public sealed partial class Dungeon2DGame
    {
        private void BuildPreviewWorld()
        {
            if (worldRoot != null)
            {
                return;
            }

            BuildDungeon();
            if (rooms.Count > 0 && mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(rooms[0].Center.x, rooms[0].Center.y, Dungeon2DConfig.CameraDepth);
            }
        }

        private static void DestroyEditModePreview()
        {
            GameObject preview = GameObject.Find(Dungeon2DConfig.EditModePreviewName);
            if (preview != null)
            {
                Destroy(preview);
            }
        }

        private void BuildDungeon()
        {
            worldRoot = new GameObject(Dungeon2DConfig.WorldRootName).transform;
            rooms.Clear();

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
                    BuildCorridor(i - 1, previousCenterY + previousHeight * 0.5f, centerY - size.y * 0.5f);
                }

                DungeonRoom2D room = BuildRoom(i, kind, new Vector2(0f, centerY), size);
                rooms.Add(room);
                previousCenterY = centerY;
                previousHeight = size.y;
                hasPrevious = true;
            }
        }

        private DungeonRoom2D BuildRoom(int index, RoomKind kind, Vector2 center, Vector2 size)
        {
            GameObject roomObject = new GameObject("Room_" + (index + 1) + "_" + kind);
            roomObject.transform.SetParent(worldRoot, false);
            roomObject.transform.position = center;

            DungeonRoom2D room = new DungeonRoom2D
            {
                Index = index,
                Kind = kind,
                Root = roomObject,
                Center = center,
                Size = size,
                SpawnPoint = center + new Vector2(0f, -size.y * 0.5f + Dungeon2DConfig.PlayerSpawnBottomPadding)
            };

            BuildRoomFloor(room);
            BuildWallContactShadows(room);
            BuildWalls(room);
            return room;
        }

        private void BuildRoomFloor(DungeonRoom2D room)
        {
            GameObject floor = CreateSpriteObject("RoomFloor", Art.GetEnvironmentSprite(EnvironmentSpriteId.RoomFloor), room.Root.transform, room.Center, 0);
            Vector2 visualSize = room.Size + Vector2.one * Dungeon2DConfig.RoomWallInset;
            ScaleSpriteToWorldSize(floor, visualSize);
        }

        private void BuildWalls(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            float halfDoor = Dungeon2DConfig.DoorWidth * 0.5f;
            float halfBridgeBodyGap = Dungeon2DConfig.BridgeWallBodyGapWidth * 0.5f;
            float cornerInset = Dungeon2DConfig.WallCornerSize * 0.5f;
            BuildWallCornerPillars(room.Root.transform, bounds);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin + cornerInset), new Vector2(bounds.xMin, bounds.yMax - cornerInset), false, 0f, false, false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMax, bounds.yMin + cornerInset), new Vector2(bounds.xMax, bounds.yMax - cornerInset), false, 0f, true, false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin + cornerInset, bounds.yMin), new Vector2(bounds.xMax - cornerInset, bounds.yMin), true, room.Index > 0 ? halfBridgeBodyGap : 0f, false, false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin + cornerInset, bounds.yMax), new Vector2(bounds.xMax - cornerInset, bounds.yMax), true, room.Index < Dungeon2DConfig.TargetRoomCount - 1 ? halfBridgeBodyGap : 0f, false, false);

            if (room.Index > 0)
            {
                BuildBridgeOpeningCornerPillars(room.Root.transform, bounds.yMin);
            }

            if (room.Index < Dungeon2DConfig.TargetRoomCount - 1)
            {
                BuildBridgeOpeningCornerPillars(room.Root.transform, bounds.yMax);
            }

            AddWallCollider(room.Root.transform, "LeftWallCollider", new Vector2(bounds.xMin - Dungeon2DConfig.RoomWallInset, room.Center.y), new Vector2(Dungeon2DConfig.RoomWallThickness, room.Size.y + Dungeon2DConfig.RoomWallPadding));
            AddWallCollider(room.Root.transform, "RightWallCollider", new Vector2(bounds.xMax + Dungeon2DConfig.RoomWallInset, room.Center.y), new Vector2(Dungeon2DConfig.RoomWallThickness, room.Size.y + Dungeon2DConfig.RoomWallPadding));

            if (room.Index > 0)
            {
                AddWallCollider(room.Root.transform, "BottomWallLeft", new Vector2((bounds.xMin - halfDoor) * 0.5f, bounds.yMin - Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
                AddWallCollider(room.Root.transform, "BottomWallRight", new Vector2((bounds.xMax + halfDoor) * 0.5f, bounds.yMin - Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
            }
            else
            {
                AddWallCollider(room.Root.transform, "BottomWallCollider", new Vector2(room.Center.x, bounds.yMin - Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x + Dungeon2DConfig.RoomWallPadding, Dungeon2DConfig.RoomWallThickness));
            }

            if (room.Index < Dungeon2DConfig.TargetRoomCount - 1)
            {
                AddWallCollider(room.Root.transform, "TopWallLeft", new Vector2((bounds.xMin - halfDoor) * 0.5f, bounds.yMax + Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
                AddWallCollider(room.Root.transform, "TopWallRight", new Vector2((bounds.xMax + halfDoor) * 0.5f, bounds.yMax + Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
                room.ExitDoor = CreateDoor(room.Root.transform, new Vector2(0f, bounds.yMax + Dungeon2DConfig.DoorVerticalOffset), true);
            }
            else
            {
                AddWallCollider(room.Root.transform, "TopWallCollider", new Vector2(room.Center.x, bounds.yMax + Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x + Dungeon2DConfig.RoomWallPadding, Dungeon2DConfig.RoomWallThickness));
            }
        }

        private void BuildWallVisualLine(Transform parent, Vector2 start, Vector2 end, bool horizontal, float centerGapHalfWidth = 0f, bool flipX = false, bool flipY = false)
        {
            if (horizontal && centerGapHalfWidth > 0f)
            {
                BuildWallVisualSegment(parent, start, new Vector2(-centerGapHalfWidth, start.y), true, flipX, flipY);
                BuildWallVisualSegment(parent, new Vector2(centerGapHalfWidth, end.y), end, true, flipX, flipY);
                return;
            }

            BuildWallVisualSegment(parent, start, end, horizontal, flipX, flipY);
        }

        private void BuildWallVisualSegment(Transform parent, Vector2 start, Vector2 end, bool horizontal, bool flipX, bool flipY)
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
            GameObject wall = CreateSpriteObject(horizontal ? "HorizontalWall" : "VerticalWall", Art.GetEnvironmentSprite(spriteId), parent, center, 6);
            ScaleSpriteToWorldSize(wall, size);

            SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = flipX;
                renderer.flipY = flipY;
            }
        }

        private void BuildWallCornerPillars(Transform parent, Rect bounds)
        {
            BuildWallCornerPillar(parent, new Vector2(bounds.xMin, bounds.yMin), false, false);
            BuildWallCornerPillar(parent, new Vector2(bounds.xMax, bounds.yMin), true, false);
            BuildWallCornerPillar(parent, new Vector2(bounds.xMin, bounds.yMax), false, false);
            BuildWallCornerPillar(parent, new Vector2(bounds.xMax, bounds.yMax), true, false);
        }

        private void BuildWallCornerPillar(Transform parent, Vector2 position, bool flipX, bool flipY)
        {
            GameObject pillar = CreateSpriteObject("WallCornerPillar", Art.GetEnvironmentSprite(EnvironmentSpriteId.WallCornerPillar), parent, position, 7);
            ScaleSpriteToWorldSize(pillar, Vector2.one * Dungeon2DConfig.WallCornerSize);

            SpriteRenderer renderer = pillar.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = flipX;
                renderer.flipY = flipY;
            }
        }

        private void BuildBridgeOpeningCornerPillars(Transform parent, float y)
        {
            float halfClearGap = Dungeon2DConfig.BridgeClearVisualGapWidth * 0.5f;
            float halfCorner = Dungeon2DConfig.WallCornerSize * 0.5f;
            BuildWallCornerPillar(parent, new Vector2(-halfClearGap - halfCorner, y), true, false);
            BuildWallCornerPillar(parent, new Vector2(halfClearGap + halfCorner, y), false, false);
        }

        private void BuildWallContactShadows(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            float halfBridgeGap = Dungeon2DConfig.BridgeClearVisualGapWidth * 0.5f;

            BuildWallContactShadowLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false, 1f);
            BuildWallContactShadowLine(room.Root.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false, -1f);
            BuildWallContactShadowLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true, 1f, room.Index > 0 ? halfBridgeGap : 0f);
            BuildWallContactShadowLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true, -1f, room.Index < Dungeon2DConfig.TargetRoomCount - 1 ? halfBridgeGap : 0f);
        }

        private void BuildWallContactShadowLine(Transform parent, Vector2 start, Vector2 end, bool horizontal, float inwardSign, float centerGapHalfWidth = 0f)
        {
            if (horizontal && centerGapHalfWidth > 0f)
            {
                BuildWallContactShadowSegment(parent, start, new Vector2(-centerGapHalfWidth, start.y), true, inwardSign);
                BuildWallContactShadowSegment(parent, new Vector2(centerGapHalfWidth, end.y), end, true, inwardSign);
                return;
            }

            BuildWallContactShadowSegment(parent, start, end, horizontal, inwardSign);
        }

        private void BuildWallContactShadowSegment(Transform parent, Vector2 start, Vector2 end, bool horizontal, float inwardSign)
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
            GameObject shadow = CreateSpriteObject(horizontal ? "HorizontalWallContactShadow" : "VerticalWallContactShadow", Art.GetEnvironmentSprite(spriteId), parent, center, 2);
            ScaleSpriteToWorldSize(shadow, size);

            SpriteRenderer renderer = shadow.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.flipX = !horizontal && inwardSign < 0f;
                renderer.flipY = horizontal && inwardSign > 0f;
            }
        }

        private void BuildCorridor(int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("Corridor_" + (index + 1));
            corridor.transform.SetParent(worldRoot, false);
            float centerY = (fromY + toY) * 0.5f;
            float height = Mathf.Abs(toY - fromY);

            GameObject bridge = CreateSpriteObject("WoodenBridge", Art.GetEnvironmentSprite(EnvironmentSpriteId.WoodenBridge), corridor.transform, new Vector2(0f, centerY), 1);
            ScaleSpriteToWorldSize(bridge, new Vector2(Dungeon2DConfig.BridgeVisualWidth, height + Dungeon2DConfig.BridgeVisualOverlap));

            float halfDoor = Dungeon2DConfig.DoorWidth * 0.5f;
            AddWallCollider(corridor.transform, "CorridorLeftWall", new Vector2(-halfDoor - Dungeon2DConfig.RoomWallInset, centerY), new Vector2(Dungeon2DConfig.RoomWallThickness, height));
            AddWallCollider(corridor.transform, "CorridorRightWall", new Vector2(halfDoor + Dungeon2DConfig.RoomWallInset, centerY), new Vector2(Dungeon2DConfig.RoomWallThickness, height));
        }

        private DungeonDoor2D CreateDoor(Transform parent, Vector2 position, bool exit)
        {
            GameObject doorObject = CreateSpriteObject(exit ? "ExitBridgeDoor" : "EntranceBridgeDoor", Art.GetEnvironmentSprite(EnvironmentSpriteId.BridgeDoorClosed), parent, position, 8);
            BoxCollider2D collider = doorObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(Dungeon2DConfig.DoorWidth, Dungeon2DConfig.DoorColliderHeight);
            collider.sharedMaterial = Dungeon2DPhysics.Material;
            DungeonDoor2D door = doorObject.AddComponent<DungeonDoor2D>();
            door.Configure(Art, true);
            return door;
        }

        private static void AddWallCollider(Transform parent, string name, Vector2 center, Vector2 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.sharedMaterial = Dungeon2DPhysics.Material;
        }

        private GameObject CreateSpriteObject(string name, Sprite sprite, Transform parent, Vector2 position, int sortingOrder)
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

        private void ClearRun()
        {
            if (worldRoot != null)
            {
                Destroy(worldRoot.gameObject);
                worldRoot = null;
            }

            enemies.Clear();
            rooms.Clear();
            activeBoss = null;
            currentRoom = null;
            currentInteractable = null;
            Player = null;
        }

        private void EnsureCamera()
        {
            if (mainCamera != null)
            {
                return;
            }

            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = Dungeon2DConfig.CameraOrthographicSize;
            mainCamera.transform.position = new Vector3(0f, 0f, Dungeon2DConfig.CameraDepth);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Dungeon2DConfig.CameraBackgroundColor;
        }

        private void EnsureUI()
        {
            if (ui != null)
            {
                return;
            }

            GameObject uiObject = new GameObject("Dungeon2D_UI");
            uiObject.transform.SetParent(transform, false);
            ui = uiObject.AddComponent<Dungeon2DUI>();
            ui.Build(this, Art);
        }

        private void UpdateCameraFollow()
        {
            if (mainCamera == null || Player == null || State == DungeonGameState.MainMenu)
            {
                return;
            }

            Vector3 target = Player.transform.position + Vector3.forward * Dungeon2DConfig.CameraDepth;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target, Dungeon2DConfig.CameraFollowSpeed * Time.unscaledDeltaTime);
        }
    }
}
