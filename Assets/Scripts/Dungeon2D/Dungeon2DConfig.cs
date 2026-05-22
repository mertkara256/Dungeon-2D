using UnityEngine;

namespace Dungeon2D
{
    public static class Dungeon2DConfig
    {
        public const string RuntimeRootName = "Dungeon2D Runtime";
        public const string WorldRootName = "Dungeon2D_World";
        public const string EditModePreviewName = "Dungeon2D_EditModePreview";

        public const int TargetRoomCount = 10;
        public const float DefaultRoomWidth = 18f;
        public const float DefaultRoomHeight = 13f;
        public const float BossRoomWidth = 24f;
        public const float BossRoomHeight = 18f;
        public const float CorridorLength = 5f;
        public const float DoorWidth = 4.1f;
        public const float TileSize = 2f;

        public const float PlayerSpawnBottomPadding = 2.25f;
        public const float RoomWallInset = 0.42f;
        public const float RoomWallThickness = 0.84f;
        public const float RoomWallPadding = 0.8f;
        public const float WallVisualScale = 0.92f;
        public const float WallVisualThickness = 1.72f;
        public const float WallCornerSize = WallVisualThickness;
        public const float WallContactShadowWidth = 0.55f;
        public const float CharacterVisualScale = 1.3f;
        public const float DoorVerticalOffset = 0.02f;
        public const float DoorColliderHeight = 0.8f;
        public const float DoorClosedVisualHeight = 0.9f;
        public const float DoorOpenVisualHeight = 0.72f;
        public const float DoorVisualWidth = 1.55f;
        public const float BridgeVisualWidth = 4.75f;
        public const float BridgeVisualOverlap = 1.25f;
        public const float BridgeWallVisualGapWidth = 5.15f;
        public const float BridgeDoorClosedVisualWidth = 4.75f;
        public const float BridgeDoorOpenVisualWidth = 4.95f;
        public const float CorridorWallVisualInset = 0.45f;

        public const int InteractionHitCapacity = 24;
        public const float InteractionRadius = 1.35f;
        public const int MeleeHitCapacity = 64;
        public const float PlayerMeleeOffset = 1.35f;
        public const float PlayerMeleeRadius = 1.9f;
        public const float EnemySpawnInset = 2.5f;

        public const float CameraOrthographicSize = 6.6f;
        public const float CameraDepth = -10f;
        public const float CameraFollowSpeed = 8f;

        public static readonly Color CameraBackgroundColor = new Color(0.026f, 0.03f, 0.038f, 1f);

        public static RoomKind DetermineRoomKind(int index)
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

        public static Vector2 GetRoomSize(RoomKind kind)
        {
            return kind == RoomKind.Boss
                ? new Vector2(BossRoomWidth, BossRoomHeight)
                : new Vector2(DefaultRoomWidth, DefaultRoomHeight);
        }

        public static EnvironmentSpriteId GetFloorSpriteId(int tileX, int tileY, int roomIndex)
        {
            int noise = Mathf.Abs((tileX * 31 + tileY * 17 + roomIndex * 13) % 7);
            if (noise == 0)
            {
                return EnvironmentSpriteId.CrackedFloor;
            }

            return noise == 3 ? EnvironmentSpriteId.MossFloor : EnvironmentSpriteId.Floor;
        }
    }
}
