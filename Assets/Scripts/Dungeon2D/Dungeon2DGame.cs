using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon2D
{
    public sealed partial class Dungeon2DGame : MonoBehaviour
    {
        public static Dungeon2DGame Instance { get; private set; }

        private readonly List<DungeonRoom2D> rooms = new List<DungeonRoom2D>();
        private readonly List<DungeonEnemy2D> enemies = new List<DungeonEnemy2D>();
        private readonly Collider2D[] interactionHits = new Collider2D[Dungeon2DConfig.InteractionHitCapacity];
        private readonly Collider2D[] meleeHits = new Collider2D[Dungeon2DConfig.MeleeHitCapacity];
        private readonly HashSet<IDamageable2D> meleeDamaged = new HashSet<IDamageable2D>();

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

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Time.timeScale = 1f;
            Instance = null;
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
            if (rooms.Count == 0)
            {
                SetState(DungeonGameState.MainMenu);
                Notify("Dungeon generation failed");
                return;
            }

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
            if (ui == null)
            {
                return;
            }

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
            if (ui == null)
            {
                return;
            }

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

        private void SetState(DungeonGameState state)
        {
            State = state;
            Time.timeScale = state == DungeonGameState.Paused || state == DungeonGameState.RewardSelection ? 0f : 1f;
            if (ui != null)
            {
                ui.SetState(state);
            }
        }

        private void HandleGlobalInput()
        {
            if (State == DungeonGameState.MainMenu && Dungeon2DInput.WasStartPressed())
            {
                StartRun();
                return;
            }

            if (Dungeon2DInput.WasPausePressed() && State != DungeonGameState.MainMenu && State != DungeonGameState.GameOver && State != DungeonGameState.Win && State != DungeonGameState.RewardSelection)
            {
                SetState(State == DungeonGameState.Paused ? DungeonGameState.Playing : DungeonGameState.Paused);
                Notify(State == DungeonGameState.Paused ? "Paused" : "Resumed");
            }

            if ((State == DungeonGameState.GameOver || State == DungeonGameState.Win) && Dungeon2DInput.WasRestartPressed())
            {
                RestartRun();
            }
        }

        private void UpdateDraftHotkeys()
        {
            int choiceIndex = Dungeon2DInput.ReadDraftChoiceIndex();
            if (choiceIndex >= 0)
            {
                SelectPerkChoice(choiceIndex);
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

            if (Dungeon2DInput.WasInteractPressed() && currentInteractable != null && currentInteractable.CanInteract(Player))
            {
                currentInteractable.Interact(Player);
            }
        }

        private IDungeonInteractable2D FindNearestInteractable()
        {
            ContactFilter2D filter = ContactFilter2D.noFilter;
            int count = Physics2D.OverlapCircle(Player.transform.position, Dungeon2DConfig.InteractionRadius, filter, interactionHits);
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

    }
}
