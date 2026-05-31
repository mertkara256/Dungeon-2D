using System.Collections;
using UnityEngine;

namespace Dungeon2D
{
    public sealed class Dungeon2DVisualAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private SpriteRenderer meleeWeaponRenderer;
        [SerializeField] private float frameRate = 9f;
        [SerializeField] private float meleeWeaponScale = 0.68f;
        [SerializeField] private Color damageFlashColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private float damageFlashDuration = 0.14f;
        [SerializeField] private float damageShakeDistance = 0.08f;
        [SerializeField] private float damageShakeFrequency = 34f;

        private Transform visualTransform;
        private Transform meleeWeaponTransform;
        private Vector3 baseLocalPosition;
        private Vector3 baseLocalScale;
        private Vector3 loopMotionOffset;
        private Vector3 hitShakeOffset;
        private Vector2 activeAttackDirection = Vector2.right;
        private Color baseColor = Color.white;
        private Sprite[] idleFrames;
        private Sprite[] walkFrames;
        private Sprite[] primaryAttackFrames;
        private Sprite[] secondaryAttackFrames;
        private readonly Sprite[] fallbackLoopFrame = new Sprite[1];
        private Coroutine flashRoutine;
        private Coroutine attackRoutine;
        private Coroutine deathRoutine;
        private DungeonSpriteAnimation currentLoop = DungeonSpriteAnimation.Idle;
        private float frameTimer;
        private int frameIndex;
        private bool attackPlaying;

