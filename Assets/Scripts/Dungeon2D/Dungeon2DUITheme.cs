using UnityEngine;

namespace Dungeon2D
{
    internal sealed class Dungeon2DUITheme
    {
        public Sprite MenuBackground { get; private set; }
        public Sprite UpgradeBackground { get; private set; }
        public Sprite Panel { get; private set; }
        public Sprite TitlePlaque { get; private set; }
        public Sprite Button { get; private set; }
        public Sprite ButtonHighlighted { get; private set; }
        public Sprite ItemBadge { get; private set; }
        public Sprite Notification { get; private set; }
        public Sprite BarTrack { get; private set; }
        public Sprite HealthFill { get; private set; }
        public Sprite BossFill { get; private set; }
        public Sprite CardCommon { get; private set; }
        public Sprite CardUncommon { get; private set; }
        public Sprite CardRare { get; private set; }
        public Sprite IconHealth { get; private set; }
        public Sprite IconDamage { get; private set; }
        public Sprite IconAttackRate { get; private set; }
        public Sprite IconMoveSpeed { get; private set; }
        public Sprite IconProjectileSpeed { get; private set; }
        public Sprite IconArmor { get; private set; }

        public Sprite GetPerkCard(PerkRarity2D rarity)
        {
            switch (rarity)
            {
                case PerkRarity2D.Rare:
                    return CardRare;
                case PerkRarity2D.Uncommon:
                    return CardUncommon;
                default:
                    return CardCommon;
            }
        }

        public Sprite GetUpgradeIcon(UpgradeType2D upgradeType)
        {
            switch (upgradeType)
            {
                case UpgradeType2D.MaxHealth:
                    return IconHealth;
                case UpgradeType2D.Damage:
                    return IconDamage;
                case UpgradeType2D.AttackRate:
                    return IconAttackRate;
                case UpgradeType2D.MoveSpeed:
                    return IconMoveSpeed;
                case UpgradeType2D.ProjectileSpeed:
                    return IconProjectileSpeed;
                case UpgradeType2D.Armor:
                    return IconArmor;
                default:
                    return null;
            }
        }

        public static Dungeon2DUITheme Load()
        {
            Dungeon2DUITheme loaded = new Dungeon2DUITheme();

            Texture2D menuBackground = Resources.Load<Texture2D>("Dungeon2D/UI/dungeon_menu_background");
            if (menuBackground != null)
            {
                loaded.MenuBackground = CreateFullSprite(menuBackground);
            }

            Texture2D upgradeBackground = Resources.Load<Texture2D>("Dungeon2D/UI/dungeon_upgrade_background");
            if (upgradeBackground != null)
            {
                loaded.UpgradeBackground = CreateFullSprite(upgradeBackground);
            }

            Texture2D atlas = Resources.Load<Texture2D>("Dungeon2D/UI/dungeon_ui_atlas");
            if (atlas != null)
            {
                loaded.Panel = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 41f, 18f, 684f, 484f), 58f);
                loaded.TitlePlaque = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 749f, 24f, 647f, 186f), 58f);
                loaded.Button = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 758f, 234f, 409f, 112f), 34f);
                loaded.ButtonHighlighted = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 758f, 371f, 408f, 116f), 34f);
                loaded.ItemBadge = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 65f, 516f, 172f, 159f), 28f);
                loaded.Notification = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 276f, 536f, 600f, 113f), 36f);
                loaded.BarTrack = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 22f, 685f, 768f, 111f), 34f);
                loaded.HealthFill = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 32f, 812f, 758f, 57f), 22f);
                loaded.BossFill = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 31f, 911f, 759f, 60f), 22f);
                loaded.CardCommon = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 823f, 691f, 210f, 303f), 34f);
                loaded.CardUncommon = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 1056f, 687f, 215f, 307f), 34f);
                loaded.CardRare = CreateSlicedSprite(atlas, RectFromTopLeft(atlas, 1292f, 687f, 211f, 307f), 34f);
            }

            Texture2D icons = Resources.Load<Texture2D>("Dungeon2D/UI/dungeon_upgrade_icons");
            if (icons != null)
            {
                loaded.IconHealth = CreateSimpleSprite(icons, RectFromTopLeft(icons, 78f, 42f, 419f, 444f));
                loaded.IconDamage = CreateSimpleSprite(icons, RectFromTopLeft(icons, 557f, 42f, 419f, 444f));
                loaded.IconAttackRate = CreateSimpleSprite(icons, RectFromTopLeft(icons, 1037f, 42f, 419f, 444f));
                loaded.IconMoveSpeed = CreateSimpleSprite(icons, RectFromTopLeft(icons, 78f, 527f, 419f, 446f));
                loaded.IconProjectileSpeed = CreateSimpleSprite(icons, RectFromTopLeft(icons, 557f, 527f, 419f, 446f));
                loaded.IconArmor = CreateSimpleSprite(icons, RectFromTopLeft(icons, 1037f, 528f, 419f, 445f));
            }

            return loaded;
        }

        private static Sprite CreateFullSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite CreateSimpleSprite(Texture2D texture, Rect rect)
        {
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite CreateSlicedSprite(Texture2D texture, Rect rect, float border)
        {
            Vector4 safeBorder = new Vector4(border, border, border, border);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, safeBorder);
        }

        private static Rect RectFromTopLeft(Texture2D texture, float x, float y, float width, float height)
        {
            return new Rect(x, texture.height - y - height, width, height);
        }
    }
}
