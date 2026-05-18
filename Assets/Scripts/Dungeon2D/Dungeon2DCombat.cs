using UnityEngine;

namespace Dungeon2D
{
    public sealed class DungeonProjectile2D : MonoBehaviour
    {
        private static Material trailMaterial;

        private Dungeon2DGame game;
        private Vector2 direction;
        private float speed;
        private float damage;
        private float expiresAt;
        private bool playerOwned;
        private GameObject owner;

        public void Launch(
            Dungeon2DGame ownerGame,
            Sprite sprite,
            Vector2 start,
            Vector2 launchDirection,
            float launchSpeed,
            float launchDamage,
            float lifeTime,
            bool isPlayerOwned,
            GameObject launchOwner,
            float scale,
            Color color)
        {
            game = ownerGame;
            direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.up;
            speed = Mathf.Max(0.1f, launchSpeed);
            damage = Mathf.Max(1f, launchDamage);
            expiresAt = Time.time + Mathf.Max(0.1f, lifeTime);
            playerOwned = isPlayerOwned;
            owner = launchOwner;
            transform.position = start;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            SpriteRenderer renderer = gameObject.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 25;
            ConfigureTrail(scale, color, playerOwned);

            CircleCollider2D collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = 0.34f;

            Rigidbody2D body = gameObject.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        private void ConfigureTrail(float scale, Color projectileColor, bool isPlayerOwned)
        {
            TrailRenderer trail = gameObject.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
            }

            trail.material = GetTrailMaterial();
            trail.time = isPlayerOwned ? 0.18f : 0.24f;
            trail.minVertexDistance = 0.035f;
            trail.numCapVertices = 0;
            trail.numCornerVertices = 0;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.emitting = true;
            trail.sortingOrder = 24;
            trail.startWidth = Mathf.Lerp(0.13f, 0.24f, Mathf.Clamp01(scale));
            trail.endWidth = 0f;
            trail.colorGradient = CreateTrailGradient(projectileColor, isPlayerOwned);
            trail.Clear();
        }

