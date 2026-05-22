using System;
using UnityEngine;

namespace Dungeon2D
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DungeonEnemy2D : MonoBehaviour, IDamageable2D
    {
        private Dungeon2DGame game;
        private DungeonRoom2D room;
        private EnemyKind kind;
        private SpriteRenderer spriteRenderer;
        private Dungeon2DVisualAnimator visualAnimator;
        private Dungeon2DWorldHealthBar healthBar;
        private CircleCollider2D bodyCollider;
        private Rigidbody2D body;
        private Transform player;
        private float maxHealth;
        private float currentHealth;
        private float moveSpeed;
        private float damage;
        private float detectionRange;
        private float attackRange;
        private float attackCooldown;
        private float projectileSpeed;
        private float projectileLifetime;
        private int scoreReward;
        private bool ranged;
        private float nextAttackAt;
        private Vector2 patrolTarget;
        private Vector2 desiredVelocity;
        private bool hasPatrolTarget;
        private bool invertFacing;

        public event Action<DungeonEnemy2D> Died;

        public bool IsDead { get; private set; }

        public EnemyKind Kind
        {
            get { return kind; }
        }

        public DungeonRoom2D Room
        {
            get { return room; }
        }

        public int ScoreReward
        {
            get { return scoreReward; }
        }

        public float HealthRatio
        {
            get { return maxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / maxHealth); }
        }

        public void Configure(Dungeon2DGame owner, DungeonRoom2D sourceRoom, EnemyKind enemyKind, int difficultyLevel)
        {
            if (owner == null || sourceRoom == null)
            {
                enabled = false;
                return;
            }

            game = owner;
            room = sourceRoom;
            kind = enemyKind;
            invertFacing = UsesInvertedFacing(enemyKind);
            player = owner.Player != null ? owner.Player.transform : null;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            bodyCollider = GetComponent<CircleCollider2D>();
            bodyCollider.radius = 0.46f;
            bodyCollider.offset = new Vector2(0f, -0.12f);
            bodyCollider.sharedMaterial = Dungeon2DPhysics.Material;

            body = GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            ApplyProfile(enemyKind);
            ApplyDifficulty(difficultyLevel);
            currentHealth = maxHealth;
            IsDead = false;

            Dungeon2DArtLibrary art = game.Art;
            art.Apply(spriteRenderer, SpriteForKind(enemyKind), 18);
            transform.localScale = Vector3.one * ScaleForKind(enemyKind);

            visualAnimator = GetComponent<Dungeon2DVisualAnimator>();
            if (visualAnimator == null)
            {
                visualAnimator = gameObject.AddComponent<Dungeon2DVisualAnimator>();
            }

            CharacterSpriteId spriteId = SpriteForKind(enemyKind);
            visualAnimator.Configure(
                spriteRenderer,
                game.Art.GetCharacterAnimation(spriteId, DungeonSpriteAnimation.Idle),
                game.Art.GetCharacterAnimation(spriteId, DungeonSpriteAnimation.Walk),
                game.Art.GetCharacterAnimation(spriteId, DungeonSpriteAnimation.PrimaryAttack),
                game.Art.GetCharacterAnimation(spriteId, DungeonSpriteAnimation.SecondaryAttack));
            visualAnimator.SetBaseVisualScale(Dungeon2DConfig.CharacterVisualScale);
            spriteRenderer = visualAnimator.Renderer;

            healthBar = GetComponent<Dungeon2DWorldHealthBar>();
            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<Dungeon2DWorldHealthBar>();
            }

            float barWidth = enemyKind == EnemyKind.Tank ? 1.25f : enemyKind == EnemyKind.Minion ? 0.72f : 1f;
            float barHeight = enemyKind == EnemyKind.Tank ? 1.42f : enemyKind == EnemyKind.Minion ? 0.78f : 1.1f;
            healthBar.Configure(barWidth, new Vector2(0f, barHeight * Dungeon2DConfig.CharacterVisualScale), new Color(0.95f, 0.23f, 0.18f, 1f), 36);
            healthBar.SetRatio(HealthRatio);
        }

        private void Update()
        {
            if (game == null || game.State != DungeonGameState.Playing || IsDead || player == null || game.Player.IsDead)
            {
                if (visualAnimator != null)
                {
                    visualAnimator.IsMoving = false;
                }

                desiredVelocity = Vector2.zero;

                return;
            }

            desiredVelocity = Vector2.zero;
            Vector2 current = transform.position;
            Vector2 target = player.position;
            float distance = Vector2.Distance(current, target);

            if (distance <= detectionRange)
            {
                if (distance <= attackRange)
                {
                    TryAttack(target);
                }
                else
                {
                    MoveTowards(target, 1f);
                }
            }
            else
            {
                Patrol();
            }

            if (visualAnimator != null)
            {
                visualAnimator.IsMoving = desiredVelocity.sqrMagnitude > 0.001f;
                float horizontalFacing = target.x - current.x;
                visualAnimator.SetFacing(invertFacing ? -horizontalFacing : horizontalFacing);
            }
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            if (game == null || game.State != DungeonGameState.Playing || IsDead)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            body.linearVelocity = desiredVelocity;
        }

        public void TakeDamage(float amount, GameObject instigator)
        {
            if (IsDead)
            {
                return;
            }

            currentHealth -= Mathf.Max(1f, amount);
            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }

            if (visualAnimator != null)
            {
                visualAnimator.PlayDamageFeedback();
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void TryAttack(Vector2 target)
        {
            if (Time.time < nextAttackAt)
            {
                if (!ranged)
                {
                    MoveTowards(target, 0.35f);
                }

                return;
            }

            nextAttackAt = Time.time + attackCooldown;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.PrimaryAttack);
            }

            Vector2 direction = (target - (Vector2)transform.position).normalized;
            if (ranged)
            {
                if (kind == EnemyKind.EliteRanged)
                {
                    game.SpawnEnemySpreadProjectile(transform.position, direction, projectileSpeed, damage, projectileLifetime, gameObject, 11f);
                }
                else
                {
                    game.SpawnProjectile(transform.position, direction, projectileSpeed, damage, projectileLifetime, false, gameObject, 0.42f);
                }
            }
            else if (game.Player != null && Vector2.Distance(transform.position, game.Player.transform.position) <= attackRange + 0.25f)
            {
                game.Player.TakeDamage(damage, gameObject);
            }
        }

        private void MoveTowards(Vector2 target, float speedMultiplier)
        {
            Vector2 current = transform.position;
            Vector2 direction = target - current;
            if (direction.sqrMagnitude < 0.001f)
            {
                desiredVelocity = Vector2.zero;
                return;
            }

            desiredVelocity = direction.normalized * moveSpeed * speedMultiplier;
            Vector2 next = ClampInsideRoom(current + desiredVelocity * Time.deltaTime);
            if (Vector2.Distance(next, current) < 0.001f)
            {
                desiredVelocity = Vector2.zero;
            }

            if (visualAnimator != null)
            {
                visualAnimator.IsMoving = true;
            }
        }

        private void Patrol()
        {
            if (!hasPatrolTarget || Vector2.Distance(transform.position, patrolTarget) < 0.35f)
            {
                Rect bounds = room.Bounds;
                patrolTarget = new Vector2(
                    UnityEngine.Random.Range(bounds.xMin + 2f, bounds.xMax - 2f),
                    UnityEngine.Random.Range(bounds.yMin + 2f, bounds.yMax - 2f));
                hasPatrolTarget = true;
            }

            MoveTowards(patrolTarget, 0.55f);
        }

        private Vector2 ClampInsideRoom(Vector2 position)
        {
            Rect bounds = room.Bounds;
            return new Vector2(
                Mathf.Clamp(position.x, bounds.xMin + 1.15f, bounds.xMax - 1.15f),
                Mathf.Clamp(position.y, bounds.yMin + 1.15f, bounds.yMax - 1.15f));
        }

        private void Die()
        {
            IsDead = true;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (healthBar != null)
            {
                healthBar.SetRatio(0f);
            }

            game.AddScore(scoreReward);
            game.PlayHitFeedback();

            if (visualAnimator != null)
            {
                visualAnimator.PlayDeath(0.35f);
            }

            Died?.Invoke(this);
            Destroy(gameObject, 0.42f);
        }

        private void ApplyProfile(EnemyKind enemyKind)
        {
            projectileLifetime = 2f;

            switch (enemyKind)
            {
                case EnemyKind.Ranged:
                    maxHealth = 30f;
                    moveSpeed = 2.8f;
                    damage = 9f;
                    detectionRange = 17f;
                    attackRange = 8.5f;
                    attackCooldown = 1.45f;
                    projectileSpeed = 18f;
                    scoreReward = 15;
                    ranged = true;
                    break;
                case EnemyKind.Tank:
                    maxHealth = 85f;
                    moveSpeed = 2f;
                    damage = 18f;
                    detectionRange = 12f;
                    attackRange = 2.3f;
                    attackCooldown = 1.4f;
                    projectileSpeed = 0f;
                    scoreReward = 22;
                    ranged = false;
                    break;
                case EnemyKind.EliteRanged:
                    maxHealth = 58f;
                    moveSpeed = 2.6f;
                    damage = 10f;
                    detectionRange = 24f;
                    attackRange = 16f;
                    attackCooldown = 0.82f;
                    projectileSpeed = 22f;
                    scoreReward = 24;
                    ranged = true;
                    break;
                case EnemyKind.Minion:
                    maxHealth = 1f;
                    moveSpeed = 7.4f;
                    damage = 4f;
                    detectionRange = 20f;
                    attackRange = 1.25f;
                    attackCooldown = 0.7f;
                    projectileSpeed = 0f;
                    scoreReward = 4;
                    ranged = false;
                    break;
                default:
                    maxHealth = 42f;
                    moveSpeed = 3.6f;
                    damage = 11f;
                    detectionRange = 13f;
                    attackRange = 1.8f;
                    attackCooldown = 1.1f;
                    projectileSpeed = 0f;
                    scoreReward = 12;
                    ranged = false;
                    break;
            }
        }

        private void ApplyDifficulty(int difficultyLevel)
        {
            if (kind == EnemyKind.Minion)
            {
                return;
            }

            float safeDifficulty = Mathf.Max(0, difficultyLevel);
            float roomMultiplier = 1f + safeDifficulty * 0.08f;
            maxHealth *= roomMultiplier;
            damage *= 1f + safeDifficulty * 0.05f;
            moveSpeed *= 1f + safeDifficulty * 0.01f;
            scoreReward = Mathf.RoundToInt(scoreReward * roomMultiplier);
        }

        private static CharacterSpriteId SpriteForKind(EnemyKind enemyKind)
        {
            switch (enemyKind)
            {
                case EnemyKind.Ranged:
                    return CharacterSpriteId.RangedEnemy;
                case EnemyKind.Tank:
                    return CharacterSpriteId.TankEnemy;
                case EnemyKind.EliteRanged:
                    return CharacterSpriteId.EliteRangedEnemy;
                case EnemyKind.Minion:
                    return CharacterSpriteId.Minion;
                default:
                    return CharacterSpriteId.MeleeEnemy;
            }
        }

        private static float ScaleForKind(EnemyKind enemyKind)
        {
            switch (enemyKind)
            {
                case EnemyKind.Tank:
                    return 1.05f;
                case EnemyKind.EliteRanged:
                    return 1f;
                case EnemyKind.Minion:
                    return 0.72f;
                default:
                    return 0.9f;
            }
        }

        private static bool UsesInvertedFacing(EnemyKind enemyKind)
        {
            return enemyKind == EnemyKind.Melee || enemyKind == EnemyKind.Ranged;
        }
    }
}
