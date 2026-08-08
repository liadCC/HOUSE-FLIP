using HouseFlip.Core;
using UnityEngine;

namespace HouseFlip.Player
{
    /// <summary>
    /// Procedural character animation (GDD 6, which asks only for basic locomotion).
    ///
    /// There is no rig and no Animator here on purpose. For blocky low-poly characters,
    /// driving the limbs from code gives a better result than a thin blend tree would:
    /// it responds to actual speed rather than a blend parameter, it never foot-slides
    /// because it has no baked stride, and squash on landing costs three lines.
    ///
    /// Everything is derived from OBSERVED motion — how far the transform actually moved
    /// since last frame — rather than from input. That single choice is what makes remote
    /// players animate correctly: their transforms arrive over the network with no input
    /// attached, and they walk, swing and land exactly like the local one.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Body parts (optional — animation degrades gracefully)")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;

        [Header("Walk")]
        [Tooltip("Speed at which the walk cycle plays at full amplitude.")]
        [SerializeField] private float referenceSpeed = 4.5f;
        [SerializeField] private float strideFrequency = 2.1f;
        [SerializeField] private float limbSwing = 42f;
        [SerializeField] private float bodyBob = 0.055f;
        [SerializeField] private float leanAngle = 9f;

        [Header("Idle")]
        [SerializeField] private float breathAmount = 0.014f;
        [SerializeField] private float breathSpeed = 1.7f;

        [Header("Landing")]
        [SerializeField] private float landSquash = 0.22f;
        [SerializeField] private float landRecoverySpeed = 4.5f;

        [Header("Tool swing")]
        [SerializeField] private float swingDuration = 0.42f;
        [SerializeField] private float swingAngle = 115f;

        [Tooltip("Observed downward speed, in m/s, above which the character counts as falling.")]
        [SerializeField] private float fallThreshold = 2.5f;

        private PlayerController _player;

        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private float _fallSpeed;
        private float _cycle;
        private float _squash;
        private float _swingTimer = -1f;
        private bool _wasFalling;

