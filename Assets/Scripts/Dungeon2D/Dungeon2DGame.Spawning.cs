using UnityEngine;

namespace Dungeon2D
{
    public sealed partial class Dungeon2DGame
    {
        private static readonly Vector2[] EnemySpawnOffsets =
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
            if (worldRoot == null || Art == null)
            {
                return;
            }

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
            float scale = bossCrystal ? 0.58f : 0.42f;
            SpawnProjectile(position, direction, speed, damage, lifeTime, false, owner, scale, bossCrystal);
            SpawnProjectile(position, Rotate(direction, -spreadAngle), speed, damage, lifeTime, false, owner, scale, bossCrystal);
            SpawnProjectile(position, Rotate(direction, spreadAngle), speed, damage, lifeTime, false, owner, scale, bossCrystal);
        }

        public void PerformPlayerMelee(Vector2 origin, Vector2 direction, float damage)
        {
            Vector2 safeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            Vector2 center = origin + safeDirection * Dungeon2DConfig.PlayerMeleeOffset;
            SpawnSlashEffect(center, safeDirection);

            meleeDamaged.Clear();
            ContactFilter2D filter = ContactFilter2D.noFilter;
            int hitCount = Physics2D.OverlapCircle(center, Dungeon2DConfig.PlayerMeleeRadius, filter, meleeHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = meleeHits[i];
                if (hit == null)
                {
                    continue;
                }

                IDamageable2D damageable = hit.GetComponentInParent<IDamageable2D>();
                if (damageable == null || ReferenceEquals(damageable, Player) || damageable.IsDead || meleeDamaged.Contains(damageable))
                {
                    continue;
                }

                meleeDamaged.Add(damageable);
                damageable.TakeDamage(damage, Player != null ? Player.gameObject : null);
            }

            meleeDamaged.Clear();
            audioSystem.PlayHit();
        }

        public DungeonEnemy2D SpawnEnemy(DungeonRoom2D room, EnemyKind kind, Vector2 position)
        {
            if (room == null || room.Root == null)
            {
                return null;
            }

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
            if (worldRoot == null || Art == null)
            {
                return null;
            }

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
            if (worldRoot == null || Art == null)
            {
                return;
            }

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

        private void SpawnBossEncounter(DungeonRoom2D room)
        {
            if (room == null || room.Root == null)
            {
                return;
            }

            room.BossSpawned = true;
            GameObject bossObject = new GameObject("Corrupted Guardian");
            bossObject.transform.SetParent(room.Root.transform, true);
            bossObject.transform.position = room.Center + Vector2.up * 1.5f;
            activeBoss = bossObject.AddComponent<DungeonBoss2D>();
            activeBoss.Configure(this, room, room.Index + 1);
            activeBoss.BossDefeated += HandleBossDefeated;
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
            Vector2 offset = EnemySpawnOffsets[index % EnemySpawnOffsets.Length];
            return new Vector2(
                Mathf.Lerp(bounds.xMin + Dungeon2DConfig.EnemySpawnInset, bounds.xMax - Dungeon2DConfig.EnemySpawnInset, offset.x + 0.5f),
                Mathf.Lerp(bounds.yMin + Dungeon2DConfig.EnemySpawnInset, bounds.yMax - Dungeon2DConfig.EnemySpawnInset, offset.y + 0.5f));
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

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
        }
    }
}
