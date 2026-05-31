using UnityEngine;

namespace Dungeon2D
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DungeonPlayer2D : MonoBehaviour, IDamageable2D
    {
        private Dungeon2DGame game;
        private Rigidbody2D body;
        private CircleCollider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private Dungeon2DVisualAnimator visualAnimator;
        private Dungeon2DWorldHealthBar healthBar;
        private Vector2 moveInput;
        private Vector2 aimDirection = Vector2.up;
        private bool isDashing;
        private Vector2 dashDirection;
        private float dashEndsAt;
        private float nextDashAt;
        private float nextDashTrailAt;
        private float nextRangedAt;
        private float nextMeleeAt;
        private float lastDamageAt = -999f;
        private float speedBoostEndsAt;
        private bool speedBoostActive;
        private float currentHealth;

        private const float DashDistance = 4.5f;
        private const float DashDuration = 0.12f;
        private const float DashCooldown = 0.45f;
        private const float DashTrailInterval = 0.016f;
        private const float MeleeCooldown = 0.45f;
        private const float InvulnerabilityDuration = 0.25f;
        private const float BodyScale = 0.94f;
        private const float HealthBarYOffset = 1.55f;

        public bool IsDead { get; private set; }

        public float CurrentHealth
        {
            get { return currentHealth; }
        }

        public float HealthRatio
        {
            get { return game == null || game.PlayerStats.MaxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / game.PlayerStats.MaxHealth); }
        }

        public Vector2 AimDirection
        {
            get { return aimDirection; }
        }

        public void Configure(Dungeon2DGame owner, Dungeon2DArtLibrary art)
        {
            game = owner;
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            bodyCollider = GetComponent<CircleCollider2D>();
            bodyCollider.radius = 0.38f;
            bodyCollider.offset = new Vector2(0f, -0.16f);
            bodyCollider.sharedMaterial = Dungeon2DPhysics.Material;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            art.Apply(spriteRenderer, CharacterSpriteId.Hero, 20);
            transform.localScale = Vector3.one * BodyScale;

            visualAnimator = GetComponent<Dungeon2DVisualAnimator>();
            if (visualAnimator == null)
            {
                visualAnimator = gameObject.AddComponent<Dungeon2DVisualAnimator>();
            }

            visualAnimator.Configure(
                spriteRenderer,
                art.GetCharacterAnimation(CharacterSpriteId.Hero, DungeonSpriteAnimation.Idle),
                art.GetCharacterAnimation(CharacterSpriteId.Hero, DungeonSpriteAnimation.Walk),
                art.GetCharacterAnimation(CharacterSpriteId.Hero, DungeonSpriteAnimation.PrimaryAttack),
                art.GetCharacterAnimation(CharacterSpriteId.Hero, DungeonSpriteAnimation.SecondaryAttack));
            visualAnimator.SetBaseVisualScale(Dungeon2DConfig.CharacterVisualScale);
            spriteRenderer = visualAnimator.Renderer;

            healthBar = GetComponent<Dungeon2DWorldHealthBar>();
            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<Dungeon2DWorldHealthBar>();
            }

            healthBar.Configure(1.35f, new Vector2(0f, HealthBarYOffset), new Color(0.26f, 0.95f, 0.42f, 1f), 38);

            ResetForRun();
        }

        public void ResetForRun()
        {
            IsDead = false;
            currentHealth = game.PlayerStats.MaxHealth;
            nextDashAt = 0f;
            nextDashTrailAt = 0f;
            nextRangedAt = 0f;
            nextMeleeAt = 0f;
            speedBoostEndsAt = 0f;
            speedBoostActive = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }
        }

        private void Update()
        {
            if (game == null || game.State != DungeonGameState.Playing || IsDead)
            {
                moveInput = Vector2.zero;
                return;
            }

            ReadMovementInput();
            UpdateAimDirection();
            HandleDashInput();
            HandleCombatInput();
            HandleConsumableInput();
            UpdateSpeedBoostTimer();

            if (visualAnimator != null)
            {
                visualAnimator.IsMoving = moveInput.sqrMagnitude > 0.001f || isDashing;
                visualAnimator.SetFacing(aimDirection.x);
            }
        }

        private void FixedUpdate()
        {
            if (game == null || game.State != DungeonGameState.Playing || IsDead || body == null)
            {
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            if (isDashing)
            {
                if (Time.time >= dashEndsAt)
                {
                    isDashing = false;
                }
                else
                {
                    SpawnDashTrail();
                    body.linearVelocity = dashDirection * (DashDistance / DashDuration);
                    return;
                }
            }

            float speed = game.PlayerStats.MoveSpeed + (speedBoostActive ? 3f : 0f);
            body.linearVelocity = moveInput * speed;
        }

        public void TakeDamage(float amount, GameObject instigator)
        {
            if (IsDead || Time.time - lastDamageAt < InvulnerabilityDuration)
            {
                return;
            }

            float mitigated = Mathf.Max(1f, amount - game.PlayerStats.Armor);
            currentHealth = Mathf.Clamp(currentHealth - mitigated, 0f, game.PlayerStats.MaxHealth);
            lastDamageAt = Time.time;

            if (visualAnimator != null)
            {
                visualAnimator.PlayHit(new Color(1f, 0.24f, 0.22f, 1f));
            }

            game.RefreshHud();
            game.PlayHitFeedback();
            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(1f, amount), 0f, game.PlayerStats.MaxHealth);
            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }
            game.RefreshHud();
        }

        public void ClampHealthToStats()
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, game.PlayerStats.MaxHealth);
            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }
            game.RefreshHud();
        }

        private void Die()
        {
            IsDead = true;
            body.linearVelocity = Vector2.zero;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (healthBar != null)
            {
                healthBar.SetRatio(0f);
            }

            if (visualAnimator != null)
            {
                visualAnimator.PlayDeath(0.5f);
            }

            game.HandlePlayerDied();
        }

        private void ReadMovementInput()
        {
            moveInput = Dungeon2DInput.ReadMovement();
        }

        private void UpdateAimDirection()
        {
            if (!Dungeon2DInput.TryGetMouseScreenPosition(out Vector2 screenPosition))
            {
                if (moveInput.sqrMagnitude > 0.001f)
                {
                    aimDirection = moveInput.normalized;
                }

                return;
            }

            Vector2 mouseWorld = game.ScreenToWorld(screenPosition);
            Vector2 direction = mouseWorld - (Vector2)transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                aimDirection = direction.normalized;
            }
        }

        private void HandleDashInput()
        {
            if (isDashing || Time.time < nextDashAt)
            {
                return;
            }

            if (!Dungeon2DInput.WasDashPressed())
            {
                return;
            }

            Vector2 direction = moveInput.sqrMagnitude > 0.001f ? moveInput.normalized : aimDirection;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            isDashing = true;
            dashDirection = direction;
            dashEndsAt = Time.time + DashDuration;
            nextDashAt = Time.time + DashCooldown;
            nextDashTrailAt = 0f;
            game.PlayDashFeedback(transform.position, direction);
            SpawnDashTrail();
        }

        private void SpawnDashTrail()
        {
            if (game == null || Time.time < nextDashTrailAt)
            {
                return;
            }

            nextDashTrailAt = Time.time + DashTrailInterval;
            Sprite sprite = spriteRenderer != null ? spriteRenderer.sprite : null;
            bool flipX = spriteRenderer != null && spriteRenderer.flipX;
            Vector3 trailScale = spriteRenderer != null ? spriteRenderer.transform.lossyScale : transform.localScale;
            game.SpawnPlayerDashTrail(transform.position, dashDirection, sprite, trailScale, flipX);
        }

        private void HandleCombatInput()
        {
            if (Dungeon2DInput.IsPrimaryFireHeld())
            {
                TryRangedAttack();
            }

            if (Dungeon2DInput.WasSecondaryFirePressed())
            {
                TryMeleeAttack();
            }
        }

        private void TryRangedAttack()
        {
            float interval = 1f / Mathf.Max(0.1f, game.PlayerStats.AttackRate);
            if (Time.time < nextRangedAt)
            {
                return;
            }

            nextRangedAt = Time.time + interval;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.PrimaryAttack);
            }

            Vector2 spawn = (Vector2)transform.position + aimDirection * 0.66f;
            game.SpawnProjectile(spawn, aimDirection, game.PlayerStats.ProjectileSpeed, game.PlayerStats.Damage, 4.2f, true, gameObject, 0.5f);
        }

        private void TryMeleeAttack()
        {
            if (Time.time < nextMeleeAt)
            {
                return;
            }

            nextMeleeAt = Time.time + MeleeCooldown;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.SecondaryAttack, aimDirection);
            }

            game.PerformPlayerMelee(transform.position, aimDirection, game.PlayerStats.Damage * 1.3f);
        }

        private void HandleConsumableInput()
        {
            if (Dungeon2DInput.WasPotionPressed())
            {
                if (game.Inventory.Potions <= 0)
                {
                    game.Notify("No health potion");
                }
                else
                {
                    game.Inventory.Potions -= 1;
                    Heal(35f);
                    game.Notify("Health potion used");
                    game.PlayPickupFeedback();
                }

                game.RefreshHud();
            }

            if (Dungeon2DInput.WasSpeedBoostPressed())
            {
                if (game.Inventory.SpeedBoosts <= 0)
                {
                    game.Notify("No speed boost");
                }
                else
                {
                    game.Inventory.SpeedBoosts -= 1;
                    speedBoostActive = true;
                    speedBoostEndsAt = Time.time + 6f;
                    game.Notify("Speed boost active");
                    game.PlayPickupFeedback();
                }

                game.RefreshHud();
            }
        }

        private void UpdateSpeedBoostTimer()
        {
            if (!speedBoostActive || Time.time < speedBoostEndsAt)
            {
                return;
            }

            speedBoostActive = false;
            game.Notify("Speed boost ended");
        }
    }
}