        private static Material GetTrailMaterial()
        {
            if (trailMaterial != null)
            {
                return trailMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            trailMaterial = shader != null ? new Material(shader) : null;
            return trailMaterial;
        }

        private static Gradient CreateTrailGradient(Color projectileColor, bool isPlayerOwned)
        {
            Color core = Color.Lerp(projectileColor, Color.white, isPlayerOwned ? 0.32f : 0.18f);
            Color ember = Color.Lerp(projectileColor, new Color(0.08f, 0.05f, 0.12f, 1f), 0.28f);
            float startAlpha = isPlayerOwned ? 0.74f : 0.68f;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(core, 0f),
                    new GradientColorKey(projectileColor, 0.34f),
                    new GradientColorKey(ember, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(0.42f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private void Update()
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == null || other.gameObject == owner)
            {
                return;
            }

            if (other.isTrigger && other.GetComponent<DungeonHazard2D>() == null)
            {
                return;
            }

            DungeonPlayer2D player = other.GetComponentInParent<DungeonPlayer2D>();
            DungeonEnemy2D enemy = other.GetComponentInParent<DungeonEnemy2D>();
            DungeonBoss2D boss = other.GetComponentInParent<DungeonBoss2D>();

            if (playerOwned && player != null)
            {
                return;
            }

            if (!playerOwned && (enemy != null || boss != null))
            {
                return;
            }

            IDamageable2D damageable = other.GetComponentInParent<IDamageable2D>();
            if (damageable != null && !damageable.IsDead)
            {
                damageable.TakeDamage(damage, owner);
                game?.PlayHitFeedback();
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }

    public interface IDungeonInteractable2D
    {
        string InteractionLabel { get; }
        bool CanInteract(DungeonPlayer2D player);
        void Interact(DungeonPlayer2D player);
    }

    public sealed class DungeonPickup2D : MonoBehaviour, IDungeonInteractable2D
    {
        private Dungeon2DGame game;
        private PickupKind kind;
        private int amount;
        private bool collected;

        public string InteractionLabel
        {
            get
            {
                switch (kind)
                {
                    case PickupKind.HealthPotion:
                        return "Pick Health Potion";
                    case PickupKind.SpeedBoost:
                        return "Pick Speed Boost";
                    default:
                        return "Pick Dungeon Key";
                }
            }
        }

        public void Configure(Dungeon2DGame owner, PickupKind pickupKind, int pickupAmount)
        {
            game = owner;
            kind = pickupKind;
            amount = Mathf.Max(1, pickupAmount);

            CircleCollider2D collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = 0.55f;
        }

        public bool CanInteract(DungeonPlayer2D player)
        {
            return !collected && player != null;
        }

        public void Interact(DungeonPlayer2D player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            Collect(player);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            DungeonPlayer2D player = other.GetComponentInParent<DungeonPlayer2D>();
            if (player != null)
            {
                Collect(player);
            }
        }

        private void Collect(DungeonPlayer2D player)
        {
            if (collected || player == null)
            {
                return;
            }

            collected = true;
            game.Inventory.Add(kind, amount);
            game.Notify(InteractionLabel.Replace("Pick ", "Picked: ") + " x" + amount);
            game.RefreshHud();
            game.PlayPickupFeedback();
            Destroy(gameObject);
        }
    }

    public sealed class DungeonUpgradeStation2D : MonoBehaviour, IDungeonInteractable2D
    {
        private Dungeon2DGame game;
        private SpriteRenderer spriteRenderer;
        private int roomIndex;
        private bool used;
        private bool draftOpen;

        public string InteractionLabel
        {
            get { return used ? "Used" : "Draft Perk"; }
        }

        public void Configure(Dungeon2DGame owner, int sourceRoomIndex)
        {
            game = owner;
            roomIndex = Mathf.Max(0, sourceRoomIndex);
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureTrigger(1.15f);
        }

        public bool CanInteract(DungeonPlayer2D player)
        {
            return !used && !draftOpen && player != null;
        }

        public void Interact(DungeonPlayer2D player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            draftOpen = true;
            game.OpenPerkDraft(
                "Choose a Perk",
                "Pick 1 of 3 upgrades for this run",
                roomIndex,
                false,
                choice =>
                {
                    draftOpen = false;
                    used = true;
                    SetSpentVisual();
                    game.PlayerStats.ApplyUpgrade(choice.UpgradeType, choice.Amount);
                    game.ClampPlayerHealthAfterStatChange();
                    game.Notify("Perk gained: " + choice.Title);
                    game.PlayRewardFeedback();
                });
        }

        private void EnsureTrigger(float radius)
        {
            CircleCollider2D collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = radius;
        }

        private void SetSpentVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && game != null)
            {
                spriteRenderer.sprite = game.Art.GetEnvironmentSprite(EnvironmentSpriteId.AltarSpent);
                spriteRenderer.color = new Color(0.74f, 0.72f, 0.68f, 1f);
            }
        }
    }

    public sealed class DungeonLockedChest2D : MonoBehaviour, IDungeonInteractable2D
    {
        private Dungeon2DGame game;
        private SpriteRenderer spriteRenderer;
        private int roomIndex;
        private bool opened;
        private bool draftOpen;
        private string title;
        private string subtitle;

        public string InteractionLabel
        {
            get
            {
                if (opened)
                {
                    return "Opened";
                }

                return game != null && game.Inventory.Keys > 0 ? "Unlock Cache (1 Key)" : "Need 1 Key";
            }
        }

        public void Configure(Dungeon2DGame owner, int sourceRoomIndex, string draftTitle, string draftSubtitle)
        {
            game = owner;
            roomIndex = Mathf.Max(0, sourceRoomIndex);
            title = string.IsNullOrWhiteSpace(draftTitle) ? "Locked Cache" : draftTitle;
            subtitle = string.IsNullOrWhiteSpace(draftSubtitle) ? "Spend 1 key to claim a premium perk" : draftSubtitle;
            spriteRenderer = GetComponent<SpriteRenderer>();
            EnsureTrigger(1.2f);
        }

        public bool CanInteract(DungeonPlayer2D player)
        {
            return !opened && !draftOpen && player != null;
        }

        public void Interact(DungeonPlayer2D player)
        {
            if (!CanInteract(player))
            {
                return;
            }

            if (game.Inventory.Keys <= 0)
            {
                game.Notify("Cache locked - you need 1 key");
                return;
            }

            game.Inventory.Keys -= 1;
            game.RefreshHud();
            draftOpen = true;
            opened = true;
            SetOpenedVisual();
            game.OpenPerkDraft(
                title,
                subtitle,
                roomIndex,
                true,
                choice =>
                {
                    draftOpen = false;
                    game.PlayerStats.ApplyUpgrade(choice.UpgradeType, choice.Amount);
                    game.ClampPlayerHealthAfterStatChange();
                    game.Notify("Cache reward: " + choice.Title);
                    game.PlayRewardFeedback();
                });
        }

        private void EnsureTrigger(float radius)
        {
            CircleCollider2D collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = radius;
        }

        private void SetOpenedVisual()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null && game != null)
            {
                spriteRenderer.sprite = game.Art.GetEnvironmentSprite(EnvironmentSpriteId.OpenChest);
                spriteRenderer.color = Color.white;
            }
        }
    }

