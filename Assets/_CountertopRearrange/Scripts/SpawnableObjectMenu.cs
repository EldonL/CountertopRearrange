using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CounterTopRearrange
{
    /// <summary>
    /// Horizontal scrollable menu of spawnable objects. Reads its entries from
    /// <see cref="SpawnableObjectsSO"/> and shows each object's image + name.
    /// Tapping an entry selects it for the next capture; the prompt label then
    /// asks the user to point at the surface (the existing Take Picture gating
    /// is untouched and enables the button once a surface is found).
    ///
    /// Setup (scene mode): wire Catalog, Content and Prompt Label to the
    /// hierarchy objects; tiles populate Content at runtime. When Content is
    /// empty, the whole menu (scroll view, items, prompt) is built in code.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpawnableObjectMenu : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Catalog shown in the menu. When empty, Resources/SpawnableObjects is used.")]
        [SerializeField] SpawnableObjectsSO _catalog;
        [Tooltip("Placement controller notified of the selection. Found automatically when empty.")]
        [SerializeField] ObjectPlacementController _placement;

        [Header("Scene Wiring (optional)")]
        [Tooltip("Content RectTransform of the hierarchy ScrollView (Scroll > Viewport > Content). When assigned, tiles populate it instead of a runtime-built menu.")]
        [SerializeField] RectTransform _content;
        [Tooltip("Prompt label in the hierarchy. When empty, no prompt is shown.")]
        [SerializeField] TextMeshProUGUI _promptLabel;

        [Header("Layout")]
        [Tooltip("Menu height in pixels (prompt label + scroll row).")]
        [SerializeField] float _menuHeight = 216f;
        [Tooltip("Right inset in pixels so the menu does not sit under the Take Picture button.")]
        [SerializeField] float _rightInset = 500f;
        [Tooltip("Item tile size in pixels (width x height).")]
        [SerializeField] Vector2 _itemSize = new Vector2(132f, 150f);
        [Tooltip("Background color of the selected tile.")]
        [SerializeField] Color _selectedColor = new Color(0.25f, 0.55f, 1f, 0.95f);
        [Tooltip("Background color of unselected tiles.")]
        [SerializeField] Color _normalColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);

        [Header("Behavior")]
        [Tooltip("Select the first entry on start so a capture always has an object.")]
        [SerializeField] bool _autoSelectFirst = true;

        public UnityEvent<SpawnableObjectSO> onSelected;

        readonly List<Image> _tileBackgrounds = new List<Image>();
        readonly List<SpawnableObjectSO> _entries = new List<SpawnableObjectSO>();
        RectTransform _activeContent;
        bool _built;

        public SpawnableObjectSO Selected
        {
            get { return SpawnableObjectSelection.Current; }
        }

        void Awake()
        {
            if (_catalog == null)
                _catalog = Resources.Load<SpawnableObjectsSO>("SpawnableObjects");
            if (_placement == null)
                _placement = FindAnyObjectByType<ObjectPlacementController>();
            EnsureBuilt();
        }

        void Start()
        {
            EnsureBuilt();
            if (_autoSelectFirst && SpawnableObjectSelection.Current == null && _entries.Count > 0)
                Select(_entries[0]);
            else
                RefreshSelection();
        }

        void OnEnable()
        {
            SpawnableObjectSelection.Changed += OnSelectionChanged;
        }

        void OnDisable()
        {
            SpawnableObjectSelection.Changed -= OnSelectionChanged;
        }

        /// <summary>Selects an entry (wired to each tile button).</summary>
        public void Select(SpawnableObjectSO entry)
        {
            if (entry == null)
                return;
            SpawnableObjectSelection.Current = entry;
            if (_placement == null)
                _placement = FindAnyObjectByType<ObjectPlacementController>();
            if (_placement != null)
                _placement.SetSpawnableObject(entry);
            RefreshSelection();
            if (onSelected != null)
                onSelected.Invoke(entry);
        }

        /// <summary>Swaps the catalog at runtime and rebuilds the tiles.</summary>
        public void SetCatalog(SpawnableObjectsSO catalog)
        {
            _catalog = catalog;
            EnsureBuilt();
            if (_activeContent != null)
            {
                foreach (Transform child in _activeContent)
                {
                    if (Application.isPlaying)
                        Destroy(child.gameObject);
                    else
                        DestroyImmediate(child.gameObject);
                }
            }
            Populate(_activeContent);
            if (SpawnableObjectSelection.Current == null && _entries.Count > 0)
                Select(_entries[0]);
            else
                RefreshSelection();
        }

        void OnSelectionChanged(SpawnableObjectSO _)
        {
            RefreshSelection();
        }

        void RefreshSelection()
        {
            for (int i = 0; i < _tileBackgrounds.Count && i < _entries.Count; i++)
            {
                bool selected = _entries[i] != null && _entries[i] == SpawnableObjectSelection.Current;
                _tileBackgrounds[i].color = selected ? _selectedColor : _normalColor;
            }
            if (_promptLabel != null)
            {
                SpawnableObjectSO current = SpawnableObjectSelection.Current;
                _promptLabel.text = current != null
                    ? "Selected: " + current.displayName + " - point at the surface, then Take Picture"
                    : "Pick an object, point at the surface, then Take Picture";
            }
        }

        void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;

            _activeContent = _content != null
                ? ConfigureSceneView()
                : BuildRuntimeView();
            Populate(_activeContent);
        }

        /// <summary>
        /// Scene mode: Scroll, Viewport, Content and the prompt label already
        /// exist in the hierarchy with Content wired to <see cref="_content"/>.
        /// The standard ScrollView behavior components (ScrollRect, Mask,
        /// HorizontalLayoutGroup, ContentSizeFitter) are attached here when
        /// missing and every setting is enforced in code, so the menu behaves
        /// identically to the runtime-built one.
        /// </summary>
        RectTransform ConfigureSceneView()
        {
            RectTransform viewport = _content.parent as RectTransform;
            if (viewport == null || viewport.parent == null)
            {
                Debug.LogError("SpawnableObjectMenu: Content must sit under Scroll > Viewport. Tiles will be built without scrolling.");
                return _content;
            }

            ScrollRect scroll = viewport.GetComponentInParent<ScrollRect>();
            if (scroll == null)
                scroll = viewport.parent.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = viewport;
            scroll.content = _content;

            Mask mask = viewport.GetComponent<Mask>();
            if (mask == null)
                mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            HorizontalLayoutGroup layout = _content.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            return _content;
        }

        /// <summary>Fallback when nothing is wired in the scene: builds the whole menu in code.</summary>
        RectTransform BuildRuntimeView()
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.offsetMin = new Vector2(8f, 8f);
            root.offsetMax = new Vector2(-_rightInset, 8f + _menuHeight);

            Image backdrop = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<Image>();
            backdrop.transform.SetParent(root, false);
            ((RectTransform)backdrop.transform).anchorMin = Vector2.zero;
            ((RectTransform)backdrop.transform).anchorMax = Vector2.one;
            ((RectTransform)backdrop.transform).offsetMin = Vector2.zero;
            ((RectTransform)backdrop.transform).offsetMax = Vector2.zero;
            backdrop.sprite = CountertopUi.WhiteSprite();
            backdrop.color = new Color(0f, 0f, 0f, 0.45f);
            backdrop.raycastTarget = false;

            _promptLabel = CountertopUi.MakeLabel(root, "PromptLabel", 30f, Color.white);
            RectTransform promptRect = (RectTransform)_promptLabel.transform;
            promptRect.anchorMin = new Vector2(0f, 1f);
            promptRect.anchorMax = new Vector2(1f, 1f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.offsetMin = new Vector2(8f, -52f);
            promptRect.offsetMax = new Vector2(-8f, -4f);

            ScrollRect scroll = new GameObject("Scroll", typeof(RectTransform)).AddComponent<ScrollRect>();
            scroll.transform.SetParent(root, false);
            RectTransform scrollRect = (RectTransform)scroll.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(4f, 4f);
            scrollRect.offsetMax = new Vector2(-4f, -56f);
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            RectTransform viewport = CountertopUi.FullStretch(scroll.transform, "Viewport");
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.sprite = CountertopUi.WhiteSprite();
            viewportImage.color = new Color(1f, 1f, 1f, 0.04f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport, false);
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 0f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 0.5f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new Vector2(0f, 0f);
            HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(6, 6, 4, 4);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = contentRect;

            return contentRect;
        }

        void Populate(RectTransform content)
        {
            _entries.Clear();
            _tileBackgrounds.Clear();
            if (_catalog != null && _catalog.objects != null)
            {
                foreach (SpawnableObjectSO entry in _catalog.objects)
                {
                    if (entry == null)
                        continue;
                    _entries.Add(entry);
                    BuildTile(content.transform, entry);
                }
            }

            if (_entries.Count == 0 && _promptLabel != null)
                _promptLabel.text = "No spawnable objects in catalog";
        }

        void BuildTile(Transform content, SpawnableObjectSO entry)
        {
            GameObject tile = new GameObject("Tile_" + entry.displayName, typeof(RectTransform));
            tile.transform.SetParent(content, false);
            RectTransform tileRect = (RectTransform)tile.transform;
            tileRect.sizeDelta = _itemSize;

            Image background = tile.AddComponent<Image>();
            background.sprite = CountertopUi.WhiteSprite();
            background.color = _normalColor;
            _tileBackgrounds.Add(background);

            Button button = tile.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => Select(entry));

            Image icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            icon.transform.SetParent(tile.transform, false);
            RectTransform iconRect = (RectTransform)icon.transform;
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -8f);
            iconRect.sizeDelta = new Vector2(96f, 96f);
            icon.sprite = entry.menuImage != null
                ? entry.menuImage
                : SpawnableFallbackIcon.FromPrefab(entry.prefab);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TextMeshProUGUI name = CountertopUi.MakeLabel(tile.transform, "Name", 24f, Color.white);
            RectTransform nameRect = (RectTransform)name.transform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.offsetMin = new Vector2(4f, 4f);
            nameRect.offsetMax = new Vector2(-4f, 40f);
            name.text = entry.displayName;
            name.enableAutoSizing = true;
            name.fontSizeMin = 18f;
            name.fontSizeMax = 26f;
        }
    }
}
