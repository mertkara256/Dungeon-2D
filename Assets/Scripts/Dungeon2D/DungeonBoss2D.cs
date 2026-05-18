using System;
using System.Collections;
using UnityEngine;

namespace Dungeon2D
{
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class DungeonBoss2D : MonoBehaviour, IDamageable2D
    {
        private enum BossPhase2D
        {
            Phase1,
            Phase2,
            Phase3,
            Final
        }

        private Dungeon2DGame game;
        private DungeonRoom2D room;
        private SpriteRenderer spriteRenderer;
        private Dungeon2DVisualAnimator visualAnimator;
        private Dungeon2DWorldHealthBar healthBar;
        private CircleCollider2D bodyCollider;
        private Rigidbody2D body;
        private Transform player;
        private const float BaseMaxHealth = 480f;
        private const float BaseMoveSpeed = 3.4f;
        private float maxHealth = BaseMaxHealth;
        private float currentHealth;
        private float moveSpeed = BaseMoveSpeed;
        private bool busy;
        private bool vulnerable;
        private bool alternateTripleVolley;
        private Vector2 desiredVelocity;
        private float nextMeleeAt;
        private float nextSingleAt;
        private float nextTripleAt;
        private float nextDashAt;
        private float nextMinionsAt;
        private float nextAreaAt;
        private float nextFinalComboAt;
        private BossPhase2D currentPhase = BossPhase2D.Phase1;
        private Coroutine actionRoutine;

        public event Action<DungeonBoss2D> BossDefeated;

        public bool IsDead { get; private set; }

        public float HealthRatio
        {
            get { return maxHealth <= 0.01f ? 0f : Mathf.Clamp01(currentHealth / maxHealth); }
        }

        public void Configure(Dungeon2DGame owner, DungeonRoom2D sourceRoom, int difficultyLevel)
        {
            if (owner == null || sourceRoom == null)
            {
                enabled = false;
                return;
            }

            game = owner;
            room = sourceRoom;
            player = owner.Player != null ? owner.Player.transform : null;
            maxHealth = BaseMaxHealth;
            moveSpeed = BaseMoveSpeed;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            game.Art.Apply(spriteRenderer, CharacterSpriteId.Boss, 19);
            transform.localScale = Vector3.one * 1.55f;

            bodyCollider = GetComponent<CircleCollider2D>();
            bodyCollider.radius = 0.9f;
            bodyCollider.offset = new Vector2(0f, -0.25f);
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

            visualAnimator = GetComponent<Dungeon2DVisualAnimator>();
            if (visualAnimator == null)
            {
                visualAnimator = gameObject.AddComponent<Dungeon2DVisualAnimator>();
            }

            visualAnimator.Configure(
                spriteRenderer,
                game.Art.GetCharacterAnimation(CharacterSpriteId.Boss, DungeonSpriteAnimation.Idle),
                game.Art.GetCharacterAnimation(CharacterSpriteId.Boss, DungeonSpriteAnimation.Walk),
                game.Art.GetCharacterAnimation(CharacterSpriteId.Boss, DungeonSpriteAnimation.PrimaryAttack),
                game.Art.GetCharacterAnimation(CharacterSpriteId.Boss, DungeonSpriteAnimation.SecondaryAttack));
            spriteRenderer = visualAnimator.Renderer;

            healthBar = GetComponent<Dungeon2DWorldHealthBar>();
            if (healthBar == null)
            {
                healthBar = gameObject.AddComponent<Dungeon2DWorldHealthBar>();
            }

            healthBar.Configure(2.35f, new Vector2(0f, 2.12f), new Color(0.78f, 0.15f, 0.95f, 1f), 37);

            float safeDifficulty = Mathf.Max(0, difficultyLevel);
            maxHealth *= 1f + safeDifficulty * 0.06f;
            moveSpeed *= 1f + safeDifficulty * 0.02f;
            currentHealth = maxHealth;
            healthBar.SetRatio(HealthRatio);
            currentPhase = BossPhase2D.Phase1;
            IsDead = false;
            game.Notify("Corrupted Guardian awakened");
            game.PlayBossFeedback();
            game.RefreshHud();
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
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            EvaluatePhase();
            FacePlayer();
            desiredVelocity = Vector2.zero;

            if (busy || vulnerable)
            {
                if (visualAnimator != null)
                {
                    visualAnimator.IsMoving = false;
                }

                desiredVelocity = Vector2.zero;
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                }

                return;
            }

            RunPhaseLogic();
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            if (game == null || game.State != DungeonGameState.Playing || IsDead || vulnerable)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            if (busy)
            {
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

            currentHealth = Mathf.Clamp(currentHealth - Mathf.Max(1f, amount), 0f, maxHealth);
            if (healthBar != null)
            {
                healthBar.SetRatio(HealthRatio);
            }

            if (visualAnimator != null)
            {
                visualAnimator.PlayDamageFeedback();
            }

            game.RefreshHud();

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void RunPhaseLogic()
        {
            float distance = Vector2.Distance(transform.position, player.position);
            float cadence = GetCadenceMultiplier();

            switch (currentPhase)
            {
                case BossPhase2D.Phase1:
                    if (distance <= 3.6f && CanUse(ref nextMeleeAt, 2.8f, cadence))
                    {
                        BeginAction(MeleeSlamRoutine(cadence));
                    }
                    else
                    {
                        MoveTowardsPlayer(1f);
                    }
                    break;
                case BossPhase2D.Phase2:
                    if (distance <= 3.6f && CanUse(ref nextMeleeAt, 2.8f, cadence))
                    {
                        BeginAction(MeleeSlamRoutine(cadence));
                    }
                    else if (alternateTripleVolley && CanUse(ref nextTripleAt, 5.4f, cadence))
                    {
                        alternateTripleVolley = false;
                        BeginAction(ProjectileRoutine(true, cadence));
                    }
                    else if (CanUse(ref nextSingleAt, 3.2f, cadence))
                    {
                        alternateTripleVolley = true;
                        BeginAction(ProjectileRoutine(false, cadence));
                    }
                    else
                    {
                        MoveTowardsPlayer(1f);
                    }
                    break;
                case BossPhase2D.Phase3:
                    if (CanUse(ref nextMinionsAt, 8f, cadence))
                    {
                        BeginAction(MinionRoutine(cadence));
                    }
                    else if (CanUse(ref nextAreaAt, 6.6f, cadence))
                    {
                        BeginAction(AreaRoutine(cadence));
                    }
                    else if (distance > 4f && CanUse(ref nextDashAt, 4.4f, cadence))
                    {
                        BeginAction(DashRoutine(cadence));
                    }
                    else if (CanUse(ref nextTripleAt, 5.4f, cadence))
                    {
                        BeginAction(ProjectileRoutine(true, cadence));
                    }
                    else if (distance <= 3.6f && CanUse(ref nextMeleeAt, 2.8f, cadence))
                    {
                        BeginAction(MeleeSlamRoutine(cadence));
                    }
                    else
                    {
                        MoveTowardsPlayer(1f);
                    }
                    break;
                default:
                    if (Time.time >= nextFinalComboAt)
                    {
                        nextFinalComboAt = Time.time + 6f / cadence;
                        BeginAction(FinalComboRoutine(cadence));
                    }
                    else if (distance <= 3.6f && CanUse(ref nextMeleeAt, 2.8f, cadence))
                    {
                        BeginAction(FinalSingleRoutine(MeleeSlamRoutine(cadence)));
                    }
                    else if (distance > 3.1f && CanUse(ref nextDashAt, 4.4f, cadence))
                    {
                        BeginAction(FinalSingleRoutine(DashRoutine(cadence)));
                    }
                    else if (CanUse(ref nextTripleAt, 5.4f, cadence))
                    {
                        BeginAction(FinalSingleRoutine(ProjectileRoutine(true, cadence)));
                    }
                    else
                    {
                        MoveTowardsPlayer(1.1f);
                    }
                    break;
            }
        }

        private IEnumerator MeleeSlamRoutine(float cadence)
        {
            busy = true;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.PrimaryAttack);
            }

            GameObject telegraph = game.SpawnTelegraph(transform.position, 4.8f, new Color(1f, 0.2f, 0.16f, 0.72f), 0.62f);
            float windup = 0.85f / Mathf.Max(0.75f, cadence);
            yield return new WaitForSeconds(windup);

            if (telegraph != null)
            {
                Destroy(telegraph);
            }

            if (!game.Player.IsDead && Vector2.Distance(transform.position, game.Player.transform.position) <= 4.8f)
            {
                game.Player.TakeDamage(24f, gameObject);
            }

            yield return new WaitForSeconds(0.32f / Mathf.Max(0.75f, cadence));
            busy = false;
        }

