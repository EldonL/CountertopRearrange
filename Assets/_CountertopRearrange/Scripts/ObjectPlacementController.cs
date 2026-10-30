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
        [Tooltip("Prefab to spawn at the chosen spot. Assumed to rest on the surface with its base pivot and face +Z. When empty, a book-sized placeholder cube is used.")]
        [SerializeField] GameObject _bookPrefab;
        [Tooltip("Minimum horizontal plane area in m^2 before the counter counts as scanned.")]
        [SerializeField] float _minCounterArea = 0.04f;
        [Tooltip("Lift above the surface to avoid z-fighting, in meters.")]
        [SerializeField] float _surfaceLift = 0.005f;
        [Tooltip("Placeholder book size in meters (width, height, depth). Only used when no prefab is assigned.")]
        [SerializeField] Vector3 _bookSize = new Vector3(0.16f, 0.04f, 0.23f);
        [Tooltip("Seconds before a stalled capture/vision request re-enables the Take button.")]
        [SerializeField] float _busyTimeoutSeconds = 120f;

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

            // Placeholder cubes pivot at their center; prefabs are expected to pivot at the base.
            float lift = _surfaceLift + (_bookPrefab == null ? _bookSize.y * 0.5f : 0f);
            Vector3 position = hit.pose.position + hit.pose.up * lift;
            Quaternion rotation = YawTowardCamera(position, hit.pose.rotation);
            Pose pose = new Pose(position, rotation);

            ARAnchor anchor = await CreateAnchorAsync(pose, hit.trackableId);
            if (anchor == null)
            {
                Fail("Could not anchor the book. Please try again.");
                return;
            }

            ReplaceCurrent(anchor, pose);
            _busy = false;
            RefreshGate();
            SetStatus("Book placed.");
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

            _currentBook = _bookPrefab != null
                ? Instantiate(_bookPrefab, pose.position, pose.rotation)
                : BuildPlaceholderBook(pose);
            _currentAnchor = anchor;
            _currentBook.transform.SetParent(anchor.transform, true);
        }

        GameObject BuildPlaceholderBook(Pose pose)
        {
            GameObject book = GameObject.CreatePrimitive(PrimitiveType.Cube);
            book.name = "PlacedBook (Placeholder)";
            book.transform.SetPositionAndRotation(pose.position, pose.rotation);
            book.transform.localScale = _bookSize;
            return book;
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
                    ? "Counter found. Take a picture to place the book."
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
