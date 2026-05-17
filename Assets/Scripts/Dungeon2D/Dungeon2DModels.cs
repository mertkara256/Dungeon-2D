using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon2D
{
    public enum DungeonGameState
    {
        MainMenu,
        Playing,
        Paused,
        RewardSelection,
        GameOver,
        Win
    }

    public enum RoomKind
    {
        Combat,
        Treasure,
        Upgrade,
        Boss
    }

    public enum EnemyKind
    {
        Melee,
        Ranged,
        Tank,
        EliteRanged,
        Minion
    }

    public enum PickupKind
    {
        HealthPotion,
        SpeedBoost,
        Key
    }

    public enum UpgradeType2D
    {
        MaxHealth,
        Damage,
        AttackRate,
        MoveSpeed,
        ProjectileSpeed,
        Armor
    }

    public enum PerkRarity2D
    {
        Common,
        Uncommon,
        Rare
    }

    public enum EnvironmentSpriteId
    {
        Floor,
        CrackedFloor,
        MossFloor,
        Wall,
        IronBars,
        Door,
        Archway,
        Chest,
        OpenChest,
        Altar,
        AltarSpent,
        HealthPotion,
        SpeedPotion,
        Key,
        Projectile,
        HazardRune,
        Slash,
        Panel
    }

    public enum CharacterSpriteId
    {
        Hero,
        MeleeEnemy,
        RangedEnemy,
        TankEnemy,
        EliteRangedEnemy,
        Minion,
        Boss,
        BossCrystal
    }

    public enum DungeonSpriteAnimation
    {
        Idle,
        Walk,
        PrimaryAttack,
        SecondaryAttack
    }

    public interface IDamageable2D
    {
        bool IsDead { get; }
        void TakeDamage(float amount, GameObject instigator);
    }

    public static class Dungeon2DPhysics
    {
        private static PhysicsMaterial2D material;

        public static PhysicsMaterial2D Material
        {
            get
            {
                if (material == null)
                {
                    material = new PhysicsMaterial2D("Dungeon2D_NoFriction")
                    {
                        friction = 0f,
                        bounciness = 0f
                    };
                }

                return material;
            }
        }
    }

    public sealed class DungeonRoom2D
    {
        public int Index;
        public RoomKind Kind;
        public GameObject Root;
        public Vector2 Center;
        public Vector2 Size;
        public Vector2 SpawnPoint;
        public DungeonDoor2D EntranceDoor;
        public DungeonDoor2D ExitDoor;
        public bool Activated;
        public bool Cleared;
        public bool BossSpawned;
        public int BossPrepDrops;

        public Rect Bounds
        {
            get
            {
                return new Rect(
                    Center.x - Size.x * 0.5f,
                    Center.y - Size.y * 0.5f,
                    Size.x,
                    Size.y);
            }
        }
    }

    [Serializable]
    public sealed class PlayerStats2D
    {
        public float MaxHealth = 100f;
        public float MoveSpeed = 6f;
        public float Damage = 18f;
        public float AttackRate = 2f;
        public float ProjectileSpeed = 9.5f;
        public float Armor = 0f;

        public void ApplyUpgrade(UpgradeType2D upgradeType, float amount)
        {
            switch (upgradeType)
            {
                case UpgradeType2D.MaxHealth:
                    MaxHealth = Mathf.Max(10f, MaxHealth + amount);
                    break;
                case UpgradeType2D.Damage:
                    Damage = Mathf.Max(1f, Damage + amount);
                    break;
                case UpgradeType2D.AttackRate:
                    AttackRate = Mathf.Clamp(AttackRate + amount, 0.3f, 15f);
                    break;
                case UpgradeType2D.MoveSpeed:
                    MoveSpeed = Mathf.Clamp(MoveSpeed + amount, 2f, 20f);
                    break;
                case UpgradeType2D.ProjectileSpeed:
                    ProjectileSpeed = Mathf.Clamp(ProjectileSpeed + amount, 5f, 60f);
                    break;
                case UpgradeType2D.Armor:
                    Armor = Mathf.Clamp(Armor + amount, 0f, 200f);
                    break;
            }
        }
    }

    public sealed class PlayerInventory2D
    {
        public int Potions;
        public int SpeedBoosts;
        public int Keys;

        public void Add(PickupKind kind, int amount)
        {
            int safeAmount = Mathf.Max(1, amount);
            switch (kind)
            {
                case PickupKind.HealthPotion:
                    Potions += safeAmount;
                    break;
                case PickupKind.SpeedBoost:
                    SpeedBoosts += safeAmount;
                    break;
                case PickupKind.Key:
                    Keys += safeAmount;
                    break;
            }
        }
    }

    public sealed class PerkChoice2D
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        public readonly UpgradeType2D UpgradeType;
        public readonly float Amount;
        public readonly PerkRarity2D Rarity;

        public PerkChoice2D(
            string id,
            string title,
            string description,
            UpgradeType2D upgradeType,
            float amount,
            PerkRarity2D rarity)
        {
            Id = id;
            Title = title;
            Description = description;
            UpgradeType = upgradeType;
            Amount = amount;
            Rarity = rarity;
        }
    }

    public static class UpgradeCatalog2D
    {
        private sealed class Entry
        {
            public string Id;
            public string Title;
            public string Description;
            public UpgradeType2D UpgradeType;
            public float Amount;
            public PerkRarity2D Rarity;
            public int MinimumRoomIndex;
            public float Weight;
        }

        private static readonly List<Entry> Entries = new List<Entry>
        {
            new Entry { Id = "vitality_i", Title = "Vitality I", Description = "+12 max health", UpgradeType = UpgradeType2D.MaxHealth, Amount = 12f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 1.1f },
            new Entry { Id = "sharpshot_i", Title = "Sharpshot I", Description = "+4 weapon damage", UpgradeType = UpgradeType2D.Damage, Amount = 4f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 1f },
            new Entry { Id = "quickdraw_i", Title = "Quickdraw I", Description = "+0.35 attack rate", UpgradeType = UpgradeType2D.AttackRate, Amount = 0.35f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 0.95f },
            new Entry { Id = "strider_i", Title = "Strider I", Description = "+0.9 move speed", UpgradeType = UpgradeType2D.MoveSpeed, Amount = 0.9f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 0.95f },
            new Entry { Id = "velocity_i", Title = "Velocity I", Description = "+2.5 projectile speed", UpgradeType = UpgradeType2D.ProjectileSpeed, Amount = 2.5f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 0.9f },
            new Entry { Id = "guard_i", Title = "Guard I", Description = "+1.5 armor", UpgradeType = UpgradeType2D.Armor, Amount = 1.5f, Rarity = PerkRarity2D.Common, MinimumRoomIndex = 0, Weight = 0.85f },
            new Entry { Id = "vitality_ii", Title = "Vitality II", Description = "+18 max health", UpgradeType = UpgradeType2D.MaxHealth, Amount = 18f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.95f },
            new Entry { Id = "sharpshot_ii", Title = "Sharpshot II", Description = "+6 weapon damage", UpgradeType = UpgradeType2D.Damage, Amount = 6f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.9f },
            new Entry { Id = "quickdraw_ii", Title = "Quickdraw II", Description = "+0.55 attack rate", UpgradeType = UpgradeType2D.AttackRate, Amount = 0.55f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.9f },
            new Entry { Id = "strider_ii", Title = "Strider II", Description = "+1.35 move speed", UpgradeType = UpgradeType2D.MoveSpeed, Amount = 1.35f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.8f },
            new Entry { Id = "velocity_ii", Title = "Velocity II", Description = "+4 projectile speed", UpgradeType = UpgradeType2D.ProjectileSpeed, Amount = 4f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.8f },
            new Entry { Id = "guard_ii", Title = "Guard II", Description = "+2.5 armor", UpgradeType = UpgradeType2D.Armor, Amount = 2.5f, Rarity = PerkRarity2D.Uncommon, MinimumRoomIndex = 2, Weight = 0.75f },
            new Entry { Id = "vitality_iii", Title = "Vitality III", Description = "+28 max health", UpgradeType = UpgradeType2D.MaxHealth, Amount = 28f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.8f },
            new Entry { Id = "sharpshot_iii", Title = "Sharpshot III", Description = "+9 weapon damage", UpgradeType = UpgradeType2D.Damage, Amount = 9f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.75f },
            new Entry { Id = "quickdraw_iii", Title = "Quickdraw III", Description = "+0.8 attack rate", UpgradeType = UpgradeType2D.AttackRate, Amount = 0.8f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.75f },
            new Entry { Id = "strider_iii", Title = "Strider III", Description = "+1.9 move speed", UpgradeType = UpgradeType2D.MoveSpeed, Amount = 1.9f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.65f },
            new Entry { Id = "velocity_iii", Title = "Velocity III", Description = "+6.5 projectile speed", UpgradeType = UpgradeType2D.ProjectileSpeed, Amount = 6.5f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.65f },
            new Entry { Id = "guard_iii", Title = "Guard III", Description = "+4 armor", UpgradeType = UpgradeType2D.Armor, Amount = 4f, Rarity = PerkRarity2D.Rare, MinimumRoomIndex = 5, Weight = 0.6f },
        };

        public static List<PerkChoice2D> BuildDraft(int roomIndex, bool premium, int count)
        {
            List<Entry> eligible = new List<Entry>();
            for (int i = 0; i < Entries.Count; i++)
            {
                Entry entry = Entries[i];
                if (entry.MinimumRoomIndex > roomIndex)
                {
                    continue;
                }

                if (!premium && entry.Rarity == PerkRarity2D.Rare && roomIndex < 5)
                {
                    continue;
                }

                eligible.Add(entry);
            }

            List<PerkChoice2D> result = new List<PerkChoice2D>();
            HashSet<UpgradeType2D> usedTypes = new HashSet<UpgradeType2D>();
            for (int i = 0; i < count; i++)
            {
                List<Entry> remaining = new List<Entry>();
                for (int j = 0; j < eligible.Count; j++)
                {
                    if (!usedTypes.Contains(eligible[j].UpgradeType))
                    {
                        remaining.Add(eligible[j]);
                    }
                }

                if (remaining.Count == 0)
                {
                    break;
                }

                PerkRarity2D targetRarity = RollRarity(roomIndex, premium);
                Entry picked = PickWeighted(FilterByRarity(remaining, targetRarity));
                if (picked == null)
                {
                    picked = PickWeighted(remaining);
                }

                if (picked == null)
                {
                    break;
                }

                usedTypes.Add(picked.UpgradeType);
                result.Add(new PerkChoice2D(
                    picked.Id,
                    picked.Title,
                    picked.Description,
                    picked.UpgradeType,
                    picked.Amount,
                    picked.Rarity));
            }

            return result;
        }

        private static List<Entry> FilterByRarity(List<Entry> pool, PerkRarity2D rarity)
        {
            List<Entry> result = new List<Entry>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Rarity == rarity)
                {
                    result.Add(pool[i]);
                }
            }

            return result;
        }

        private static Entry PickWeighted(List<Entry> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                total += Mathf.Max(0.05f, pool[i].Weight);
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= Mathf.Max(0.05f, pool[i].Weight);
                if (roll <= 0f)
                {
                    return pool[i];
                }
            }

            return pool[pool.Count - 1];
        }

        private static PerkRarity2D RollRarity(int roomIndex, bool premium)
        {
            float common;
            float uncommon;
            float rare;

            if (premium)
            {
                common = 0.1f;
                uncommon = roomIndex >= 5 ? 0.45f : 0.6f;
                rare = roomIndex >= 5 ? 0.45f : 0.3f;
            }
            else if (roomIndex >= 5)
            {
                common = 0.45f;
                uncommon = 0.4f;
                rare = 0.15f;
            }
            else if (roomIndex >= 2)
            {
                common = 0.62f;
                uncommon = 0.33f;
                rare = 0.05f;
            }
            else
            {
                common = 0.78f;
                uncommon = 0.22f;
                rare = 0f;
            }

            float roll = UnityEngine.Random.value * (common + uncommon + rare);
            if (roll < common)
            {
                return PerkRarity2D.Common;
            }

            if (roll < common + uncommon)
            {
                return PerkRarity2D.Uncommon;
            }

            return PerkRarity2D.Rare;
        }
    }
}