    public sealed class DungeonDoor2D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private BoxCollider2D blockingCollider;

        private Dungeon2DArtLibrary art;
        private bool open = true;

        public bool IsOpen
        {
            get { return open; }
        }

        public void Configure(Dungeon2DArtLibrary artLibrary, bool startsOpen)
        {
            art = artLibrary;
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (blockingCollider == null)
            {
                blockingCollider = GetComponent<BoxCollider2D>();
            }

            SetOpen(startsOpen, true);
        }

        public void SetOpen(bool value, bool immediate = false)
        {
            open = value;
            if (blockingCollider != null)
            {
                blockingCollider.enabled = !open;
            }

            if (spriteRenderer != null && art != null)
            {
                spriteRenderer.sprite = art.GetEnvironmentSprite(open ? EnvironmentSpriteId.Archway : EnvironmentSpriteId.Door);
                spriteRenderer.color = open ? new Color(0.82f, 0.82f, 0.86f, 0.72f) : Color.white;
                spriteRenderer.sortingOrder = 8;
            }

            if (!immediate)
            {
                transform.localScale = open ? new Vector3(1f, Dungeon2DConfig.DoorOpenVisualHeight, 1f) : Vector3.one;
            }
        }
    }

    public sealed class DungeonHazard2D : MonoBehaviour
    {
        private Dungeon2DGame game;
        private float damage;
        private float tickInterval;
        private float expiresAt;
        private float nextTickAt;

        public void Configure(Dungeon2DGame owner, float damagePerTick, float tick, float duration)
        {
            game = owner;
            damage = Mathf.Max(1f, damagePerTick);
            tickInterval = Mathf.Max(0.1f, tick);
            expiresAt = Time.time + Mathf.Max(0.2f, duration);

            CircleCollider2D collider = gameObject.GetComponent<CircleCollider2D>();
            if (collider == null)
            {
                collider = gameObject.AddComponent<CircleCollider2D>();
            }

            collider.isTrigger = true;
            collider.radius = 1f;
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (Time.time < nextTickAt)
            {
                return;
            }

            DungeonPlayer2D player = other.GetComponentInParent<DungeonPlayer2D>();
            if (player == null || player.IsDead)
            {
                return;
            }

            nextTickAt = Time.time + tickInterval;
            player.TakeDamage(damage, game != null ? game.gameObject : null);
        }
    }
}