        private IEnumerator ProjectileRoutine(bool triple, float cadence)
        {
            busy = true;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.PrimaryAttack);
            }

            yield return new WaitForSeconds((triple ? 0.36f : 0.28f) / Mathf.Max(0.75f, cadence));
            Vector2 direction = ((Vector2)player.position - (Vector2)transform.position).normalized;
            if (triple)
            {
                game.SpawnEnemySpreadProjectile(transform.position, direction, 19f, 14f, 3f, gameObject, 16f, true);
            }
            else
            {
                game.SpawnProjectile(transform.position, direction, 19f, 14f, 3f, false, gameObject, 0.58f, true);
            }

            busy = false;
        }

        private IEnumerator DashRoutine(float cadence)
        {
            busy = true;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.SecondaryAttack);
            }

            float windup = 0.2f / Mathf.Max(0.8f, cadence);
            yield return new WaitForSeconds(windup);

            Vector2 start = transform.position;
            Vector2 target = player.position;
            Vector2 direction = (target - start).sqrMagnitude > 0.01f ? (target - start).normalized : Vector2.up;
            Vector2 end = ClampInsideRoom(start + direction * 8f);
            bool damaged = false;
            float duration = 0.4f / Mathf.Max(0.8f, cadence);
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                MoveBody(Vector2.Lerp(start, end, Mathf.Clamp01(timer / duration)));
                if (!damaged && !game.Player.IsDead && Vector2.Distance(transform.position, game.Player.transform.position) <= 1.45f)
                {
                    damaged = true;
                    game.Player.TakeDamage(20f, gameObject);
                }

                yield return null;
            }

            busy = false;
        }

        private IEnumerator MinionRoutine(float cadence)
        {
            busy = true;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.PrimaryAttack);
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 4.5f);
                Vector2 position = ClampInsideRoom((Vector2)transform.position + offset);
                game.SpawnEnemy(room, EnemyKind.Minion, position);
                yield return new WaitForSeconds(0.08f / Mathf.Max(0.75f, cadence));
            }

            busy = false;
        }

        private IEnumerator AreaRoutine(float cadence)
        {
            busy = true;
            if (visualAnimator != null)
            {
                visualAnimator.PlayAttack(DungeonSpriteAnimation.SecondaryAttack);
            }

            Vector2[] positions = new Vector2[2];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = ClampInsideRoom((Vector2)player.position + UnityEngine.Random.insideUnitCircle * 2.4f);
                game.SpawnTelegraph(positions[i], 2.2f, new Color(1f, 0.18f, 0.12f, 0.72f), 0.65f);
            }

            yield return new WaitForSeconds(0.65f / Mathf.Max(0.8f, cadence));

            for (int i = 0; i < positions.Length; i++)
            {
                game.SpawnHazard(positions[i], 2.2f, 11f, 0.55f, 5f);
            }

            busy = false;
        }

        private IEnumerator FinalComboRoutine(float cadence)
        {
            yield return MeleeSlamRoutine(cadence);
            yield return VulnerableWindowRoutine(1.2f);
            yield return DashRoutine(cadence);
            yield return VulnerableWindowRoutine(1.2f);
            yield return ProjectileRoutine(true, cadence);
            yield return VulnerableWindowRoutine(1.4f);
        }

        private IEnumerator FinalSingleRoutine(IEnumerator attack)
        {
            yield return attack;
            yield return VulnerableWindowRoutine(1.2f);
        }

        private IEnumerator VulnerableWindowRoutine(float duration)
        {
            vulnerable = true;
            game.Notify("Corrupted Guardian staggered");
            yield return new WaitForSeconds(duration);
            vulnerable = false;
        }

        private void BeginAction(IEnumerator routine)
        {
            if (routine == null || actionRoutine != null)
            {
                return;
            }

            actionRoutine = StartCoroutine(TrackAction(routine));
        }

        private IEnumerator TrackAction(IEnumerator routine)
        {
            yield return routine;
            busy = false;
            actionRoutine = null;
        }

        private bool CanUse(ref float nextTime, float cooldown, float cadence)
        {
            if (Time.time < nextTime)
            {
                return false;
            }

            nextTime = Time.time + Mathf.Max(0.25f, cooldown / Mathf.Max(0.5f, cadence));
            return true;
        }

        private void MoveTowardsPlayer(float multiplier)
        {
            Vector2 current = transform.position;
            Vector2 target = player.position;
            if (Vector2.Distance(current, target) <= 2.2f)
            {
                if (visualAnimator != null)
                {
                    visualAnimator.IsMoving = false;
                }

                return;
            }

            Vector2 direction = target - current;
            desiredVelocity = direction.sqrMagnitude > 0.001f
                ? direction.normalized * moveSpeed * multiplier
                : Vector2.zero;
            Vector2 next = ClampInsideRoom(current + desiredVelocity * Time.deltaTime);
            if (Vector2.Distance(next, current) < 0.001f)
            {
                desiredVelocity = Vector2.zero;
            }

            if (visualAnimator != null)
            {
                visualAnimator.IsMoving = desiredVelocity.sqrMagnitude > 0.001f;
            }
        }

        private void MoveBody(Vector2 position)
        {
            desiredVelocity = Vector2.zero;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.MovePosition(position);
            }
            else
            {
                transform.position = position;
            }
        }

        private Vector2 ClampInsideRoom(Vector2 position)
        {
            Rect bounds = room.Bounds;
            return new Vector2(
                Mathf.Clamp(position.x, bounds.xMin + 1.6f, bounds.xMax - 1.6f),
                Mathf.Clamp(position.y, bounds.yMin + 1.6f, bounds.yMax - 1.6f));
        }

        private void FacePlayer()
        {
            if (visualAnimator != null && player != null)
            {
                visualAnimator.SetFacing(player.position.x - transform.position.x);
            }
        }

        private void EvaluatePhase()
        {
            BossPhase2D nextPhase;
            float ratio = HealthRatio;
            if (ratio <= 0.15f)
            {
                nextPhase = BossPhase2D.Final;
            }
            else if (ratio <= 0.4f)
            {
                nextPhase = BossPhase2D.Phase3;
            }
            else if (ratio <= 0.7f)
            {
                nextPhase = BossPhase2D.Phase2;
            }
            else
            {
                nextPhase = BossPhase2D.Phase1;
            }

            if (nextPhase == currentPhase)
            {
                return;
            }

            currentPhase = nextPhase;
            switch (currentPhase)
            {
                case BossPhase2D.Phase2:
                    game.Notify("Corrupted Guardian entered Phase 2");
                    break;
                case BossPhase2D.Phase3:
                    game.Notify("Corrupted Guardian entered Phase 3");
                    break;
                case BossPhase2D.Final:
                    game.Notify("Corrupted Guardian is enraged");
                    break;
            }
        }

        private float GetCadenceMultiplier()
        {
            switch (currentPhase)
            {
                case BossPhase2D.Phase2:
                    return 1.12f;
                case BossPhase2D.Phase3:
                    return 1.22f;
                case BossPhase2D.Final:
                    return 1.35f;
                default:
                    return 1f;
            }
        }

        private void Die()
        {
            IsDead = true;
            StopAllCoroutines();
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
                visualAnimator.PlayDeath(1.1f);
            }

            game.Notify("Corrupted Guardian defeated");
            game.PlayRewardFeedback();
            BossDefeated?.Invoke(this);
            Destroy(gameObject, 1.25f);
        }
    }
}
