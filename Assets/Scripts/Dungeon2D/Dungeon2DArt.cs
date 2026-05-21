using System.Collections.Generic;
using UnityEngine;

namespace Dungeon2D
{
    public sealed class Dungeon2DArtLibrary
    {
        public const float EnvironmentPixelsPerUnit = 128f;
        public const float PropPixelsPerUnit = 80f;
        public const float CharacterPixelsPerUnit = 256f;
        public const float AnimationPixelsPerUnit = 160f;
        private const int AnimationColumns = 16;
        private const int AnimationRows = 8;
        private const int AnimationFramesPerState = 4;

        private readonly Dictionary<EnvironmentSpriteId, Sprite> environmentSprites = new Dictionary<EnvironmentSpriteId, Sprite>();
        private readonly Dictionary<CharacterSpriteId, Sprite> characterSprites = new Dictionary<CharacterSpriteId, Sprite>();
        private readonly Dictionary<CharacterSpriteId, Rect> characterRects = new Dictionary<CharacterSpriteId, Rect>();
        private readonly Dictionary<string, Sprite[]> characterAnimations = new Dictionary<string, Sprite[]>();
        private Texture2D environmentAtlas;
        private Texture2D propAtlas;
        private Texture2D roomFloorTexture;
        private Texture2D woodenBridgeTexture;
        private Texture2D bridgeDoorClosedTexture;
        private Texture2D bridgeDoorOpenTexture;
        private Texture2D characterSheet;
        private Texture2D animationSheet;
        private Texture2D projectileTexture;
        private Texture2D slashTexture;
        private Sprite projectileSprite;
        private Sprite slashSprite;
        private Sprite fallbackSprite;

