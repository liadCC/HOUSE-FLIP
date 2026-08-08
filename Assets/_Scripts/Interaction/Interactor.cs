using HouseFlip.Core;
using HouseFlip.Player;
using HouseFlip.UI;
using UnityEngine;

namespace HouseFlip.Interaction
{
    /// <summary>
    /// Local-only aiming component (GDD 7). Sits on the player, casts from the camera,
    /// and drives the single [E] prompt.
    ///
    /// Runs only for the owning client — remote players do not raycast.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private float range = GameConstants.InteractRange;

        [Tooltip("Radius of the forgiving sphere cast used when the thin ray misses.")]
        [SerializeField] private float assistRadius = 0.35f;

        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private PlayerController _player;
        private Camera _camera;

        public IInteractable Current { get; private set; }
        public Component CurrentComponent { get; private set; }

        // Every hold-to-use component (40 dirt sources + 16 fixtures) needs the local
        // player's Interactor every frame to know whether it is the aimed-at target.
        // They all ask about the same one player, so one shared slot removes ~56
        // GetComponent walks per frame instead of paying for one lookup each.
        private static PlayerController _cachedOwner;
        private static Interactor _cachedInteractor;

        /// <summary>
        /// The <see cref="Interactor"/> on <paramref name="player"/>, memoised.
        /// Safe across respawns: a new (or destroyed) player object misses the cache
        /// and forces a fresh lookup.
        /// </summary>
        public static Interactor For(PlayerController player)
        {
            if (player == null)
            {
                return null;
            }

            if (!ReferenceEquals(player, _cachedOwner) || _cachedInteractor == null)
            {
                _cachedOwner = player;
                _cachedInteractor = player.GetComponent<Interactor>();
            }

            return _cachedInteractor;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!_player.IsLocalPlayer)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            Refresh();

            if (Current != null && Input.GetKeyDown(interactKey))
            {
                Current.OnInteract(_player);
                // The target may destroy itself or change state, so re-resolve next frame.
            }
        }

        private void Refresh()
        {
            IInteractable found = Probe(out Component ownerComponent);

            Current = found;
            CurrentComponent = ownerComponent;

            string prompt = Current != null ? Current.GetPromptText() : null;
            InteractionPromptUI.SetPrompt(string.IsNullOrEmpty(prompt) ? null : prompt);
        }

        private IInteractable Probe(out Component ownerComponent)
        {
            ownerComponent = null;

            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

            // A thin ray first so precise aiming wins, then a fat one so the player does
            // not have to be a marksman to grab a lamp.
            if (Physics.Raycast(ray, out RaycastHit hit, range + 2f, GameLayers.InteractableMask,
                    QueryTriggerInteraction.Collide)
                && TryResolve(hit.collider, hit.point, out IInteractable direct, out ownerComponent))
            {
                return direct;
            }

            if (Physics.SphereCast(ray, assistRadius, out RaycastHit softHit, range + 2f,
                    GameLayers.InteractableMask, QueryTriggerInteraction.Collide)
                && TryResolve(softHit.collider, softHit.point, out IInteractable assisted, out ownerComponent))
            {
                return assisted;
            }

            return null;
        }

        private bool TryResolve(Collider collider, Vector3 point, out IInteractable interactable,
            out Component ownerComponent)
        {
            interactable = null;
            ownerComponent = null;

            if (collider == null)
            {
                return false;
            }

            // Measure from the player, not the camera: an orbiting third-person camera
            // sits metres behind the character and would otherwise inflate the range.
            if (Vector3.Distance(transform.position, point) > range)
            {
                return false;
            }

            // Find the nearest ancestor carrying any interaction at all.
            IInteractable[] candidates = null;
            GameObject owner = null;

            for (Transform t = collider.transform; t != null; t = t.parent)
            {
                IInteractable[] found = t.GetComponents<IInteractable>();
                if (found.Length > 0)
                {
                    candidates = found;
                    owner = t.gameObject;
                    break;
                }
            }

            if (candidates == null)
            {
                return false;
            }

            // Never offer to interact with the thing already in our hands.
            if (_player.Carry != null && _player.Carry.IsHolding(owner))
            {
                return false;
            }

            IInteractable chosen = Choose(candidates);
            if (chosen == null)
            {
                return false;
            }

            interactable = chosen;
            ownerComponent = chosen as Component;
            return true;
        }

        /// <summary>
        /// Resolves which of an object's interactions [E] means right now.
        ///
        /// The equipped tool wins — a hammer smashes the wall, a roller paints it. With no
        /// matching tool we fall back to whichever interaction still has something to say,
        /// so an empty-handed player still gets the "Need Hammer" hint instead of silence.
        /// </summary>
        private IInteractable Choose(IInteractable[] candidates)
        {
            if (candidates.Length == 1)
            {
                return HasPrompt(candidates[0]) ? candidates[0] : null;
            }

            ToolType equipped = _player.Tools != null ? _player.Tools.CurrentTool : ToolType.None;

            foreach (IInteractable candidate in candidates)
            {
                if (candidate is IToolGated gated && gated.RequiredTool == equipped && HasPrompt(candidate))
                {
                    return candidate;
                }
            }

            // Nothing matches the equipped tool, so prefer an interaction that needs no
            // tool at all. Component order decided this before, which meant an old cabinet
            // that is both grabbable and smashable offered a dead "Smash (Need Hammer)" to
            // anyone holding the vacuum — hiding the grab they could actually perform.
            foreach (IInteractable candidate in candidates)
            {
                if (RequiresNoTool(candidate) && HasPrompt(candidate))
                {
                    return candidate;
                }
            }

            // Last resort: the "you need tool X" hint, so [E] explains itself.
            foreach (IInteractable candidate in candidates)
            {
                if (HasPrompt(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// True when the interaction is performable whatever the player is holding —
        /// either it declares no tool gate, or its gate is <see cref="ToolType.None"/>
        /// (bare hands, like grabbing, which the server never tool-checks).
        /// </summary>
        private static bool RequiresNoTool(IInteractable candidate)
        {
            return !(candidate is IToolGated gated) || gated.RequiredTool == ToolType.None;
        }

        private static bool HasPrompt(IInteractable candidate)
        {
            return candidate != null && !string.IsNullOrEmpty(candidate.GetPromptText());
        }

        private void OnDisable()
        {
            if (_player != null && _player.IsLocalPlayer)
            {
                InteractionPromptUI.SetPrompt(null);
            }
        }
    }
}
