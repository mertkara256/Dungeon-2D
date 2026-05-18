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

            BuildFloorTiles(room);
            BuildWalls(room);
            return room;
        }

        private void BuildFloorTiles(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            int columns = Mathf.CeilToInt(room.Size.x / Dungeon2DConfig.TileSize);
            int rows = Mathf.CeilToInt(room.Size.y / Dungeon2DConfig.TileSize);
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector2 position = new Vector2(
                        bounds.xMin + Dungeon2DConfig.TileSize * 0.5f + x * Dungeon2DConfig.TileSize,
                        bounds.yMin + Dungeon2DConfig.TileSize * 0.5f + y * Dungeon2DConfig.TileSize);
                    EnvironmentSpriteId spriteId = Dungeon2DConfig.GetFloorSpriteId(x, y, room.Index);
                    CreateSpriteObject("FloorTile", Art.GetEnvironmentSprite(spriteId), room.Root.transform, position, 0);
                }
            }
        }

        private void BuildWalls(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            float halfDoor = Dungeon2DConfig.DoorWidth * 0.5f;
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true);

            AddWallCollider(room.Root.transform, "LeftWallCollider", new Vector2(bounds.xMin - Dungeon2DConfig.RoomWallInset, room.Center.y), new Vector2(Dungeon2DConfig.RoomWallThickness, room.Size.y + Dungeon2DConfig.RoomWallPadding));
            AddWallCollider(room.Root.transform, "RightWallCollider", new Vector2(bounds.xMax + Dungeon2DConfig.RoomWallInset, room.Center.y), new Vector2(Dungeon2DConfig.RoomWallThickness, room.Size.y + Dungeon2DConfig.RoomWallPadding));

            if (room.Index > 0)
            {
                AddWallCollider(room.Root.transform, "BottomWallLeft", new Vector2((bounds.xMin - halfDoor) * 0.5f, bounds.yMin - Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
                AddWallCollider(room.Root.transform, "BottomWallRight", new Vector2((bounds.xMax + halfDoor) * 0.5f, bounds.yMin - Dungeon2DConfig.RoomWallInset), new Vector2(room.Size.x * 0.5f - halfDoor, Dungeon2DConfig.RoomWallThickness));
                room.EntranceDoor = CreateDoor(room.Root.transform, new Vector2(0f, bounds.yMin - Dungeon2DConfig.DoorVerticalOffset), false);
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

        private void BuildWallVisualLine(Transform parent, Vector2 start, Vector2 end, bool horizontal)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / Dungeon2DConfig.TileSize));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0f : i / (float)count;
                Vector2 position = Vector2.Lerp(start, end, t);
                GameObject wall = CreateSpriteObject("WallTile", Art.GetEnvironmentSprite(EnvironmentSpriteId.Wall), parent, position, 6);
                wall.transform.localScale = Vector3.one * Dungeon2DConfig.WallVisualScale;
            }
        }

        private void BuildCorridor(int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("Corridor_" + (index + 1));
            corridor.transform.SetParent(worldRoot, false);
            float centerY = (fromY + toY) * 0.5f;
            float height = Mathf.Abs(toY - fromY);
            int rows = Mathf.CeilToInt(height / Dungeon2DConfig.TileSize);

            for (int y = 0; y < rows; y++)
            {
                Vector2 position = new Vector2(0f, Mathf.Min(fromY, toY) + Dungeon2DConfig.TileSize * 0.5f + y * Dungeon2DConfig.TileSize);
                CreateSpriteObject("CorridorFloor", Art.GetEnvironmentSprite(EnvironmentSpriteId.CrackedFloor), corridor.transform, position, 0);
            }

            float halfDoor = Dungeon2DConfig.DoorWidth * 0.5f;
            AddWallCollider(corridor.transform, "CorridorLeftWall", new Vector2(-halfDoor - Dungeon2DConfig.RoomWallInset, centerY), new Vector2(Dungeon2DConfig.RoomWallThickness, height));
            AddWallCollider(corridor.transform, "CorridorRightWall", new Vector2(halfDoor + Dungeon2DConfig.RoomWallInset, centerY), new Vector2(Dungeon2DConfig.RoomWallThickness, height));

            for (int y = 0; y <= rows; y++)
            {
                float rowY = Mathf.Min(fromY, toY) + y * Dungeon2DConfig.TileSize;
                CreateSpriteObject("CorridorWallLeft", Art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(-halfDoor - Dungeon2DConfig.CorridorWallVisualInset, rowY), 7);
                CreateSpriteObject("CorridorWallRight", Art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(halfDoor + Dungeon2DConfig.CorridorWallVisualInset, rowY), 7);
            }
        }

        private DungeonDoor2D CreateDoor(Transform parent, Vector2 position, bool exit)
        {
            GameObject doorObject = CreateSpriteObject(exit ? "ExitDoor" : "EntranceDoor", Art.GetEnvironmentSprite(EnvironmentSpriteId.Door), parent, position, 8);
            doorObject.transform.localScale = new Vector3(Dungeon2DConfig.DoorVisualWidth, Dungeon2DConfig.DoorClosedVisualHeight, 1f);
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
