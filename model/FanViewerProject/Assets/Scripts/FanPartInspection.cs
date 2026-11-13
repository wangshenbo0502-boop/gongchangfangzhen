using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed class FanPartInspection : MonoBehaviour
{
    readonly Dictionary<Collider, int> colliderParts = new Dictionary<Collider, int>();
    FanViewer viewer;
    FanViewer.Manifest manifest;
    Transform[] parts;
    Camera viewCamera;
    Canvas canvas;
    Font font;
    readonly List<Renderer> sources = new List<Renderer>();
    readonly List<Renderer> masks = new List<Renderer>();
    readonly List<int> maskOwners = new List<int>();
    Camera maskCamera;
    GameObject maskRoot;
    Material white, black, outline;
    RenderTexture selectionTexture;
    RawImage outlineImage;
    int pending = -1, selected = -1;
    float pendingAt;
    Vector2 pointerStart;
    bool pointerMoved;
    readonly List<Text> labels = new List<Text>();
    readonly List<RectTransform> labelRoots = new List<RectTransform>();
    readonly List<RectTransform> leaders = new List<RectTransform>();
    readonly List<Renderer> labelAnchors = new List<Renderer>();
    readonly List<Rect> placedLabels = new List<Rect>();
    bool pointerStartedInView;
    bool labelsVisible, pickersEnabled = true;
    Matrix4x4 lastView;
    Rect lastViewport;
    float lastScale;
    int describedPart = -2;
    bool describedVisible;
    public int SelectedPart => selected;
    public IReadOnlyList<RectTransform> LabelRoots => labelRoots;
    public IReadOnlyList<RectTransform> Leaders => leaders;

    public void Initialize(FanViewer owner, Transform root, Camera camera, Canvas targetCanvas,
        Font uiFont, FanViewer.Manifest data)
    {
        viewer = owner;
        manifest = data;
        parts = data.parts.Select(p => root.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == p.name)).ToArray();
        viewCamera = camera;
        canvas = targetCanvas;
        font = uiFont;
        BuildPickers();
        BuildOutline();
        BuildLabels();
        BuildLabelAnchors(root);
    }

    void Update()
    {
        if (viewer.IsAutomatedReview) return;
        bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        if (Input.GetMouseButtonDown(0))
        {
            pointerStart = Input.mousePosition;
            pointerMoved = false;
            pointerStartedInView = !overUI && viewCamera.pixelRect.Contains(pointerStart);
        }
        if (Input.GetMouseButton(0) && Vector2.Distance(pointerStart, Input.mousePosition) > 5)
            pointerMoved = true;
        if (Input.GetMouseButtonUp(0))
        {
            if (pointerStartedInView && !overUI && !pointerMoved) Click(Input.mousePosition);
            else pending = -1;
            pointerStartedInView = false;
        }
    }

    public void Tick(bool animating, float progress, bool descriptionsVisible)
    {
        bool show = progress > .999f && !animating;
        if (pickersEnabled != show)
        {
            foreach (Collider picker in colliderParts.Keys) picker.enabled = show;
            pickersEnabled = show;
        }
        Matrix4x4 view = viewCamera.projectionMatrix * viewCamera.worldToCameraMatrix;
        bool layoutChanged = show && (!labelsVisible || view != lastView ||
            viewCamera.pixelRect != lastViewport || canvas.scaleFactor != lastScale);
        if (!show && labelsVisible)
            for (int i = 0; i < labels.Count; i++)
            {
                SetActive(labelRoots[i], false);
                SetActive(leaders[i], false);
            }
        if (layoutChanged) LayoutLabels();
        labelsVisible = show;
        lastView = view;
        lastViewport = viewCamera.pixelRect;
        lastScale = canvas.scaleFactor;
        if (selected >= 0 && outlineImage) UpdateMaskCamera();
        if (describedPart != selected || describedVisible != descriptionsVisible)
        {
            viewer.ShowDescription(selected >= 0 && descriptionsVisible
                ? manifest.parts[selected].label + "\n\n" + manifest.parts[selected].description : "");
            describedPart = selected;
            describedVisible = descriptionsVisible;
        }
    }

    static void SetActive(RectTransform target, bool active)
    {
        if (target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    void LayoutLabels()
    {
        placedLabels.Clear();
        RectTransform canvasRect = (RectTransform)canvas.transform;
        Rect viewport = viewCamera.pixelRect;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, viewport.min, null,
            out Vector2 viewportMin);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, viewport.max, null,
            out Vector2 viewportMax);
        for (int i = 0; i < labels.Count; i++)
        {
            Renderer anchor = labelAnchors[i];
            Vector3 screen = viewCamera.WorldToScreenPoint(anchor.bounds.center);
            if (screen.z <= 0 || !viewport.Contains(screen))
            {
                SetActive(labelRoots[i], false);
                SetActive(leaders[i], false);
                continue;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null,
                out Vector2 point);
            RectTransform rect = labelRoots[i];
            Vector2 half = rect.sizeDelta * .5f;
            Vector2 desired = Vector2.zero;
            bool found = false;
            for (int candidate = 0; candidate < 48; candidate++)
            {
                float angle = (candidate % 12 * 30 + (i % 2 == 0 ? 30 : 150)) * Mathf.Deg2Rad;
                float radius = 42 + candidate / 12 * 28;
                desired = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                desired.x = Mathf.Clamp(desired.x, viewportMin.x + half.x + 8, viewportMax.x - half.x - 8);
                desired.y = Mathf.Clamp(desired.y, viewportMin.y + half.y + 8, viewportMax.y - half.y - 8);
                Rect bounds = new Rect(desired - half - Vector2.one * 4, rect.sizeDelta + Vector2.one * 8);
                bool overlaps = false;
                for (int j = 0; j < placedLabels.Count; j++)
                    if (placedLabels[j].Overlaps(bounds)) { overlaps = true; break; }
                if (overlaps) continue;
                placedLabels.Add(bounds);
                found = true;
                break;
            }
            if (!found)
            {
                SetActive(rect, false);
                SetActive(leaders[i], false);
                continue;
            }
            rect.anchoredPosition = desired;
            SetActive(rect, true);
            RectTransform line = leaders[i];
            Vector2 end = new Vector2(Mathf.Clamp(point.x, desired.x - half.x, desired.x + half.x),
                Mathf.Clamp(point.y, desired.y - half.y, desired.y + half.y));
            Vector2 delta = end - point;
            line.anchoredPosition = point;
            line.sizeDelta = new Vector2(delta.magnitude, 1.2f);
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            SetActive(line, delta.magnitude > 3);
        }
    }

    void BuildPickers()
    {
        for (int i = 0; i < parts.Length; i++)
        foreach (MeshFilter filter in parts[i].GetComponentsInChildren<MeshFilter>())
            RegisterPicker(filter.gameObject, filter.sharedMesh, i);
        for (int i = 0; i < parts.Length; i++)
        foreach (SkinnedMeshRenderer skinned in parts[i].GetComponentsInChildren<SkinnedMeshRenderer>())
            RegisterPicker(skinned.gameObject, skinned.sharedMesh, i);
    }

    void RegisterPicker(GameObject target, Mesh mesh, int part)
    {
        var collider = target.GetComponent<MeshCollider>();
        if (!collider) collider = target.AddComponent<MeshCollider>();
        if (collider.sharedMesh != mesh) collider.sharedMesh = mesh;
        colliderParts[collider] = part;
    }

    void BuildOutline()
    {
        maskRoot = new GameObject("Fan component selection mask");
        white = new Material(viewer.selectionMaskShader);
        white.SetColor("_BaseColor", Color.white);
        black = new Material(viewer.selectionMaskShader);
        black.SetColor("_BaseColor", Color.black);
        for (int i = 0; i < parts.Length; i++)
        foreach (Renderer source in parts[i].GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = source is SkinnedMeshRenderer skinned
                ? skinned.sharedMesh
                : source.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) continue;
            GameObject copy = new GameObject(source.name) { layer = 31 };
            copy.transform.SetParent(maskRoot.transform, false);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = copy.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterials = Enumerable.Repeat(black, source.sharedMaterials.Length).ToArray();
            sources.Add(source);
            masks.Add(renderer);
            maskOwners.Add(i);
        }
        maskCamera = new GameObject("Fan selection camera", typeof(Camera)).GetComponent<Camera>();
        maskCamera.transform.SetParent(maskRoot.transform, false);
        maskCamera.enabled = false;
        maskCamera.depth = viewCamera.depth - 2;
        maskCamera.cullingMask = 1 << 31;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = Color.black;
        maskCamera.allowHDR = false;
        maskCamera.allowMSAA = false;
        maskCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        outline = new Material(viewer.selectionOutlineShader);
        GameObject overlay = new GameObject("Selected assembly outline", typeof(RectTransform));
        overlay.transform.SetParent(canvas.transform, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        outlineImage = overlay.AddComponent<RawImage>();
        outlineImage.material = outline;
        outlineImage.raycastTarget = false;
        outlineImage.enabled = false;
    }

    void UpdateMaskCamera()
    {
        int width = Mathf.Max(1, viewCamera.pixelWidth);
        int height = Mathf.Max(1, viewCamera.pixelHeight);
        if (!selectionTexture || selectionTexture.width != width || selectionTexture.height != height)
        {
            if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); }
            selectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            selectionTexture.Create();
            maskCamera.targetTexture = selectionTexture;
            outlineImage.texture = selectionTexture;
        }
        maskCamera.transform.SetPositionAndRotation(viewCamera.transform.position, viewCamera.transform.rotation);
        maskCamera.orthographic = viewCamera.orthographic;
        maskCamera.orthographicSize = viewCamera.orthographicSize;
        maskCamera.aspect = viewCamera.aspect;
        maskCamera.nearClipPlane = viewCamera.nearClipPlane;
        maskCamera.farClipPlane = viewCamera.farClipPlane;
        outlineImage.rectTransform.anchorMin = viewCamera.rect.min;
        outlineImage.rectTransform.anchorMax = viewCamera.rect.max;
        outlineImage.rectTransform.offsetMin = outlineImage.rectTransform.offsetMax = Vector2.zero;
    }

    void BuildLabels()
    {
        for (int i = 0; i < parts.Length; i++)
        {
            GameObject leader = new GameObject("Assembly label leader", typeof(RectTransform), typeof(Image));
            leader.transform.SetParent(canvas.transform, false);
            RectTransform line = leader.GetComponent<RectTransform>();
            line.pivot = new Vector2(0, .5f);
            leader.GetComponent<Image>().color = new Color(.10f, .43f, .39f, .8f);
            leader.GetComponent<Image>().raycastTarget = false;
            leader.SetActive(false);
            leaders.Add(line);
            GameObject label = new GameObject("Assembly label", typeof(RectTransform), typeof(Image));
            label.transform.SetParent(canvas.transform, false);
            label.GetComponent<Image>().color = new Color(.075f, .095f, .095f, .91f);
            label.GetComponent<Image>().raycastTarget = false;
            Text text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(label.transform, false);
            text.font = font;
            text.fontSize = 13;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = (i + 1).ToString("00") + "  " + manifest.parts[i].label;
            text.raycastTarget = false;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(7, 1);
            textRect.offsetMax = new Vector2(-7, -1);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(Mathf.Clamp(text.preferredWidth + 14, 94, 190), 26);
            labels.Add(text);
            labelRoots.Add(labelRect);
            label.SetActive(false);
        }
    }

    void BuildLabelAnchors(Transform root)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            Renderer[] owned = parts[i].GetComponentsInChildren<Renderer>(true);
            string key = NameKey(manifest.parts[i].anchor);
            Renderer anchor = owned.FirstOrDefault(r => NameKey(r.name) == key);
            if (!anchor)
            {
                if (owned.Length == 0)
                    throw new System.InvalidOperationException(
                        "Assembly has no renderers for label: " + manifest.parts[i].name);
                anchor = owned[0];
                Debug.LogWarning("Fan label anchor fallback: " + manifest.parts[i].anchor);
            }
            labelAnchors.Add(anchor);
        }
    }

    public static string NameKey(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    void Click(Vector2 position)
    {
        if (viewer.IsAnimating || viewer.Progress < .999f || !viewCamera.pixelRect.Contains(position))
        {
            ClearSelection();
            return;
        }
        Physics.SyncTransforms();
        if (Physics.Raycast(viewCamera.ScreenPointToRay(position), out RaycastHit hit, 100) &&
            colliderParts.TryGetValue(hit.collider, out int part))
        {
            if (pending == part && Time.unscaledTime - pendingAt <= .65f)
            {
                selected = part;
                pending = -1;
                viewer.EnableDescriptions();
                for (int i = 0; i < masks.Count; i++)
                {
                    masks[i].sharedMaterials = Enumerable.Repeat(maskOwners[i] == selected ? white : black,
                        sources[i].sharedMaterials.Length).ToArray();
                    Transform source = sources[i].transform, target = masks[i].transform;
                    target.SetPositionAndRotation(source.position, source.rotation);
                    target.localScale = source.lossyScale;
                }
                UpdateMaskCamera();
                outlineImage.enabled = true;
                maskCamera.enabled = true;
            }
            else { pending = part; pendingAt = Time.unscaledTime; }
        }
        else ClearSelection();
    }

    public bool SelectForReview(Vector2 screenPosition)
    {
        ClearSelection();
        Click(screenPosition);
        pendingAt = Time.unscaledTime;
        Click(screenPosition);
        return selected >= 0;
    }

    public void ClearSelection()
    {
        if (selected < 0 && pending < 0) return;
        selected = -1;
        pending = -1;
        if (outlineImage) outlineImage.enabled = false;
        if (maskCamera) maskCamera.enabled = false;
        if (viewer) viewer.ShowDescription("");
    }

    void OnDestroy()
    {
        if (maskCamera) maskCamera.targetTexture = null;
        if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); }
        if (white) Destroy(white);
        if (black) Destroy(black);
        if (outline) Destroy(outline);
        if (maskRoot) Destroy(maskRoot);
    }
}
