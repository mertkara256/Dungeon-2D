using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dungeon2D
{
    public sealed class Dungeon2DGame : MonoBehaviour
    {
        public static Dungeon2DGame Instance { get; private set; }

        private const int TargetRoomCount = 10;
        private const float DefaultRoomWidth = 18f;
        private const float DefaultRoomHeight = 13f;
        private const float BossRoomWidth = 24f;
        private const float BossRoomHeight = 18f;
        private const float CorridorLength = 5f;
        private const float DoorWidth = 4.1f;
        private const float TileSize = 2f;
        private const string EditModePreviewName = "Dungeon2D_EditModePreview";

        private readonly List<DungeonRoom2D> rooms = new List<DungeonRoom2D>();
        private readonly List<DungeonEnemy2D> enemies = new List<DungeonEnemy2D>();
        private readonly Collider2D[] interactionHits = new Collider2D[24];

        private Transform worldRoot;
        private Camera mainCamera;
        private Dungeon2DUI ui;
        private Dungeon2DAudio audioSystem;
        private DungeonBoss2D activeBoss;
        private DungeonRoom2D currentRoom;
        private IDungeonInteractable2D currentInteractable;
        private List<PerkChoice2D> activeDraftChoices;
        private Action<PerkChoice2D> activeDraftCallback;
        private int score;

        public DungeonGameState State { get; private set; } = DungeonGameState.MainMenu;
        public Dungeon2DArtLibrary Art { get; private set; }
        public DungeonPlayer2D Player { get; private set; }
        public PlayerStats2D PlayerStats { get; private set; }
        public PlayerInventory2D Inventory { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Art = new Dungeon2DArtLibrary();
            PlayerStats = new PlayerStats2D();
            Inventory = new PlayerInventory2D();
            audioSystem = gameObject.AddComponent<Dungeon2DAudio>();
            DestroyEditModePreview();
            EnsureCamera();
            EnsureUI();
        }

        private void Start()
        {
            SetState(DungeonGameState.MainMenu);
            BuildPreviewWorld();
            ui.ShowMainMenu();
            Notify("Echo Dungeon 2D ready");
        }

        private void Update()
        {
            HandleGlobalInput();
            UpdateCameraFollow();

            if (State == DungeonGameState.Playing)
            {
                UpdateRoomActivation();
                UpdateInteractionPrompt();
            }

            if (State == DungeonGameState.RewardSelection)
            {
                UpdateDraftHotkeys();
            }
        }

        public void StartRun()
        {
            ClearRun();
            PlayerStats = new PlayerStats2D();
            Inventory = new PlayerInventory2D();
            score = 0;
            activeDraftChoices = null;
            activeDraftCallback = null;
            activeBoss = null;

            BuildDungeon();
            SpawnOrResetPlayer();
            MovePlayerToRoom(0);
            SetState(DungeonGameState.Playing);
            ui.HideMainMenu();
            ui.HidePerkDraft();
            RefreshHud();
            Notify("Room 1: clear all enemies");
            ActivateRoom(rooms[0]);
        }

        public void RestartRun()
        {
            Time.timeScale = 1f;
            StartRun();
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            EnsureCamera();
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        public void SpawnProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            float damage,
            float lifeTime,
            bool playerOwned,
            GameObject owner,
            float scale,
            bool bossCrystal = false)
        {
            GameObject projectileObject = new GameObject(playerOwned ? "PlayerProjectile" : "EnemyProjectile");
            projectileObject.transform.SetParent(worldRoot, true);
            DungeonProjectile2D projectile = projectileObject.AddComponent<DungeonProjectile2D>();
            Sprite sprite = bossCrystal
                ? Art.GetCharacterSprite(CharacterSpriteId.BossCrystal)
                : Art.GetProjectileSprite();
            Color color = playerOwned
                ? new Color(0.45f, 0.9f, 1f, 1f)
                : bossCrystal ? new Color(0.9f, 0.38f, 1f, 1f) : new Color(1f, 0.72f, 0.25f, 1f);
            projectile.Launch(this, sprite, position, direction, speed, damage, lifeTime, playerOwned, owner, scale, color);

            if (playerOwned)
            {
                audioSystem.PlayShoot();
            }
        }

        public void SpawnEnemySpreadProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            float damage,
            float lifeTime,
            GameObject owner,
            float spreadAngle,
            bool bossCrystal = false)
        {
            SpawnProjectile(position, direction, speed, damage, lifeTime, false, owner, bossCrystal ? 0.58f : 0.42f, bossCrystal);
            SpawnProjectile(position, Rotate(direction, -spreadAngle), speed, damage, lifeTime, false, owner, bossCrystal ? 0.58f : 0.42f, bossCrystal);
            SpawnProjectile(position, Rotate(direction, spreadAngle), speed, damage, lifeTime, false, owner, bossCrystal ? 0.58f : 0.42f, bossCrystal);
        }

        public void PerformPlayerMelee(Vector2 origin, Vector2 direction, float damage)
        {
            Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Vector2 center = origin + safeDirection * 1.35f;
            SpawnSlashEffect(center, safeDirection);

            Collider2D[] hits = Physics2D.OverlapCircleAll(center, 1.9f);
            HashSet<IDamageable2D> damaged = new HashSet<IDamageable2D>();
            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable2D damageable = hits[i].GetComponentInParent<IDamageable2D>();
                if (damageable == null || ReferenceEquals(damageable, Player) || damageable.IsDead || damaged.Contains(damageable))
                {
                    continue;
                }

                damaged.Add(damageable);
                damageable.TakeDamage(damage, Player.gameObject);
            }

            audioSystem.PlayHit();
        }

        public DungeonEnemy2D SpawnEnemy(DungeonRoom2D room, EnemyKind kind, Vector2 position)
        {
            GameObject enemyObject = new GameObject("Enemy_" + kind);
            enemyObject.transform.SetParent(room.Root.transform, true);
            enemyObject.transform.position = position;
            DungeonEnemy2D enemy = enemyObject.AddComponent<DungeonEnemy2D>();
            enemy.Configure(this, room, kind, room.Index + 1);
            enemy.Died += HandleEnemyDied;
            enemies.Add(enemy);
            return enemy;
        }

        public GameObject SpawnTelegraph(Vector2 position, float radius, Color color, float duration)
        {
            GameObject telegraph = new GameObject("Telegraph");
            telegraph.transform.SetParent(worldRoot, true);
            telegraph.transform.position = position;
            telegraph.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius);

            SpriteRenderer renderer = telegraph.AddComponent<SpriteRenderer>();
            renderer.sprite = Art.GetEnvironmentSprite(EnvironmentSpriteId.HazardRune);
            renderer.color = color;
            renderer.sortingOrder = 11;
            Destroy(telegraph, Mathf.Max(0.05f, duration));
            return telegraph;
        }

        public void SpawnHazard(Vector2 position, float radius, float damage, float tickInterval, float duration)
        {
            GameObject hazard = new GameObject("BossHazard");
            hazard.transform.SetParent(worldRoot, true);
            hazard.transform.position = position;
            hazard.transform.localScale = Vector3.one * Mathf.Max(0.2f, radius);

            SpriteRenderer renderer = hazard.AddComponent<SpriteRenderer>();
            renderer.sprite = Art.GetEnvironmentSprite(EnvironmentSpriteId.HazardRune);
            renderer.color = new Color(0.72f, 0.04f, 0.06f, 0.78f);
            renderer.sortingOrder = 10;

            DungeonHazard2D zone = hazard.AddComponent<DungeonHazard2D>();
            zone.Configure(this, damage, tickInterval, duration);
        }

        public void OpenPerkDraft(string title, string subtitle, int roomIndex, bool premium, Action<PerkChoice2D> onSelected)
        {
            activeDraftChoices = UpgradeCatalog2D.BuildDraft(roomIndex, premium, 3);
            if (activeDraftChoices.Count == 0)
            {
                Notify("No perk choices are available");
                return;
            }

            activeDraftCallback = onSelected;
            SetState(DungeonGameState.RewardSelection);
            ui.ShowPerkDraft(title, subtitle, activeDraftChoices, SelectPerkChoice);
        }

        public void SelectPerkChoice(int index)
        {
            if (activeDraftChoices == null || index < 0 || index >= activeDraftChoices.Count)
            {
                return;
            }

            PerkChoice2D choice = activeDraftChoices[index];
            activeDraftChoices = null;
            ui.HidePerkDraft();
            SetState(DungeonGameState.Playing);
            activeDraftCallback?.Invoke(choice);
            activeDraftCallback = null;
            RefreshHud();
        }

        public void AddScore(int value)
        {
            if (value <= 0)
            {
                return;
            }

            score += value;
            RefreshHud();
        }

        public void RefreshHud()
        {
            ui.RefreshHud(
                Player,
                PlayerStats,
                Inventory,
                score,
                currentRoom != null ? currentRoom.Index + 1 : 0,
                rooms.Count,
                activeBoss);
        }

        public void Notify(string message)
        {
            ui.Notify(message);
        }

        public void ClampPlayerHealthAfterStatChange()
        {
            if (Player != null)
            {
                Player.ClampHealthToStats();
            }
        }

        public void HandlePlayerDied()
        {
            SetState(DungeonGameState.GameOver);
            Notify("Game Over - press R to restart");
        }

        public void PlayDashFeedback(Vector2 position, Vector2 direction)
        {
            audioSystem.PlayDash();
            SpawnDashEnergyStreak(position, direction);
            SpawnDashRuneMotes(position, direction);
        }

        public void SpawnPlayerDashTrail(Vector2 position, Vector2 direction, Sprite sprite, Vector3 scale, bool flipX)
        {
            if (worldRoot == null || Art == null)
            {
                return;
            }

            Sprite trailSprite = sprite != null ? sprite : Art.GetCharacterSprite(CharacterSpriteId.Hero);
            Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            GameObject afterImage = CreateSpriteObject("DashTrailAfterImage", trailSprite, worldRoot, position - safeDirection * 0.08f, 19);
            afterImage.transform.localScale = scale;

            SpriteRenderer renderer = afterImage.GetComponent<SpriteRenderer>();
            renderer.flipX = flipX;
            renderer.color = new Color(0.44f, 0.96f, 1f, 0.72f);

            Dungeon2DDashTrailEffect trail = afterImage.AddComponent<Dungeon2DDashTrailEffect>();
            trail.Configure(safeDirection, renderer.color, 0.34f, 1.2f, 0.46f);
        }

        public void PlayHitFeedback()
        {
            audioSystem.PlayHit();
        }

        public void PlayPickupFeedback()
        {
            audioSystem.PlayPickup();
        }

        public void PlayBossFeedback()
        {
            audioSystem.PlayBoss();
        }

        public void PlayRewardFeedback()
        {
            audioSystem.PlayReward();
        }

        private void SetState(DungeonGameState state)
        {
            State = state;
            Time.timeScale = state == DungeonGameState.Paused || state == DungeonGameState.RewardSelection ? 0f : 1f;
            ui.SetState(state);
        }

        private void HandleGlobalInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (State == DungeonGameState.MainMenu && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            {
                StartRun();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame && State != DungeonGameState.MainMenu && State != DungeonGameState.GameOver && State != DungeonGameState.Win && State != DungeonGameState.RewardSelection)
            {
                SetState(State == DungeonGameState.Paused ? DungeonGameState.Playing : DungeonGameState.Paused);
                Notify(State == DungeonGameState.Paused ? "Paused" : "Resumed");
            }

            if ((State == DungeonGameState.GameOver || State == DungeonGameState.Win) && keyboard.rKey.wasPressedThisFrame)
            {
                RestartRun();
            }
        }

        private void UpdateDraftHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            {
                SelectPerkChoice(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            {
                SelectPerkChoice(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            {
                SelectPerkChoice(2);
            }
        }

        private void UpdateInteractionPrompt()
        {
            if (Player == null)
            {
                ui.SetInteractionPrompt(string.Empty);
                return;
            }

            currentInteractable = FindNearestInteractable();
            ui.SetInteractionPrompt(currentInteractable != null ? currentInteractable.InteractionLabel : string.Empty);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame && currentInteractable != null && currentInteractable.CanInteract(Player))
            {
                currentInteractable.Interact(Player);
            }
        }

        private IDungeonInteractable2D FindNearestInteractable()
        {
            ContactFilter2D filter = ContactFilter2D.noFilter;
            int count = Physics2D.OverlapCircle(Player.transform.position, 1.35f, filter, interactionHits);
            IDungeonInteractable2D best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D hit = interactionHits[i];
                if (hit == null)
                {
                    continue;
                }

                IDungeonInteractable2D interactable = hit.GetComponentInParent<IDungeonInteractable2D>();
                if (interactable == null || !interactable.CanInteract(Player))
                {
                    continue;
                }

                float distance = Vector2.Distance(Player.transform.position, hit.transform.position);
                if (distance < bestDistance)
                {
                    best = interactable;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void UpdateRoomActivation()
        {
            if (Player == null)
            {
                return;
            }

            Vector2 position = Player.transform.position;
            for (int i = 0; i < rooms.Count; i++)
            {
                DungeonRoom2D room = rooms[i];
                if (!room.Bounds.Contains(position))
                {
                    continue;
                }

                if (currentRoom != room)
                {
                    currentRoom = room;
                    RefreshHud();
                }

                if (!room.Activated)
                {
                    ActivateRoom(room);
                }

                return;
            }
        }

        private void ActivateRoom(DungeonRoom2D room)
        {
            room.Activated = true;
            currentRoom = room;
            RefreshHud();

            if (room.Kind == RoomKind.Combat || room.Kind == RoomKind.Boss)
            {
                room.EntranceDoor?.SetOpen(false);
                room.ExitDoor?.SetOpen(false);
                SpawnRoomWave(room);
                Notify("Room " + (room.Index + 1) + ": clear all enemies");
                return;
            }

            room.EntranceDoor?.SetOpen(false);
            room.ExitDoor?.SetOpen(false);
            if (room.Kind == RoomKind.Treasure)
            {
                SpawnTreasureRewards(room);
                Notify("Treasure room - free loot and a locked cache");
            }
            else
            {
                SpawnUpgradeStation(room);
                Notify("Upgrade room - draft a perk or unlock the reserve cache");
            }

            ClearRoom(room);
        }

        private void SpawnRoomWave(DungeonRoom2D room)
        {
            if (room.Kind == RoomKind.Boss)
            {
                SpawnEnemy(room, EnemyKind.Tank, PickSpawnPosition(room, 1));
                SpawnEnemy(room, EnemyKind.EliteRanged, PickSpawnPosition(room, 2));
                SpawnEnemy(room, EnemyKind.Ranged, PickSpawnPosition(room, 3));
                return;
            }

            int minCount = Mathf.Clamp(2 + room.Index / 2, 2, 6);
            int maxCount = Mathf.Clamp(3 + room.Index / 2, 3, 8);
            int spawnCount = UnityEngine.Random.Range(minCount, maxCount + 1);
            for (int i = 0; i < spawnCount; i++)
            {
                SpawnEnemy(room, ChooseEnemyKind(room.Index), PickSpawnPosition(room, i));
            }
        }

        private EnemyKind ChooseEnemyKind(int roomIndex)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            int eliteRangedWeight = roomIndex >= 2 ? Mathf.Clamp(5 + roomIndex * 2, 5, 18) : 0;
            int rangedWeight = Mathf.Clamp(20 + roomIndex * 3, 20, 45);
            int tankWeight = Mathf.Clamp(8 + roomIndex * 2, 8, 28);

            if (roll < eliteRangedWeight)
            {
                return EnemyKind.EliteRanged;
            }

            if (roll < eliteRangedWeight + tankWeight)
            {
                return EnemyKind.Tank;
            }

            if (roll < eliteRangedWeight + tankWeight + rangedWeight)
            {
                return EnemyKind.Ranged;
            }

            return EnemyKind.Melee;
        }

        private void HandleEnemyDied(DungeonEnemy2D enemy)
        {
            if (enemy == null)
            {
                return;
            }

            enemy.Died -= HandleEnemyDied;
            if (enemy.Room != null)
            {
                TrySpawnBossPrepDrop(enemy.Room, enemy.transform.position);
                CheckRoomProgress(enemy.Room);
            }
        }

        private void CheckRoomProgress(DungeonRoom2D room)
        {
            if (room.Cleared)
            {
                return;
            }

            if (HasLivingEnemies(room))
            {
                return;
            }

            if (room.Kind == RoomKind.Boss && !room.BossSpawned)
            {
                SpawnBossEncounter(room);
                return;
            }

            if (room.Kind != RoomKind.Boss)
            {
                ClearRoom(room);
            }
        }

        private bool HasLivingEnemies(DungeonRoom2D room)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                DungeonEnemy2D enemy = enemies[i];
                if (enemy == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                if (enemy.Room == room && !enemy.IsDead)
                {
                    return true;
                }
            }

            return false;
        }

        private void SpawnBossEncounter(DungeonRoom2D room)
        {
            room.BossSpawned = true;
            GameObject bossObject = new GameObject("Corrupted Guardian");
            bossObject.transform.SetParent(room.Root.transform, true);
            bossObject.transform.position = room.Center + Vector2.up * 1.5f;
            activeBoss = bossObject.AddComponent<DungeonBoss2D>();
            activeBoss.Configure(this, room, room.Index + 1);
            activeBoss.BossDefeated += HandleBossDefeated;
        }

        private void HandleBossDefeated(DungeonBoss2D boss)
        {
            if (boss != null)
            {
                boss.BossDefeated -= HandleBossDefeated;
            }

            ClearRoomMinions(currentRoom);
            ClearRoom(currentRoom);
            SetState(DungeonGameState.Win);
            Notify("Victory - press R to restart");
        }

        private void ClearRoomMinions(DungeonRoom2D room)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                DungeonEnemy2D enemy = enemies[i];
                if (enemy != null && enemy.Room == room && !enemy.IsDead)
                {
                    Destroy(enemy.gameObject);
                }
            }
        }

        private void ClearRoom(DungeonRoom2D room)
        {
            if (room == null || room.Cleared)
            {
                return;
            }

            room.Cleared = true;
            room.EntranceDoor?.SetOpen(true);
            room.ExitDoor?.SetOpen(true);

            if (ShouldSpawnKeyReward(room))
            {
                SpawnPickup(room.Center + Vector2.right * 1.4f, PickupKind.Key, 1, room.Root.transform);
                Notify("A dungeon key dropped");
            }

            RefreshHud();
        }

        private bool ShouldSpawnKeyReward(DungeonRoom2D room)
        {
            return room.Kind == RoomKind.Combat && room.Index > 0 && room.Index % 3 == 2;
        }

        private void TrySpawnBossPrepDrop(DungeonRoom2D room, Vector2 position)
        {
            if (room.Kind != RoomKind.Boss || room.BossSpawned || room.BossPrepDrops >= 2)
            {
                return;
            }

            PickupKind pickupKind = room.BossPrepDrops == 0 ? PickupKind.HealthPotion : PickupKind.SpeedBoost;
            SpawnPickup(position, pickupKind, 1, room.Root.transform);
            Notify(room.BossPrepDrops == 0 ? "Health potion dropped for the boss fight" : "Speed boost dropped for the boss fight");
            room.BossPrepDrops++;
        }

        private void SpawnTreasureRewards(DungeonRoom2D room)
        {
            SpawnPickup(room.Center + new Vector2(-2.7f, 0.6f), PickupKind.HealthPotion, 2, room.Root.transform);
            SpawnPickup(room.Center + new Vector2(-0.2f, 0.6f), PickupKind.SpeedBoost, 1, room.Root.transform);
            SpawnLockedChest(room, new Vector2(3.2f, 1.2f), "Treasure Vault", "Spend 1 key to claim a premium treasure perk");
        }

        private void SpawnUpgradeStation(DungeonRoom2D room)
        {
            GameObject station = CreateSpriteObject("UpgradeStation", Art.GetEnvironmentSprite(EnvironmentSpriteId.Altar), room.Root.transform, room.Center, 12);
            station.transform.localScale = Vector3.one * 1.15f;
            station.AddComponent<DungeonUpgradeStation2D>().Configure(this, room.Index);
            SpawnLockedChest(room, new Vector2(3.6f, 0f), "Reserve Cache", "Spend 1 key to add a premium perk");
        }

        private void SpawnLockedChest(DungeonRoom2D room, Vector2 localOffset, string title, string subtitle)
        {
            GameObject chest = CreateSpriteObject("LockedRewardChest", Art.GetEnvironmentSprite(EnvironmentSpriteId.Chest), room.Root.transform, room.Center + localOffset, 12);
            chest.transform.localScale = Vector3.one * 0.82f;
            chest.AddComponent<DungeonLockedChest2D>().Configure(this, room.Index, title, subtitle);
        }

        private void SpawnPickup(Vector2 position, PickupKind kind, int amount, Transform parent)
        {
            EnvironmentSpriteId spriteId;
            switch (kind)
            {
                case PickupKind.SpeedBoost:
                    spriteId = EnvironmentSpriteId.SpeedPotion;
                    break;
                case PickupKind.Key:
                    spriteId = EnvironmentSpriteId.Key;
                    break;
                default:
                    spriteId = EnvironmentSpriteId.HealthPotion;
                    break;
            }

            GameObject pickup = CreateSpriteObject("Pickup_" + kind, Art.GetEnvironmentSprite(spriteId), parent, position, 13);
            pickup.transform.localScale = Vector3.one * (kind == PickupKind.Key ? 0.55f : 0.48f);
            pickup.AddComponent<DungeonPickup2D>().Configure(this, kind, amount);
        }

        private Vector2 PickSpawnPosition(DungeonRoom2D room, int index)
        {
            Rect bounds = room.Bounds;
            Vector2[] offsets =
            {
                new Vector2(-0.28f, 0.18f),
                new Vector2(0.28f, 0.18f),
                new Vector2(-0.28f, -0.12f),
                new Vector2(0.28f, -0.12f),
                new Vector2(0f, 0.28f),
                new Vector2(0f, -0.28f),
                new Vector2(-0.36f, 0f),
                new Vector2(0.36f, 0f)
            };

            Vector2 offset = offsets[index % offsets.Length];
            return new Vector2(
                Mathf.Lerp(bounds.xMin + 2.5f, bounds.xMax - 2.5f, offset.x + 0.5f),
                Mathf.Lerp(bounds.yMin + 2.5f, bounds.yMax - 2.5f, offset.y + 0.5f));
        }

        private void BuildDungeon()
        {
            worldRoot = new GameObject("Dungeon2D_World").transform;
            rooms.Clear();

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
                SpawnPoint = center + new Vector2(0f, -size.y * 0.5f + 2.25f)
            };

            BuildFloorTiles(room);
            BuildWalls(room);
            return room;
        }

        private void BuildFloorTiles(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            int columns = Mathf.CeilToInt(room.Size.x / TileSize);
            int rows = Mathf.CeilToInt(room.Size.y / TileSize);
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    Vector2 position = new Vector2(
                        bounds.xMin + TileSize * 0.5f + x * TileSize,
                        bounds.yMin + TileSize * 0.5f + y * TileSize);
                    EnvironmentSpriteId spriteId = EnvironmentSpriteId.Floor;
                    int noise = Mathf.Abs((x * 31 + y * 17 + room.Index * 13) % 7);
                    if (noise == 0)
                    {
                        spriteId = EnvironmentSpriteId.CrackedFloor;
                    }
                    else if (noise == 3)
                    {
                        spriteId = EnvironmentSpriteId.MossFloor;
                    }

                    CreateSpriteObject("FloorTile", Art.GetEnvironmentSprite(spriteId), room.Root.transform, position, 0);
                }
            }
        }

        private void BuildWalls(DungeonRoom2D room)
        {
            Rect bounds = room.Bounds;
            float halfDoor = DoorWidth * 0.5f;
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMin, bounds.yMax), false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMax, bounds.yMin), new Vector2(bounds.xMax, bounds.yMax), false);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMin), new Vector2(bounds.xMax, bounds.yMin), true);
            BuildWallVisualLine(room.Root.transform, new Vector2(bounds.xMin, bounds.yMax), new Vector2(bounds.xMax, bounds.yMax), true);

            AddWallCollider(room.Root.transform, "LeftWallCollider", new Vector2(bounds.xMin - 0.42f, room.Center.y), new Vector2(0.84f, room.Size.y + 0.8f));
            AddWallCollider(room.Root.transform, "RightWallCollider", new Vector2(bounds.xMax + 0.42f, room.Center.y), new Vector2(0.84f, room.Size.y + 0.8f));

            if (room.Index > 0)
            {
                AddWallCollider(room.Root.transform, "BottomWallLeft", new Vector2((bounds.xMin - halfDoor) * 0.5f, bounds.yMin - 0.42f), new Vector2(room.Size.x * 0.5f - halfDoor, 0.84f));
                AddWallCollider(room.Root.transform, "BottomWallRight", new Vector2((bounds.xMax + halfDoor) * 0.5f, bounds.yMin - 0.42f), new Vector2(room.Size.x * 0.5f - halfDoor, 0.84f));
                room.EntranceDoor = CreateDoor(room.Root.transform, new Vector2(0f, bounds.yMin - 0.22f), false);
            }
            else
            {
                AddWallCollider(room.Root.transform, "BottomWallCollider", new Vector2(room.Center.x, bounds.yMin - 0.42f), new Vector2(room.Size.x + 0.8f, 0.84f));
            }

            if (room.Index < TargetRoomCount - 1)
            {
                AddWallCollider(room.Root.transform, "TopWallLeft", new Vector2((bounds.xMin - halfDoor) * 0.5f, bounds.yMax + 0.42f), new Vector2(room.Size.x * 0.5f - halfDoor, 0.84f));
                AddWallCollider(room.Root.transform, "TopWallRight", new Vector2((bounds.xMax + halfDoor) * 0.5f, bounds.yMax + 0.42f), new Vector2(room.Size.x * 0.5f - halfDoor, 0.84f));
                room.ExitDoor = CreateDoor(room.Root.transform, new Vector2(0f, bounds.yMax + 0.22f), true);
            }
            else
            {
                AddWallCollider(room.Root.transform, "TopWallCollider", new Vector2(room.Center.x, bounds.yMax + 0.42f), new Vector2(room.Size.x + 0.8f, 0.84f));
            }
        }

        private void BuildWallVisualLine(Transform parent, Vector2 start, Vector2 end, bool horizontal)
        {
            float length = horizontal ? Mathf.Abs(end.x - start.x) : Mathf.Abs(end.y - start.y);
            int count = Mathf.Max(1, Mathf.CeilToInt(length / TileSize));
            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0f : i / (float)count;
                Vector2 position = Vector2.Lerp(start, end, t);
                GameObject wall = CreateSpriteObject("WallTile", Art.GetEnvironmentSprite(EnvironmentSpriteId.Wall), parent, position, 6);
                wall.transform.localScale = Vector3.one * 0.92f;
            }
        }

        private void BuildCorridor(int index, float fromY, float toY)
        {
            GameObject corridor = new GameObject("Corridor_" + (index + 1));
            corridor.transform.SetParent(worldRoot, false);
            float centerY = (fromY + toY) * 0.5f;
            float height = Mathf.Abs(toY - fromY);
            int rows = Mathf.CeilToInt(height / TileSize);

            for (int y = 0; y < rows; y++)
            {
                Vector2 position = new Vector2(0f, Mathf.Min(fromY, toY) + TileSize * 0.5f + y * TileSize);
                CreateSpriteObject("CorridorFloor", Art.GetEnvironmentSprite(EnvironmentSpriteId.CrackedFloor), corridor.transform, position, 0);
            }

            AddWallCollider(corridor.transform, "CorridorLeftWall", new Vector2(-DoorWidth * 0.5f - 0.42f, centerY), new Vector2(0.84f, height));
            AddWallCollider(corridor.transform, "CorridorRightWall", new Vector2(DoorWidth * 0.5f + 0.42f, centerY), new Vector2(0.84f, height));

            for (int y = 0; y <= rows; y++)
            {
                float rowY = Mathf.Min(fromY, toY) + y * TileSize;
                CreateSpriteObject("CorridorWallLeft", Art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(-DoorWidth * 0.5f - 0.45f, rowY), 7);
                CreateSpriteObject("CorridorWallRight", Art.GetEnvironmentSprite(EnvironmentSpriteId.IronBars), corridor.transform, new Vector2(DoorWidth * 0.5f + 0.45f, rowY), 7);
            }
        }

        private DungeonDoor2D CreateDoor(Transform parent, Vector2 position, bool exit)
        {
            GameObject doorObject = CreateSpriteObject(exit ? "ExitDoor" : "EntranceDoor", Art.GetEnvironmentSprite(EnvironmentSpriteId.Door), parent, position, 8);
            doorObject.transform.localScale = new Vector3(1.55f, 0.9f, 1f);
            BoxCollider2D collider = doorObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(DoorWidth, 0.8f);
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

        private void SpawnSlashEffect(Vector2 center, Vector2 direction)
        {
            GameObject slash = CreateSpriteObject("SwordSlash", Art.GetSlashSprite(), worldRoot, center, 30);
            slash.transform.localScale = Vector3.one * 1.25f;
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 35f);
            SpriteRenderer renderer = slash.GetComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 0.92f, 0.72f, 0.9f);
            slash.AddComponent<Dungeon2DSlashEffect>();
        }

        private void BuildPreviewWorld()
        {
            if (worldRoot != null)
            {
                return;
            }

            BuildDungeon();
            if (rooms.Count > 0 && mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(rooms[0].Center.x, rooms[0].Center.y, -10f);
            }
        }

        private static void DestroyEditModePreview()
        {
            GameObject preview = GameObject.Find(EditModePreviewName);
            if (preview != null)
            {
                Destroy(preview);
            }
        }

        private void SpawnDashRuneMotes(Vector2 position, Vector2 direction)
        {
            if (worldRoot == null || Art == null)
            {
                return;
            }

            Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Vector2 side = new Vector2(-safeDirection.y, safeDirection.x);

            for (int i = 0; i < 5; i++)
            {
                float sideOffset = (i - 2) * 0.17f;
                Vector2 motePosition = position - safeDirection * (0.26f + i * 0.17f) + side * sideOffset;
                GameObject mote = CreateSpriteObject("DashRuneMote", Art.GetEnvironmentSprite(EnvironmentSpriteId.HazardRune), worldRoot, motePosition, 18);
                mote.transform.localScale = Vector3.one * (0.22f + i * 0.035f);
                mote.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg + i * 24f);

                SpriteRenderer renderer = mote.GetComponent<SpriteRenderer>();
                renderer.color = new Color(0.58f, 0.98f, 1f, 0.62f);

                Dungeon2DDashTrailEffect trail = mote.AddComponent<Dungeon2DDashTrailEffect>();
                trail.Configure(safeDirection, renderer.color, 0.24f + i * 0.025f, 1.06f, 0.26f);
            }
        }

        private void SpawnDashEnergyStreak(Vector2 position, Vector2 direction)
        {
            if (worldRoot == null || Art == null)
            {
                return;
            }

            Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            float angle = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg - 90f;
            Vector2 center = position - safeDirection * 0.55f;

            GameObject glow = CreateSpriteObject("DashEnergyGlow", Art.GetProjectileSprite(), worldRoot, center, 18);
            glow.transform.localScale = new Vector3(1.05f, 3.1f, 1f);
            glow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            SpriteRenderer glowRenderer = glow.GetComponent<SpriteRenderer>();
            glowRenderer.color = new Color(0.28f, 0.84f, 1f, 0.42f);
            glow.AddComponent<Dungeon2DDashTrailEffect>()
                .Configure(safeDirection, glowRenderer.color, 0.2f, 1f, 0.2f);

            GameObject core = CreateSpriteObject("DashEnergyCore", Art.GetProjectileSprite(), worldRoot, center + safeDirection * 0.1f, 19);
            core.transform.localScale = new Vector3(0.46f, 2.45f, 1f);
            core.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            SpriteRenderer coreRenderer = core.GetComponent<SpriteRenderer>();
            coreRenderer.color = new Color(0.82f, 1f, 1f, 0.78f);
            core.AddComponent<Dungeon2DDashTrailEffect>()
                .Configure(safeDirection, coreRenderer.color, 0.16f, 1f, 0.12f);
        }

        private void SpawnOrResetPlayer()
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(worldRoot, true);
            Player = playerObject.AddComponent<DungeonPlayer2D>();
            Player.Configure(this, Art);
        }

        private void MovePlayerToRoom(int index)
        {
            if (Player == null || rooms.Count == 0)
            {
                return;
            }

            int safeIndex = Mathf.Clamp(index, 0, rooms.Count - 1);
            Player.transform.position = rooms[safeIndex].SpawnPoint;
            currentRoom = rooms[safeIndex];
            RefreshHud();
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
            mainCamera.orthographicSize = 8f;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.026f, 0.03f, 0.038f, 1f);
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

            Vector3 target = Player.transform.position + Vector3.back * 10f;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target, 8f * Time.unscaledDeltaTime);
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

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }
    }
}