        public bool IsMoving { get; set; }
        public bool IsDead { get; private set; }
        public SpriteRenderer Renderer
        {
            get { return targetRenderer; }
        }

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            EnsureVisualChild();
            CaptureBasePose();
        }

        public void Configure(
            SpriteRenderer renderer,
            Sprite[] idle,
            Sprite[] walk,
            Sprite[] primaryAttack,
            Sprite[] secondaryAttack)
        {
            if (renderer != null && (targetRenderer == null || targetRenderer.transform == transform))
            {
                targetRenderer = renderer;
            }

            EnsureVisualChild();
            idleFrames = idle;
            walkFrames = walk;
            primaryAttackFrames = primaryAttack;
            secondaryAttackFrames = secondaryAttack;
            CaptureBasePose();
            ApplyFrame(GetActiveLoopFrames(), 0);
            RefreshMeleeWeaponSorting();
        }

        private void CaptureBasePose()
        {
            if (visualTransform == null)
            {
                visualTransform = targetRenderer != null ? targetRenderer.transform : transform;
            }

            baseLocalPosition = visualTransform.localPosition;
            baseLocalScale = visualTransform.localScale;
            loopMotionOffset = Vector3.zero;
            hitShakeOffset = Vector3.zero;
            if (targetRenderer != null)
            {
                baseColor = targetRenderer.color;
            }
        }

        private void Update()
        {
            if (IsDead)
            {
                return;
            }

            if (!attackPlaying)
            {
                DungeonSpriteAnimation desiredLoop = IsMoving ? DungeonSpriteAnimation.Walk : DungeonSpriteAnimation.Idle;
                if (desiredLoop != currentLoop)
                {
                    currentLoop = desiredLoop;
                    frameIndex = 0;
                    frameTimer = 0f;
                }

                AdvanceLoop(GetActiveLoopFrames());
            }
        }

        public void SetFacing(float horizontal)
        {
            if (targetRenderer == null || Mathf.Abs(horizontal) < 0.02f)
            {
                return;
            }

            targetRenderer.flipX = horizontal < 0f;
        }

        public void PlayAttack(DungeonSpriteAnimation attackAnimation = DungeonSpriteAnimation.PrimaryAttack)
        {
            Vector2 facingDirection = targetRenderer != null && targetRenderer.flipX ? Vector2.left : Vector2.right;
            PlayAttack(attackAnimation, facingDirection);
        }

        public void PlayAttack(DungeonSpriteAnimation attackAnimation, Vector2 attackDirection)
        {
            if (IsDead)
            {
                return;
            }

            activeAttackDirection = attackDirection.sqrMagnitude > 0.001f ? attackDirection.normalized : (targetRenderer != null && targetRenderer.flipX ? Vector2.left : Vector2.right);
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                HideMeleeWeapon();
            }

            attackRoutine = StartCoroutine(AttackRoutine(attackAnimation));
        }

        public void ConfigureMeleeWeapon(Sprite weaponSprite)
        {
            if (weaponSprite == null)
            {
                HideMeleeWeapon();
                return;
            }

            EnsureMeleeWeaponChild();
            meleeWeaponRenderer.sprite = weaponSprite;
            meleeWeaponRenderer.color = Color.white;
            meleeWeaponRenderer.enabled = false;
            RefreshMeleeWeaponSorting();
        }

        public void SetBaseVisualScale(float scale)
        {
            if (visualTransform == null)
            {
                visualTransform = targetRenderer != null ? targetRenderer.transform : transform;
            }

            visualTransform.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            CaptureBasePose();
            ApplyVisualPosition();
        }

        public void PlayHit(Color flashColor)
        {
            PlayHit(flashColor, 0.08f, 0f, damageShakeFrequency);
        }

        public void PlayDamageFeedback()
        {
            PlayHit(damageFlashColor, damageFlashDuration, damageShakeDistance, damageShakeFrequency);
        }

        public void PlayHit(Color flashColor, float duration, float shakeDistance, float shakeFrequency)
        {
            if (IsDead || targetRenderer == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(HitReactionRoutine(flashColor, duration, shakeDistance, shakeFrequency));
        }

        public void PlayDeath(float duration)
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            hitShakeOffset = Vector3.zero;
            ApplyVisualPosition();
            HideMeleeWeapon();

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
            }

            IsDead = true;
            deathRoutine = StartCoroutine(DeathRoutine(Mathf.Max(0.1f, duration)));
        }

        private IEnumerator AttackRoutine(DungeonSpriteAnimation attackAnimation)
        {
            attackPlaying = true;
            Sprite[] frames = attackAnimation == DungeonSpriteAnimation.SecondaryAttack ? secondaryAttackFrames : primaryAttackFrames;
            if (frames == null || frames.Length == 0)
            {
                frames = GetActiveLoopFrames();
            }

            float secondsPerFrame = 1f / Mathf.Max(1f, frameRate * 1.7f);
            float totalDuration = secondsPerFrame * frames.Length;
            float timer = 0f;
            int appliedFrame = -1;
            bool swingMeleeWeapon = attackAnimation == DungeonSpriteAnimation.SecondaryAttack && meleeWeaponRenderer != null && meleeWeaponRenderer.sprite != null;

            if (swingMeleeWeapon)
            {
                meleeWeaponRenderer.enabled = true;
                UpdateMeleeWeaponPose(0f);
            }

            while (timer < totalDuration)
            {
                float t = Mathf.Clamp01(timer / totalDuration);
                int frame = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(timer / secondsPerFrame));
                if (frame != appliedFrame)
                {
                    ApplyFrame(frames, frame);
                    appliedFrame = frame;
                }

                float punch = Mathf.Sin(t * Mathf.PI) * 0.16f;
                visualTransform.localScale = new Vector3(baseLocalScale.x * (1f + punch), baseLocalScale.y * (1f - punch * 0.25f), baseLocalScale.z);
                if (swingMeleeWeapon)
                {
                    UpdateMeleeWeaponPose(t);
                }

                timer += Time.deltaTime;
                yield return null;
            }

            if (swingMeleeWeapon)
            {
                UpdateMeleeWeaponPose(1f);
                HideMeleeWeapon();
            }

            visualTransform.localScale = baseLocalScale;
            attackPlaying = false;
            currentLoop = IsMoving ? DungeonSpriteAnimation.Walk : DungeonSpriteAnimation.Idle;
            ApplyFrame(GetActiveLoopFrames(), 0);
            attackRoutine = null;
        }

        private IEnumerator HitReactionRoutine(Color flashColor, float duration, float shakeDistance, float shakeFrequency)
        {
            float safeDuration = Mathf.Max(0.01f, duration);
            float safeShakeDistance = Mathf.Max(0f, shakeDistance);
            float safeShakeFrequency = Mathf.Max(1f, shakeFrequency);
            float timer = 0f;

            targetRenderer.color = flashColor;

            while (timer < safeDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / safeDuration);
                float damping = 1f - t;
                float wave = Mathf.Sin(timer * Mathf.PI * 2f * safeShakeFrequency);
                float liftWave = Mathf.Sin(timer * Mathf.PI * safeShakeFrequency);
                hitShakeOffset = new Vector3(
                    wave * safeShakeDistance * damping,
                    liftWave * safeShakeDistance * 0.25f * damping,
                    0f);
                ApplyVisualPosition();

                if (targetRenderer != null)
                {
                    targetRenderer.color = Color.Lerp(flashColor, baseColor, t);
                }

                yield return null;
            }

            hitShakeOffset = Vector3.zero;
            ApplyVisualPosition();
            targetRenderer.color = baseColor;
            flashRoutine = null;
        }

        private IEnumerator DeathRoutine(float duration)
        {
            Vector3 startScale = visualTransform.localScale;
            Quaternion startRotation = visualTransform.localRotation;
            Color startColor = targetRenderer != null ? targetRenderer.color : Color.white;
            float side = Random.value < 0.5f ? -1f : 1f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                visualTransform.localRotation = Quaternion.Slerp(startRotation, Quaternion.Euler(0f, 0f, side * 82f), t);
                visualTransform.localScale = Vector3.Lerp(startScale, startScale * 0.72f, t);

                if (targetRenderer != null)
                {
                    Color color = startColor;
                    color.a = Mathf.Lerp(startColor.a, 0f, t);
                    targetRenderer.color = color;
                }

                yield return null;
            }
        }

        private void AdvanceLoop(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            frameTimer += Time.deltaTime;
            float secondsPerFrame = 1f / Mathf.Max(1f, frameRate);
            if (frameTimer < secondsPerFrame)
            {
                return;
            }

            frameTimer -= secondsPerFrame;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyFrame(frames, frameIndex);

            float phase = frameIndex / (float)Mathf.Max(1, frames.Length);
            float bob = currentLoop == DungeonSpriteAnimation.Walk
                ? Mathf.Sin(phase * Mathf.PI * 2f) * 0.045f
                : Mathf.Sin(phase * Mathf.PI * 2f) * 0.018f;
            loopMotionOffset = Vector3.up * bob;
            ApplyVisualPosition();
        }

        private void ApplyVisualPosition()
        {
            if (visualTransform == null)
            {
                return;
            }

            visualTransform.localPosition = baseLocalPosition + loopMotionOffset + hitShakeOffset;
        }

        private Sprite[] GetActiveLoopFrames()
        {
            if (currentLoop == DungeonSpriteAnimation.Walk && walkFrames != null && walkFrames.Length > 0)
            {
                return walkFrames;
            }

            if (idleFrames != null && idleFrames.Length > 0)
            {
                return idleFrames;
            }

            fallbackLoopFrame[0] = targetRenderer != null ? targetRenderer.sprite : null;
            return fallbackLoopFrame;
        }

        private void ApplyFrame(Sprite[] frames, int index)
        {
            if (targetRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            if (frame != null)
            {
                targetRenderer.sprite = frame;
            }
        }

        private void EnsureVisualChild()
        {
            if (targetRenderer == null)
            {
                return;
            }

            if (targetRenderer.transform != transform)
            {
                visualTransform = targetRenderer.transform;
                return;
            }

            GameObject visualObject = new GameObject("SpriteVisual");
            visualObject.transform.SetParent(transform, false);
            visualTransform = visualObject.transform;
            SpriteRenderer childRenderer = visualObject.AddComponent<SpriteRenderer>();
            CopyRenderer(targetRenderer, childRenderer);
            targetRenderer.enabled = false;
            targetRenderer = childRenderer;
        }

        private void EnsureMeleeWeaponChild()
        {
            if (meleeWeaponRenderer != null)
            {
                meleeWeaponTransform = meleeWeaponRenderer.transform;
                Transform desiredParent = visualTransform != null ? visualTransform : transform;
                if (meleeWeaponTransform.parent != desiredParent)
                {
                    meleeWeaponTransform.SetParent(desiredParent, false);
                }

                return;
            }

            GameObject weaponObject = new GameObject("MeleeSwordVisual");
            weaponObject.transform.SetParent(visualTransform != null ? visualTransform : transform, false);
            meleeWeaponTransform = weaponObject.transform;
            meleeWeaponRenderer = weaponObject.AddComponent<SpriteRenderer>();
            meleeWeaponRenderer.enabled = false;
        }

        private void RefreshMeleeWeaponSorting()
        {
            if (meleeWeaponRenderer == null || targetRenderer == null)
            {
                return;
            }

            meleeWeaponRenderer.sortingLayerID = targetRenderer.sortingLayerID;
            meleeWeaponRenderer.sortingOrder = targetRenderer.sortingOrder + 2;
        }

        private void UpdateMeleeWeaponPose(float normalizedTime)
        {
            if (meleeWeaponTransform == null)
            {
                return;
            }

            Vector2 direction = activeAttackDirection.sqrMagnitude > 0.001f ? activeAttackDirection.normalized : Vector2.right;
            float facingSign = Mathf.Abs(direction.x) > 0.08f
                ? Mathf.Sign(direction.x)
                : (targetRenderer != null && targetRenderer.flipX ? -1f : 1f);
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalizedTime - 0.04f) / 0.78f));
            float recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalizedTime - 0.72f) / 0.28f));
            float sweepAngle = Mathf.Lerp(58f * facingSign, -78f * facingSign, swing) + recover * 18f * facingSign;
            float directionAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector2 handAnchor = new Vector2(-0.32f * facingSign, -0.31f);
            Vector2 gripFollow = direction * Mathf.Lerp(0.01f, 0.04f, swing) + Vector2.up * Mathf.Lerp(0.015f, -0.015f, swing);
            float pulse = Mathf.Sin(normalizedTime * Mathf.PI) * 0.06f;

            meleeWeaponRenderer.flipY = facingSign < 0f;
            meleeWeaponTransform.localPosition = handAnchor + gripFollow;
            meleeWeaponTransform.localRotation = Quaternion.Euler(0f, 0f, directionAngle + sweepAngle);
            meleeWeaponTransform.localScale = Vector3.one * (meleeWeaponScale + pulse);
        }

        private void HideMeleeWeapon()
        {
            if (meleeWeaponRenderer != null)
            {
                meleeWeaponRenderer.enabled = false;
            }
        }

        private static void CopyRenderer(SpriteRenderer source, SpriteRenderer target)
        {
            target.sprite = source.sprite;
            target.color = source.color;
            target.flipX = source.flipX;
            target.flipY = source.flipY;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.drawMode = source.drawMode;
            target.size = source.size;
            target.maskInteraction = source.maskInteraction;
            target.spriteSortPoint = source.spriteSortPoint;
            target.sharedMaterial = source.sharedMaterial;
        }
    }

    public sealed class Dungeon2DSlashEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float duration = 0.22f;
        [SerializeField] private float forwardDrift = 0.16f;

        private Sprite[] frames;
        private float timer;
        private Vector3 initialScale;
        private Vector3 initialPosition;
        private Color initialColor;
        private int lastFrameIndex = -1;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            CaptureInitialState();
        }

        public void Configure(Sprite[] animationFrames)
        {
            if (animationFrames != null && animationFrames.Length > 0)
            {
                frames = animationFrames;
                duration = Mathf.Clamp(frames.Length * 0.036f, 0.14f, 0.28f);
            }

            timer = 0f;
            lastFrameIndex = -1;
            CaptureInitialState();
            ApplyAnimationFrame(0f);
        }

        private void OnEnable()
        {
            timer = 0f;
            lastFrameIndex = -1;
            CaptureInitialState();
        }

        private void CaptureInitialState()
        {
            initialScale = transform.localScale;
            initialPosition = transform.position;
            if (spriteRenderer != null)
            {
                initialColor = spriteRenderer.color;
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
            ApplyAnimationFrame(t);

            float pulse = Mathf.Sin(t * Mathf.PI) * 0.08f;
            float sweepScale = Mathf.Lerp(0.94f, 1.08f, Mathf.SmoothStep(0f, 1f, t)) + pulse;
            transform.localScale = initialScale * sweepScale;
            transform.position = initialPosition + transform.right * Mathf.Lerp(0f, forwardDrift, t);

            if (spriteRenderer != null)
            {
                Color color = initialColor;
                float fade = t < 0.62f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.62f) / 0.38f);
                color.a = initialColor.a * fade;
                spriteRenderer.color = color;
            }

            if (timer >= duration)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyAnimationFrame(float normalizedTime)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            int frameIndex = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(normalizedTime * frames.Length));
            if (frameIndex == lastFrameIndex || frames[frameIndex] == null)
            {
                return;
            }

            spriteRenderer.sprite = frames[frameIndex];
            lastFrameIndex = frameIndex;
        }
    }

    public sealed class Dungeon2DDashTrailEffect : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float duration = 0.22f;
        [SerializeField] private float driftDistance = 0.24f;

        private Vector3 startPosition;
        private Vector3 startScale;
        private Quaternion startRotation;
        private Vector2 driftDirection;
        private Color startColor = new Color(0.38f, 0.9f, 1f, 0.42f);
        private float timer;
        private float rotationWobble;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            CaptureStartState();
        }

        public void Configure(Vector2 dashDirection, Color color, float lifeTime, float scaleMultiplier, float drift)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            duration = Mathf.Max(0.05f, lifeTime);
            driftDistance = Mathf.Max(0f, drift);
            driftDirection = dashDirection.sqrMagnitude > 0.001f ? -dashDirection.normalized : Vector2.zero;
            timer = 0f;
            CaptureStartState();
            startColor = color;
            startScale *= Mathf.Max(0.1f, scaleMultiplier);
            transform.localScale = startScale;
            rotationWobble = Random.Range(-5f, 5f);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = startColor;
            }
        }

        private void CaptureStartState()
        {
            startPosition = transform.position;
            startScale = transform.localScale;
            startRotation = transform.rotation;
            if (spriteRenderer != null)
            {
                startColor = spriteRenderer.color;
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            transform.position = startPosition + (Vector3)(driftDirection * driftDistance * eased);
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.78f, t);
            transform.rotation = startRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * rotationWobble);

            if (spriteRenderer != null)
            {
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = color;
            }

            if (timer >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    public sealed class Dungeon2DWorldHealthBar : MonoBehaviour
    {
        private static Sprite barSprite;

        private Transform barRoot;
        private Transform fillTransform;
        private SpriteRenderer fillRenderer;
        private Color baseFillColor = Color.green;
        private float width = 1f;
        private const float Height = 0.08f;

        public void Configure(float barWidth, Vector2 localOffset, Color fillColor, int sortingOrder)
        {
            width = Mathf.Max(0.2f, barWidth);
            baseFillColor = fillColor;
            EnsureSprite();

            GameObject rootObject = new GameObject("HealthBar");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = localOffset;
            barRoot = rootObject.transform;

            GameObject backObject = new GameObject("Back");
            backObject.transform.SetParent(barRoot, false);
            backObject.transform.localScale = new Vector3(width + 0.08f, Height + 0.05f, 1f);
            SpriteRenderer backRenderer = backObject.AddComponent<SpriteRenderer>();
            backRenderer.sprite = barSprite;
            backRenderer.color = new Color(0.02f, 0.015f, 0.012f, 0.86f);
            backRenderer.sortingOrder = sortingOrder;

            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(barRoot, false);
            fillTransform = fillObject.transform;
            fillRenderer = fillObject.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = barSprite;
            fillRenderer.color = fillColor;
            fillRenderer.sortingOrder = sortingOrder + 1;
            SetRatio(1f);
        }

        public void SetRatio(float ratio)
        {
            if (barRoot == null || fillTransform == null)
            {
                return;
            }

            float safeRatio = Mathf.Clamp01(ratio);
            barRoot.gameObject.SetActive(safeRatio > 0f);
            fillTransform.localScale = new Vector3(width * safeRatio, Height, 1f);
            fillTransform.localPosition = new Vector3((safeRatio - 1f) * width * 0.5f, 0f, -0.01f);

            if (fillRenderer != null)
            {
                fillRenderer.color = safeRatio <= 0.3f
                    ? new Color(1f, 0.22f, 0.16f, 1f)
                    : baseFillColor;
            }
        }

        private static void EnsureSprite()
        {
            if (barSprite != null)
            {
                return;
            }

            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            barSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }
    }

    public sealed class Dungeon2DAudio : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip shootClip;
        private AudioClip hitClip;
        private AudioClip pickupClip;
        private AudioClip dashClip;
        private AudioClip bossClip;
        private AudioClip rewardClip;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.55f;

            shootClip = CreateTone("Shoot", 620f, 0.055f, 0.25f);
            hitClip = CreateTone("Hit", 145f, 0.08f, 0.38f);
            pickupClip = CreateTone("Pickup", 880f, 0.09f, 0.25f);
            dashClip = CreateSweep("Dash", 820f, 360f, 0.12f, 0.24f);
            bossClip = CreateSweep("Boss", 130f, 72f, 0.42f, 0.44f);
            rewardClip = CreateSweep("Reward", 520f, 1040f, 0.22f, 0.3f);
        }

        public void PlayShoot()
        {
            Play(shootClip, 0.32f);
        }

        public void PlayHit()
        {
            Play(hitClip, 0.45f);
        }

        public void PlayPickup()
        {
            Play(pickupClip, 0.42f);
        }

        public void PlayDash()
        {
            Play(dashClip, 0.36f);
        }

        public void PlayBoss()
        {
            Play(bossClip, 0.5f);
        }

        public void PlayReward()
        {
            Play(rewardClip, 0.44f);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip, volume);
            }
        }

        private static AudioClip CreateTone(string name, float frequency, float duration, float decay)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-t / Mathf.Max(0.01f, decay));
                samples[i] = Mathf.Sin(Mathf.PI * 2f * frequency * t) * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSweep(string name, float startFrequency, float endFrequency, float duration, float decay)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
                float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
                phase += Mathf.PI * 2f * frequency / sampleRate;
                float envelope = Mathf.Exp(-t / Mathf.Max(0.01f, decay)) * (1f - normalized * 0.25f);
                samples[i] = Mathf.Sin(phase) * envelope;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