        private Vector3 _bodyBasePosition, _headBasePosition;
        private Vector3 _bodyBaseScale;
        private bool _capturedRest;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _lastPosition = transform.position;
            CaptureRestPose();
        }

        private void CaptureRestPose()
        {
            if (_capturedRest)
            {
                return;
            }

            if (body != null)
            {
                _bodyBasePosition = body.localPosition;
                _bodyBaseScale = body.localScale;
            }

            if (head != null)
            {
                _headBasePosition = head.localPosition;
            }

            _capturedRest = true;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            UpdateObservedSpeed(dt);
            UpdateLanding(dt);
            UpdateSwing(dt);

            float gait = Mathf.Clamp01(_smoothedSpeed / Mathf.Max(0.01f, referenceSpeed));

            // The cycle advances with distance travelled, not with time, so the stride
            // stays locked to the ground at any speed instead of skating.
            _cycle += _smoothedSpeed * strideFrequency * dt;

            ApplyLimbs(gait);
            ApplyBody(gait);
        }

        private void UpdateObservedSpeed(float dt)
        {
            Vector3 delta = transform.position - _lastPosition;
            _lastPosition = transform.position;

            _fallSpeed = -delta.y / dt;

            delta.y = 0f;
            float speed = delta.magnitude / dt;

            // Network transforms arrive in steps, so raw frame-to-frame speed is spiky.
            // Smoothing it keeps a remote player's walk from stuttering.
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-12f * dt));
        }

        private void UpdateLanding(float dt)
        {
            // Landing is read off the transform, not off CharacterController.isGrounded.
            // isGrounded is only refreshed by whoever calls Move(), and only the owning
            // client does that — on every other peer a remote player's controller reports
            // "airborne" for the whole session, so the squash would never once fire for the
            // three characters you actually spend the game watching.
            bool falling = _fallSpeed > fallThreshold;

            if (_wasFalling && !falling)
            {
                _squash = landSquash;
            }

            _wasFalling = falling;
            _squash = Mathf.MoveTowards(_squash, 0f, landRecoverySpeed * dt);
        }

        private void UpdateSwing(float dt)
        {
            if (_swingTimer >= 0f)
            {
                _swingTimer += dt;
                if (_swingTimer > swingDuration)
                {
                    _swingTimer = -1f;
                }
            }
        }

        /// <summary>Plays a one-shot tool swing. Safe to call on any peer.</summary>
        public void PlaySwing()
        {
            _swingTimer = 0f;
        }

        /// <summary>
        /// Call after any non-locomotive jump in the transform (a spawn-point teleport).
        ///
        /// Speed here is measured as distance over frame time, so a metres-wide jump in one
        /// frame reads as hundreds of metres per second: without this the character would
        /// spend the first half-second after spawning windmilling its limbs at full stride.
        /// </summary>
        public void NotifyTeleported()
        {
            _lastPosition = transform.position;
            _smoothedSpeed = 0f;
            _fallSpeed = 0f;
            _wasFalling = false;
        }

        private void ApplyLimbs(float gait)
        {
            float swing = Mathf.Sin(_cycle) * limbSwing * gait;

            // Arms counter-swing against the legs, which is most of what makes a walk
            // read as a walk rather than a shuffle.
            float armSwing = -swing;

            bool carrying = _player != null && _player.Carry != null && _player.Carry.IsCarrying;
            if (carrying)
            {
                // Both arms forward and still: the character is holding something in front
                // of them, so a swing would look like they were juggling it.
                SetArm(armLeft, -62f);
                SetArm(armRight, -62f);
            }
            else
            {
                SetArm(armLeft, armSwing);
                SetArm(armRight, -armSwing);
            }

            SetLeg(legLeft, swing);
            SetLeg(legRight, -swing);

            ApplySwingToArm();
        }

        private void ApplySwingToArm()
        {
            if (armRight == null || _swingTimer < 0f)
            {
                return;
            }

            float t = Mathf.Clamp01(_swingTimer / Mathf.Max(0.01f, swingDuration));

            // Fast down-stroke, slower recovery — a hammer blow is not symmetric, and an
            // even arc reads as waving rather than hitting.
            float arc = t < 0.35f
                ? Mathf.Sin(t / 0.35f * (Mathf.PI * 0.5f))
                : Mathf.Cos((t - 0.35f) / 0.65f * (Mathf.PI * 0.5f));

            armRight.localRotation = Quaternion.Euler(-swingAngle * arc, 0f, 0f);
        }

        private static void SetArm(Transform arm, float angle)
        {
            if (arm != null)
            {
                arm.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }

        private static void SetLeg(Transform leg, float angle)
        {
            if (leg != null)
            {
                leg.localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
        }

        private void ApplyBody(float gait)
        {
            CaptureRestPose();

            if (body == null)
            {
                return;
            }

            // Two bobs per stride: the body rises on each footfall, not each full cycle.
            float bob = Mathf.Abs(Mathf.Sin(_cycle)) * bodyBob * gait;

            // Idle breathing only when actually still, so it does not fight the walk.
            float breath = Mathf.Sin(Time.time * breathSpeed) * breathAmount * (1f - gait);

            body.localPosition = _bodyBasePosition + new Vector3(0f, bob + breath, 0f);

            // Squash preserves volume: flatten vertically, widen horizontally.
            float squashY = 1f - _squash;
            float squashXZ = 1f + _squash * 0.5f;
            body.localScale = new Vector3(
                _bodyBaseScale.x * squashXZ,
                _bodyBaseScale.y * squashY,
                _bodyBaseScale.z * squashXZ);

            // Lean into the run. Applied to the body only, never the root, because the
            // root's rotation is the player's aim and is replicated.
            float lean = leanAngle * gait;
            body.localRotation = Quaternion.Euler(lean, 0f, 0f);

            if (head != null)
            {
                // Head lags the bob slightly, which gives the walk a bit of weight.
                float headBob = Mathf.Abs(Mathf.Sin(_cycle - 0.6f)) * bodyBob * 0.6f * gait;
                head.localPosition = _headBasePosition + new Vector3(0f, headBob + breath, 0f);
                head.localRotation = Quaternion.Euler(-lean * 0.5f, 0f, 0f);
            }
        }

        /// <summary>Wires body parts up at build time so the generator does not need reflection.</summary>
        public void SetRig(Transform bodyPart, Transform headPart, Transform leftArm, Transform rightArm,
            Transform leftLeg, Transform rightLeg)
        {
            body = bodyPart;
            head = headPart;
            armLeft = leftArm;
            armRight = rightArm;
            legLeft = leftLeg;
            legRight = rightLeg;
            _capturedRest = false;
        }
    }
}