        public Sprite GetEnvironmentSprite(EnvironmentSpriteId id)
        {
            EnsureLoaded();
            if (environmentSprites.TryGetValue(id, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            return fallbackSprite;
        }

        public Sprite GetCharacterSprite(CharacterSpriteId id)
        {
            EnsureLoaded();
            if (characterSprites.TryGetValue(id, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            return fallbackSprite;
        }

        public Sprite[] GetCharacterAnimation(CharacterSpriteId id, DungeonSpriteAnimation animation)
        {
            EnsureLoaded();
            string key = id + "_" + animation;
            if (characterAnimations.TryGetValue(key, out Sprite[] frames) && frames != null && frames.Length > 0)
            {
                return frames;
            }

            Sprite[] fallbackFrames = { GetCharacterSprite(id) };
            characterAnimations[key] = fallbackFrames;
            return fallbackFrames;
        }

        public Sprite GetProjectileSprite()
        {
            EnsureLoaded();
            return projectileSprite != null ? projectileSprite : GetEnvironmentSprite(EnvironmentSpriteId.Projectile);
        }

        public Sprite GetSlashSprite()
        {
            EnsureLoaded();
            return slashSprite != null ? slashSprite : GetEnvironmentSprite(EnvironmentSpriteId.Slash);
        }

        public void Apply(SpriteRenderer renderer, EnvironmentSpriteId id, int order)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = GetEnvironmentSprite(id);
            renderer.sortingOrder = order;
        }

        public void Apply(SpriteRenderer renderer, CharacterSpriteId id, int order)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sprite = GetCharacterSprite(id);
            renderer.sortingOrder = order;
        }

        private void EnsureLoaded()
        {
            if (fallbackSprite != null)
            {
                return;
            }

            fallbackSprite = BuildFallbackSprite();
            environmentAtlas = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_environment_atlas");
            propAtlas = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_prop_cutouts");
            roomFloorTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_room_floor_single");
            woodenBridgeTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_wooden_bridge");
            bridgeDoorClosedTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_bridge_gate_closed");
            bridgeDoorOpenTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_bridge_gate_open");
            characterSheet = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_character_sheet");
            animationSheet = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_animation_sheet");
            projectileTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_projectile_sprite");
            slashTexture = Resources.Load<Texture2D>("Dungeon2D/Art/dungeon_slash_sprite");

            if (environmentAtlas != null)
            {
                BuildEnvironmentSprites();
            }

            if (propAtlas != null)
            {
                BuildPropSprites();
            }

            BuildStandaloneEnvironmentSprites();

            if (characterSheet != null)
            {
                BuildCharacterSprites();
            }

            if (projectileTexture != null)
            {
                projectileSprite = Sprite.Create(projectileTexture, new Rect(0f, 0f, projectileTexture.width, projectileTexture.height), new Vector2(0.5f, 0.5f), 96f, 0, SpriteMeshType.FullRect);
            }

            if (slashTexture != null)
            {
                slashSprite = Sprite.Create(slashTexture, new Rect(0f, 0f, slashTexture.width, slashTexture.height), new Vector2(0.5f, 0.5f), 128f, 0, SpriteMeshType.FullRect);
            }
        }

        private void BuildEnvironmentSprites()
        {
            AddEnvironment(EnvironmentSpriteId.Floor, 0, 3);
            AddEnvironment(EnvironmentSpriteId.CrackedFloor, 1, 3);
            AddEnvironment(EnvironmentSpriteId.MossFloor, 2, 3);
            AddEnvironment(EnvironmentSpriteId.Wall, 3, 3);
            AddEnvironment(EnvironmentSpriteId.IronBars, 0, 2);
            AddEnvironment(EnvironmentSpriteId.Door, 1, 2);
            AddEnvironment(EnvironmentSpriteId.Archway, 2, 2);
            AddEnvironment(EnvironmentSpriteId.Chest, 3, 2);
            AddEnvironment(EnvironmentSpriteId.OpenChest, 3, 2);
            AddEnvironment(EnvironmentSpriteId.Altar, 0, 1);
            AddEnvironment(EnvironmentSpriteId.AltarSpent, 0, 1);
            AddEnvironment(EnvironmentSpriteId.HealthPotion, 1, 1);
            AddEnvironment(EnvironmentSpriteId.SpeedPotion, 2, 1);
            AddEnvironment(EnvironmentSpriteId.Key, 3, 1);
            AddEnvironment(EnvironmentSpriteId.Projectile, 0, 0);
            AddEnvironment(EnvironmentSpriteId.HazardRune, 1, 0);
            AddEnvironment(EnvironmentSpriteId.Slash, 2, 0);
            AddEnvironment(EnvironmentSpriteId.Panel, 3, 0);
        }

        private void BuildStandaloneEnvironmentSprites()
        {
            AddStandaloneEnvironment(EnvironmentSpriteId.RoomFloor, roomFloorTexture, EnvironmentPixelsPerUnit);
            AddStandaloneEnvironment(EnvironmentSpriteId.WoodenBridge, woodenBridgeTexture, EnvironmentPixelsPerUnit);
            AddStandaloneEnvironment(EnvironmentSpriteId.BridgeDoorClosed, bridgeDoorClosedTexture, EnvironmentPixelsPerUnit);
            AddStandaloneEnvironment(EnvironmentSpriteId.BridgeDoorOpen, bridgeDoorOpenTexture, EnvironmentPixelsPerUnit);

            UseEnvironmentFallback(EnvironmentSpriteId.RoomFloor, EnvironmentSpriteId.Floor);
            UseEnvironmentFallback(EnvironmentSpriteId.WoodenBridge, EnvironmentSpriteId.CrackedFloor);
            UseEnvironmentFallback(EnvironmentSpriteId.BridgeDoorClosed, EnvironmentSpriteId.Door);
            UseEnvironmentFallback(EnvironmentSpriteId.BridgeDoorOpen, EnvironmentSpriteId.Archway);
        }

        private void BuildPropSprites()
        {
            AddProp(EnvironmentSpriteId.IronBars, 0, 3);
            AddProp(EnvironmentSpriteId.Door, 1, 3);
            AddProp(EnvironmentSpriteId.Archway, 2, 3);
            AddProp(EnvironmentSpriteId.Chest, 3, 3);
            AddProp(EnvironmentSpriteId.OpenChest, 0, 2);
            AddProp(EnvironmentSpriteId.Altar, 1, 2);
            AddProp(EnvironmentSpriteId.AltarSpent, 2, 2);
            AddProp(EnvironmentSpriteId.HealthPotion, 3, 2);
            AddProp(EnvironmentSpriteId.SpeedPotion, 0, 1);
            AddProp(EnvironmentSpriteId.Key, 1, 1);
            AddProp(EnvironmentSpriteId.HazardRune, 2, 1);
            AddProp(EnvironmentSpriteId.Panel, 3, 1);
            AddProp(EnvironmentSpriteId.Projectile, 0, 0);
            AddProp(EnvironmentSpriteId.Slash, 1, 0);
        }

        private void BuildCharacterSprites()
        {
            AddCharacter(CharacterSpriteId.Hero, 0, 1);
            AddCharacter(CharacterSpriteId.MeleeEnemy, 1, 1);
            AddCharacter(CharacterSpriteId.RangedEnemy, 2, 1);
            AddCharacter(CharacterSpriteId.TankEnemy, 3, 1);
            AddCharacter(CharacterSpriteId.EliteRangedEnemy, 0, 0);
            AddCharacter(CharacterSpriteId.Minion, 1, 0);
            AddCharacter(CharacterSpriteId.Boss, 2, 0);
            AddCharacter(CharacterSpriteId.BossCrystal, 3, 0);
            if (animationSheet != null)
            {
                BuildCharacterAnimationsFromSheet();
            }
            else
            {
                BuildFallbackCharacterAnimations();
            }
        }

        private void AddEnvironment(EnvironmentSpriteId id, int column, int rowFromBottom)
        {
            int cellWidth = environmentAtlas.width / 4;
            int cellHeight = environmentAtlas.height / 4;
            Rect rect = new Rect(column * cellWidth, rowFromBottom * cellHeight, cellWidth, cellHeight);
            environmentSprites[id] = Sprite.Create(environmentAtlas, rect, new Vector2(0.5f, 0.5f), EnvironmentPixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void AddProp(EnvironmentSpriteId id, int column, int rowFromBottom)
        {
            int cellWidth = propAtlas.width / 4;
            int cellHeight = propAtlas.height / 4;
            Rect rect = new Rect(column * cellWidth, rowFromBottom * cellHeight, cellWidth, cellHeight);
            environmentSprites[id] = Sprite.Create(propAtlas, rect, new Vector2(0.5f, 0.5f), PropPixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void AddStandaloneEnvironment(EnvironmentSpriteId id, Texture2D texture, float pixelsPerUnit)
        {
            if (texture == null)
            {
                return;
            }

            Rect rect = new Rect(0f, 0f, texture.width, texture.height);
            environmentSprites[id] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void UseEnvironmentFallback(EnvironmentSpriteId target, EnvironmentSpriteId fallback)
        {
            if (environmentSprites.ContainsKey(target))
            {
                return;
            }

            if (environmentSprites.TryGetValue(fallback, out Sprite sprite) && sprite != null)
            {
                environmentSprites[target] = sprite;
            }
        }

        private void AddCharacter(CharacterSpriteId id, int column, int rowFromBottom)
        {
            int cellWidth = characterSheet.width / 4;
            int cellHeight = characterSheet.height / 2;
            Rect rect = new Rect(column * cellWidth, rowFromBottom * cellHeight, cellWidth, cellHeight);
            characterRects[id] = rect;
            characterSprites[id] = Sprite.Create(characterSheet, rect, new Vector2(0.5f, 0.5f), CharacterPixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private void BuildCharacterAnimationsFromSheet()
        {
            BuildOriginalLookAnimationSet(CharacterSpriteId.Hero);
            BuildAnimationSet(CharacterSpriteId.MeleeEnemy);
            BuildAnimationSet(CharacterSpriteId.RangedEnemy);
            BuildAnimationSet(CharacterSpriteId.TankEnemy);
            BuildOriginalLookAnimationSet(CharacterSpriteId.EliteRangedEnemy);
            BuildAnimationSet(CharacterSpriteId.Minion);
            BuildAnimationSet(CharacterSpriteId.Boss);
            BuildAnimationSet(CharacterSpriteId.BossCrystal);
        }

        private void BuildAnimationSet(CharacterSpriteId id)
        {
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Idle)] = BuildAnimationFrames(id, DungeonSpriteAnimation.Idle);
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Walk)] = BuildAnimationFrames(id, DungeonSpriteAnimation.Walk);
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.PrimaryAttack)] = BuildAnimationFrames(id, DungeonSpriteAnimation.PrimaryAttack);
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.SecondaryAttack)] = BuildAnimationFrames(id, DungeonSpriteAnimation.SecondaryAttack);
        }

        private void BuildOriginalLookAnimationSet(CharacterSpriteId id)
        {
            if (!characterRects.TryGetValue(id, out Rect rect))
            {
                BuildAnimationSet(id);
                return;
            }

            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Idle)] = BuildFallbackFrames(rect, new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.508f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.492f)
            });
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Walk)] = BuildFallbackFrames(rect, new[]
            {
                new Vector2(0.492f, 0.506f),
                new Vector2(0.508f, 0.494f),
                new Vector2(0.506f, 0.506f),
                new Vector2(0.494f, 0.494f)
            });
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.PrimaryAttack)] = BuildFallbackFrames(rect, new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.472f, 0.492f),
                new Vector2(0.456f, 0.488f),
                new Vector2(0.5f, 0.5f)
            });
            characterAnimations[AnimationKey(id, DungeonSpriteAnimation.SecondaryAttack)] = BuildFallbackFrames(rect, new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.528f, 0.492f),
                new Vector2(0.544f, 0.488f),
                new Vector2(0.5f, 0.5f)
            });
        }

        private Sprite[] BuildAnimationFrames(CharacterSpriteId id, DungeonSpriteAnimation animation)
        {
            int cellWidth = animationSheet.width / AnimationColumns;
            int cellHeight = animationSheet.height / AnimationRows;
            int rowFromTop = AnimationRowForCharacter(id);
            int startColumn = AnimationStartColumn(animation);
            Sprite[] frames = new Sprite[AnimationFramesPerState];

            for (int i = 0; i < frames.Length; i++)
            {
                int column = startColumn + i;
                int yFromBottom = AnimationRows - 1 - rowFromTop;
                Rect rect = new Rect(column * cellWidth, yFromBottom * cellHeight, cellWidth, cellHeight);
                frames[i] = Sprite.Create(animationSheet, rect, new Vector2(0.5f, 0.5f), AnimationPixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            return frames;
        }

        private void BuildFallbackCharacterAnimations()
        {
            foreach (KeyValuePair<CharacterSpriteId, Rect> pair in characterRects)
            {
                CharacterSpriteId id = pair.Key;
                Rect rect = pair.Value;
                characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Idle)] = BuildFallbackFrames(rect, new[]
                {
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.515f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.485f)
                });
                characterAnimations[AnimationKey(id, DungeonSpriteAnimation.Walk)] = BuildFallbackFrames(rect, new[]
                {
                    new Vector2(0.49f, 0.51f),
                    new Vector2(0.515f, 0.49f),
                    new Vector2(0.51f, 0.515f),
                    new Vector2(0.485f, 0.49f)
                });
                characterAnimations[AnimationKey(id, DungeonSpriteAnimation.PrimaryAttack)] = BuildFallbackFrames(rect, new[]
                {
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.46f, 0.49f),
                    new Vector2(0.43f, 0.485f),
                    new Vector2(0.5f, 0.5f)
                });
                characterAnimations[AnimationKey(id, DungeonSpriteAnimation.SecondaryAttack)] = BuildFallbackFrames(rect, new[]
                {
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.54f, 0.485f),
                    new Vector2(0.58f, 0.48f),
                    new Vector2(0.5f, 0.5f)
                });
            }
        }

        private Sprite[] BuildFallbackFrames(Rect rect, Vector2[] pivots)
        {
            Sprite[] frames = new Sprite[pivots.Length];
            for (int i = 0; i < pivots.Length; i++)
            {
                frames[i] = Sprite.Create(characterSheet, rect, pivots[i], CharacterPixelsPerUnit, 0, SpriteMeshType.FullRect);
            }

            return frames;
        }

        private static int AnimationRowForCharacter(CharacterSpriteId id)
        {
            switch (id)
            {
                case CharacterSpriteId.MeleeEnemy:
                    return 1;
                case CharacterSpriteId.RangedEnemy:
                    return 2;
                case CharacterSpriteId.TankEnemy:
                    return 3;
                case CharacterSpriteId.EliteRangedEnemy:
                    return 4;
                case CharacterSpriteId.Minion:
                    return 5;
                case CharacterSpriteId.Boss:
                    return 6;
                case CharacterSpriteId.BossCrystal:
                    return 7;
                default:
                    return 0;
            }
        }

        private static int AnimationStartColumn(DungeonSpriteAnimation animation)
        {
            switch (animation)
            {
                case DungeonSpriteAnimation.Walk:
                    return 4;
                case DungeonSpriteAnimation.PrimaryAttack:
                    return 8;
                case DungeonSpriteAnimation.SecondaryAttack:
                    return 12;
                default:
                    return 0;
            }
        }

        private static string AnimationKey(CharacterSpriteId id, DungeonSpriteAnimation animation)
        {
            return id + "_" + animation;
        }

        private static Sprite BuildFallbackSprite()
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.SetPixels(new[]
            {
                Color.magenta,
                Color.black,
                Color.black,
                Color.magenta
            });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        }
    }
}
