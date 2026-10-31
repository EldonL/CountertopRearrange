using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace CounterTopRearrange
{
    /// <summary>
    /// Gates the Take Picture button on countertop-plane coverage, then turns the
    /// vision answer into an anchored placement.
    ///
    /// Flow: TakeScreenshot captures -> MuseSparkVision raises onAnswer with a
    /// JSON point (see <see cref="PlacementAnswerParser"/>) -> this controller
    /// parses it, viewport-raycasts onto the counter plane, anchors the pose and
    /// spawns the book. One placement at a time; a new answer replaces the old one.
    /// </summary>
    public class ObjectPlacementController : MonoBehaviour
    {
        [Header("AR")]
        [SerializeField] ARPlaneManager _planeManager;
        [SerializeField] ARRaycastManager _raycastManager;
        [SerializeField] ARAnchorManager _anchorManager;

        [Header("Pipeline")]
        [SerializeField] MuseSparkVision _vision;
        [SerializeField] Button _takePictureButton;
        [SerializeField] TextMeshProUGUI _statusText;

        [Header("Placement")]
        [Tooltip("Prefab to spawn at the chosen spot. Grounded by measured bounds and faced +Z; auto-size rescales it to real-world size. When empty, a book-sized placeholder cube is used.")]
        [SerializeField] GameObject _bookPrefab;
        [Tooltip("Minimum horizontal plane area in m^2 before the counter counts as scanned.")]
        [SerializeField] float _minCounterArea = 0.04f;
        [Tooltip("Lift above the surface to avoid z-fighting, in meters.")]
        [SerializeField] float _surfaceLift = 0.005f;
        [Tooltip("Placeholder book size in meters (width, height, depth). Only used when no prefab is assigned.")]
        [SerializeField] Vector3 _bookSize = new Vector3(0.16f, 0.04f, 0.23f);
        [Tooltip("Seconds before a stalled capture/vision request re-enables the Take button.")]
        [SerializeField] float _busyTimeoutSeconds = 120f;

        [Header("Spawnable Object")]
        [Tooltip("Prefab chosen in the spawn menu. Overrides Book Prefab when set. Managed by SetSpawnableObject.")]
        [SerializeField] GameObject _spawnablePrefabOverride;
        [Tooltip("Display name of the chosen object, used in status messages.")]
        [SerializeField] string _spawnableDisplayName;

        [Header("Auto Size")]
        [Tooltip("When enabled, spawned prefabs are uniformly rescaled to real-world target sizes so models authored in cm or arbitrary units spawn at the correct size. Disable to spawn prefabs at authored scale (legacy behavior).")]
        [SerializeField] bool _autoScalePrefabs = true;
        [Tooltip("Default maximum horizontal footprint in meters, used when no per-object target size matches. Larger prefabs are shrunk to fit (aspect preserved).")]
        [SerializeField] float _defaultMaxFootprint = 0.3f;
        [Tooltip("Default maximum height in meters, used when no per-object target size matches.")]
        [SerializeField] float _defaultMaxHeight = 0.3f;
        [Tooltip("Smallest allowed largest-dimension in meters. Prefabs smaller than this are enlarged (aspect preserved). Set to 0 to disable growing tiny models.")]
        [SerializeField] float _minLargestDimension = 0.02f;
        [Tooltip("Per-object real-world target sizes in meters (width, height, depth). Key matches Spawnable Display Name or prefab name (case-insensitive). Matching prefabs are uniformly scaled to fit inside this box, preserving aspect.")]
        [SerializeField] List<SpawnSizeEntry> _targetSizes = new List<SpawnSizeEntry>();

        [Serializable]
        public struct SpawnSizeEntry
        {
            [Tooltip("Matches Spawnable Display Name or prefab name (case-insensitive).")]
            public string key;
            [Tooltip("Target box size in meters (width, height, depth). Prefab is uniformly scaled to fit inside.")]
            public Vector3 sizeMeters;
        }

        ARPlane _counterPlane;
        ARAnchor _currentAnchor;
        GameObject _currentBook;
        bool _busy;
        float _busyStartedAt;
        readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        void Awake()
        {
            if (_planeManager == null)
                _planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (_vision == null)
                _vision = FindAnyObjectByType<MuseSparkVision>();

            GameObject xrOrigin = _planeManager != null ? _planeManager.gameObject : null;
            if (_raycastManager == null)
                _raycastManager = xrOrigin != null
                    ? xrOrigin.GetComponent<ARRaycastManager>()
                    : FindAnyObjectByType<ARRaycastManager>();
            if (_raycastManager == null && xrOrigin != null)
                _raycastManager = xrOrigin.AddComponent<ARRaycastManager>();

            if (_anchorManager == null)
                _anchorManager = xrOrigin != null
                    ? xrOrigin.GetComponent<ARAnchorManager>()
                    : FindAnyObjectByType<ARAnchorManager>();
            if (_anchorManager == null && xrOrigin != null)
                _anchorManager = xrOrigin.AddComponent<ARAnchorManager>();

            if (_takePictureButton == null)
            {
                TakeScreenshot takeScreenshot = FindAnyObjectByType<TakeScreenshot>();
                if (takeScreenshot != null)
                    _takePictureButton = takeScreenshot.GetComponent<Button>();
            }

            UpdateCounterPlane();
            RefreshGate();
        }

        void OnEnable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged += OnPlanesChanged;
            // onAnswer is wired to OnVisionAnswer in the scene; subscribe in code
            // only as a fallback so the answer is never handled twice.
            if (_vision != null && _vision.onAnswer.GetPersistentEventCount() == 0)
                _vision.onAnswer.AddListener(OnVisionAnswer);
            if (_takePictureButton != null)
                _takePictureButton.onClick.AddListener(MarkBusy);
        }

        void OnDisable()
        {
            if (_planeManager != null)
                _planeManager.planesChanged -= OnPlanesChanged;
            if (_vision != null)
                _vision.onAnswer.RemoveListener(OnVisionAnswer);
            if (_takePictureButton != null)
                _takePictureButton.onClick.RemoveListener(MarkBusy);
        }

        void Update()
        {
            // Safety net: a failed screenshot save or vision request never calls
            // OnVisionAnswer, so release the button instead of wedging the UI.
            if (_busy && Time.unscaledTime - _busyStartedAt > _busyTimeoutSeconds)
            {
                Debug.LogWarning("ObjectPlacementController: request timed out; re-enabling Take button.");
                _busy = false;
                RefreshGate();
                SetStatus("Request timed out. Please try again.");
            }
        }

        /// <summary>Selects the prefab + display name for the next placement. Called by the spawn menu.</summary>
        public void SetSpawnableObject(SpawnableObjectSO spawnable)
        {
            if (spawnable == null)
                return;
            _spawnablePrefabOverride = spawnable.prefab;
            if (!string.IsNullOrWhiteSpace(spawnable.displayName))
                _spawnableDisplayName = spawnable.displayName.Trim();
            if (!_busy)
                RefreshGate();
        }

        string CurrentObjectName
        {
            get { return string.IsNullOrEmpty(_spawnableDisplayName) ? "book" : _spawnableDisplayName; }
        }

        string CurrentObjectLabel
        {
            get
            {
                string objectName = CurrentObjectName;
                return char.ToUpperInvariant(objectName[0]) + objectName.Substring(1);
            }
        }

        GameObject ActivePrefab
        {
            get { return _spawnablePrefabOverride != null ? _spawnablePrefabOverride : _bookPrefab; }
        }

        void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            UpdateCounterPlane();
            RefreshGate();
        }

        void MarkBusy()
        {
            if (_busy)
                return;
            _busy = true;
            _busyStartedAt = Time.unscaledTime;
            if (_takePictureButton != null)
                _takePictureButton.interactable = false;
            SetStatus("Taking picture...");
        }

        /// <summary>Wired to MuseSparkVision.onAnswer in the scene (code subscribes only if no persistent listener exists).</summary>
        public void OnVisionAnswer(string answer)
        {
            HandleAnswerAsync(answer);
        }

        async void HandleAnswerAsync(string answer)
        {
            Debug.Log("ObjectPlacementController: vision answer: " + answer);

            float normalizedX;
            float normalizedY;
            if (!PlacementAnswerParser.TryParse(answer, out normalizedX, out normalizedY))
            {
                Fail("Could not understand the placement spot. Please retake the picture.");
                return;
            }

            // Vision origin is top-left; screen origin is bottom-left.
            Vector2 screenPoint = new Vector2(
                normalizedX * Screen.width,
                (1f - normalizedY) * Screen.height);
            Debug.Log(string.Format(
                "ObjectPlacementController: normalized=({0}, {1}) screen=({2}, {3}) screenSize=({4}x{5})",
                normalizedX, normalizedY, screenPoint.x, screenPoint.y, Screen.width, Screen.height));

            UpdateCounterPlane();
            if (_counterPlane == null)
            {
                Fail("Lost the counter surface. Rescan and try again.");
                return;
            }

            ARRaycastHit hit;
            if (!TryFindCounterHit(screenPoint, out hit))
            {
                // Retry at screen center before giving up.
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                if (!TryFindCounterHit(center, out hit))
                {
                    Fail("No counter surface found at that spot. Please retake the picture.");
                    return;
                }
                Debug.Log("ObjectPlacementController: vision point missed; using screen-center hit.");
            }

            // Placeholder cubes pivot at their center. Prefabs are grounded by their measured bounds in NormalizePlacedObject, so base- and center-pivot models both rest on the surface.
            float lift = _surfaceLift + (ActivePrefab == null ? _bookSize.y * 0.5f : 0f);
            Vector3 position = hit.pose.position + hit.pose.up * lift;
            Quaternion rotation = YawTowardCamera(position, hit.pose.rotation);
            Pose pose = new Pose(position, rotation);

            ARAnchor anchor = await CreateAnchorAsync(pose, hit.trackableId);
            if (anchor == null)
            {
                Fail("Could not anchor the " + CurrentObjectName + ". Please try again.");
                return;
            }

            ReplaceCurrent(anchor, pose);
            _busy = false;
            RefreshGate();
            SetStatus(CurrentObjectLabel + " placed.");
        }

        bool TryFindCounterHit(Vector2 screenPoint, out ARRaycastHit hit)
        {
            hit = default(ARRaycastHit);
            if (_raycastManager == null || _counterPlane == null)
                return false;

            _hits.Clear();
            if (!_raycastManager.Raycast(screenPoint, _hits, TrackableType.PlaneWithinPolygon))
                return false;

            foreach (ARRaycastHit candidate in _hits)
            {
                if (candidate.trackableId == _counterPlane.trackableId)
                {
                    hit = candidate;
                    return true;
                }
            }

            hit = _hits[0];
            return true;
        }

        Quaternion YawTowardCamera(Vector3 position, Quaternion fallback)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return fallback;

            Vector3 toCamera = cam.transform.position - position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-6f)
                return fallback;

            return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
        }

        async Awaitable<ARAnchor> CreateAnchorAsync(Pose pose, TrackableId hitTrackableId)
        {
            if (_anchorManager == null || _planeManager == null)
                return null;

            ARPlane hitPlane = null;
            if (hitTrackableId == _counterPlane.trackableId)
                hitPlane = _counterPlane;
            else
                _planeManager.trackables.TryGetTrackable(hitTrackableId, out hitPlane);

            try
            {
                if (hitPlane != null
                    && _anchorManager.descriptor != null
                    && _anchorManager.descriptor.supportsTrackableAttachments)
                {
                    ARAnchor attached = _anchorManager.AttachAnchor(hitPlane, pose);
                    if (attached != null)
                        return attached;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("ObjectPlacementController: AttachAnchor failed, trying async anchor. " + e.Message);
            }

            try
            {
                Result<ARAnchor> result = await _anchorManager.TryAddAnchorAsync(pose);
                if (result.status.IsSuccess())
                    return result.value;
                Debug.LogError("ObjectPlacementController: TryAddAnchorAsync failed: " + result.status);
            }
            catch (Exception e)
            {
                Debug.LogError("ObjectPlacementController: TryAddAnchorAsync threw: " + e.Message);
            }

            return null;
        }

        void ReplaceCurrent(ARAnchor anchor, Pose pose)
        {
            if (_currentBook != null)
                Destroy(_currentBook);

            if (_currentAnchor != null)
            {
                bool removed = false;
                try
                {
                    removed = _anchorManager != null && _anchorManager.TryRemoveAnchor(_currentAnchor);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ObjectPlacementController: TryRemoveAnchor threw: " + e.Message);
                }
                if (!removed && _currentAnchor != null)
                    Destroy(_currentAnchor.gameObject);
                _currentAnchor = null;
            }

            GameObject prefab = ActivePrefab;
            _currentBook = prefab != null
                ? Instantiate(prefab, pose.position, pose.rotation)
                : BuildPlaceholderBook(pose);
            _currentAnchor = anchor;
            _currentBook.transform.SetParent(anchor.transform, true);
            if (prefab != null)
                NormalizePlacedObject(_currentBook, prefab);
        }

        GameObject BuildPlaceholderBook(Pose pose)
        {
            GameObject book = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book.name = "Placed " + CurrentObjectLabel + " (Placeholder)";
            book.transform.SetPositionAndRotation(pose.position, pose.rotation);
            book.transform.localScale = _bookSize;
            return book;
        }

        void NormalizePlacedObject(GameObject instance, GameObject prefab)
        {
            if (!_autoScalePrefabs)
                return;
            if (instance == null || prefab == null)
                return;
            Bounds bounds;
            if (!TryGetWorldBounds(instance, out bounds))
                return;
            Vector3 current = bounds.size;
            float largest = Mathf.Max(current.x, Mathf.Max(current.y, current.z));
            if (largest <= 1e-6f || float.IsNaN(largest) || float.IsInfinity(largest))
                return;

            float factor = 1f;
            Vector3 explicitTarget;
            if (TryGetExplicitTargetSize(prefab, out explicitTarget))
            {
                factor = ComputeFitFactor(current, explicitTarget);
            }
            else
            {
                Vector3 maxBox = new Vector3(
                    Mathf.Max(_defaultMaxFootprint, 1e-6f),
                    Mathf.Max(_defaultMaxHeight, 1e-6f),
                    Mathf.Max(_defaultMaxFootprint, 1e-6f));
                float fit = ComputeFitFactor(current, maxBox);
                if (fit < 1f)
                    factor = fit;
                else if (_minLargestDimension > 1e-6f && largest < _minLargestDimension)
                    factor = _minLargestDimension / largest;
            }

            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f)
                return;
            factor = Mathf.Clamp(factor, 1e-4f, 1e4f);
            if (Mathf.Abs(factor - 1f) >= 0.001f)
            {
                instance.transform.localScale = instance.transform.localScale * factor;
                Debug.Log(string.Format(
                    "ObjectPlacementController: rescaled '{0}' by {1:F3} (was {2:F2}x{3:F2}x{4:F2}m).",
                    prefab.name, factor, current.x, current.y, current.z));
            }

            GroundToAnchor(instance);
        }

        float ComputeFitFactor(Vector3 current, Vector3 target)
        {
            float factor = float.PositiveInfinity;
            if (current.x > 1e-6f && target.x > 1e-6f)
                factor = Mathf.Min(factor, target.x / current.x);
            if (current.y > 1e-6f && target.y > 1e-6f)
                factor = Mathf.Min(factor, target.y / current.y);
            if (current.z > 1e-6f && target.z > 1e-6f)
                factor = Mathf.Min(factor, target.z / current.z);
            if (float.IsInfinity(factor) || float.IsNaN(factor))
                return 1f;
            return factor;
        }

        void GroundToAnchor(GameObject instance)
        {
            if (instance == null || _currentAnchor == null)
                return;
            Bounds bounds;
            if (!TryGetWorldBounds(instance, out bounds))
                return;
            float anchorY = _currentAnchor.transform.position.y;
            if (float.IsNaN(bounds.min.y) || float.IsInfinity(bounds.min.y))
                return;
            if (float.IsNaN(anchorY) || float.IsInfinity(anchorY))
                return;
            float shift = anchorY - bounds.min.y;
            if (Mathf.Abs(shift) < 0.0005f)
                return;
            instance.transform.position += Vector3.up * shift;
        }

        bool TryGetExplicitTargetSize(GameObject prefab, out Vector3 sizeMeters)
        {
            sizeMeters = Vector3.zero;
            if (_targetSizes == null || _targetSizes.Count == 0 || prefab == null)
                return false;
            string prefabName = prefab.name;
            string display = string.IsNullOrEmpty(_spawnableDisplayName) ? null : _spawnableDisplayName.Trim();
            string fallback = CurrentObjectName;
            for (int i = 0; i < _targetSizes.Count; i++)
            {
                string key = _targetSizes[i].key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                key = key.Trim();
                bool match = false;
                if (!string.IsNullOrEmpty(display) && string.Equals(key, display, StringComparison.OrdinalIgnoreCase))
                    match = true;
                else if (!string.IsNullOrEmpty(fallback) && string.Equals(key, fallback.Trim(), StringComparison.OrdinalIgnoreCase))
                    match = true;
                else if (!string.IsNullOrEmpty(prefabName) && string.Equals(key, prefabName, StringComparison.OrdinalIgnoreCase))
                    match = true;
                if (!match)
                    continue;
                Vector3 candidate = _targetSizes[i].sizeMeters;
                if (candidate.x > 1e-6f && candidate.y > 1e-6f && candidate.z > 1e-6f)
                {
                    sizeMeters = candidate;
                    return true;
                }
                Debug.LogWarning("ObjectPlacementController: target size for '" + key + "' is invalid; using default clamp.");
                return false;
            }
            return false;
        }

        bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            if (root == null)
                return false;
            bool has = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;
                if (has)
                    bounds.Encapsulate(r.bounds);
                else
                {
                    bounds = r.bounds;
                    has = true;
                }
            }
            if (has && !float.IsNaN(bounds.size.x + bounds.size.y + bounds.size.z) && !float.IsInfinity(bounds.size.x + bounds.size.y + bounds.size.z))
                return true;
            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            bool hasCollider = false;
            Bounds colliderBounds = new Bounds();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                    continue;
                if (hasCollider)
                    colliderBounds.Encapsulate(c.bounds);
                else
                {
                    colliderBounds = c.bounds;
                    hasCollider = true;
                }
            }
            if (hasCollider && !float.IsNaN(colliderBounds.size.x + colliderBounds.size.y + colliderBounds.size.z) && !float.IsInfinity(colliderBounds.size.x + colliderBounds.size.y + colliderBounds.size.z))
            {
                bounds = colliderBounds;
                return true;
            }
            return false;
        }

        void UpdateCounterPlane()
        {
            _counterPlane = null;
            if (_planeManager == null)
                return;

            float bestArea = 0f;
            foreach (ARPlane plane in _planeManager.trackables)
            {
                if (plane.alignment != PlaneAlignment.HorizontalUp)
                    continue;
                if (plane.trackingState != TrackingState.Tracking)
                    continue;
                float area = plane.size.x * plane.size.y;
                if (area < _minCounterArea)
                    continue;
                if (area > bestArea)
                {
                    bestArea = area;
                    _counterPlane = plane;
                }
            }
        }

        void RefreshGate()
        {
            if (_takePictureButton != null)
                _takePictureButton.interactable = _counterPlane != null && !_busy;
            if (!_busy)
                SetStatus(_counterPlane != null
                    ? "Counter found. Take a picture to place the " + CurrentObjectName + "."
                    : "Scanning... point the camera at the counter.");
        }

        void Fail(string message)
        {
            _busy = false;
            RefreshGate();
            SetStatus(message);
        }

        void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message;
            Debug.Log("ObjectPlacementController: " + message);
        }
    }
}
