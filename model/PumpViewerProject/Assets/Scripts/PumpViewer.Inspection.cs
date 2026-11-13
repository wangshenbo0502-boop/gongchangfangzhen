using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public sealed partial class PumpViewer
{
    public Shader selectionMaskShader, selectionOutlineShader;
    readonly Dictionary<Collider, int> pickParts = new Dictionary<Collider, int>();
    readonly List<Renderer> maskSources = new List<Renderer>();
    readonly List<MeshRenderer> maskCopies = new List<MeshRenderer>();
    readonly List<int> maskParts = new List<int>();
    Camera maskCamera;
    GameObject maskRoot;
    Material maskWhite, maskBlack, outlineMaterial;
    RenderTexture selectionTexture;
    RawImage outlineImage;
    RectTransform introductionPanel;
    Text introductionTitle, introductionBody;
    Button introductionButton;
    bool introductionEnabled = true, clickPending, clickMoved;
    Vector2 clickStart;
    int selectedPart = -1, pendingPart = -1;
    float pendingPartTime;

    static readonly string[] PartDescriptions =
    {
        "承托泵体与电机，通过地脚螺栓固定设备；保持轴系对中，并将运行载荷传递至基础。",
        "将电能转换为轴的旋转动力，经联轴器带动泵叶轮；外壳散热筋帮助电机散热。",
        "连接电机与泵轴，传递扭矩；弹性元件缓冲冲击，并补偿小量安装偏差。",
        "隔离高速旋转的联轴器，降低误触风险；网孔兼顾观察与散热，支架固定在底板上。",
        "收集叶轮甩出的液体，沿逐渐扩大的蜗形流道引向出口，将部分流速能转换为压力能。",
        "连接吸入管路并封闭泵腔前端；法兰、定位止口与密封圈共同保证连接和密封。",
        "密封环限制液体沿泵轴泄漏；支座支撑密封腔，保持密封与轴系的相对位置。",
        "泵的主要做功部件。旋转叶片将机械能传给液体，使液体从中心流向外缘。",
        "封闭电机接线区域，保护接线端子；与密封垫配合，减少灰尘和水汽进入。",
        "与后盖板、叶片形成叶轮流道，约束液体流动，减少跨叶道泄漏。",
        "定位并保护轴承，为轴承外圈提供安装配合，并容纳润滑介质。",
        "支承旋转泵轴，承受径向及相应轴向载荷；滚动体降低摩擦，维持轴系稳定。"
    };

    void BuildPartInspection()
    {
        for (int i = 0; i < parts.Length; i++)
        foreach (MeshFilter filter in parts[i].GetComponentsInChildren<MeshFilter>())
        {
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            pickParts.Add(collider, i);
        }
        introductionPanel = Rect("Part introduction", canvas.transform, Vector2.one, Vector2.one,
            new Vector2(-354, -274), new Vector2(-22, -112));
        Panel(introductionPanel, new Color(.965f, .975f, .975f, .97f));
        introductionTitle = TextAt("", introductionPanel, Vector2.zero, Vector2.one,
            new Vector2(18, 106), new Vector2(-18, -14), 20, ink);
        introductionBody = TextAt("", introductionPanel, Vector2.zero, Vector2.one,
            new Vector2(18, 16), new Vector2(-18, -56), 16, ink, TextAnchor.UpperLeft);
        introductionPanel.gameObject.SetActive(false);
        maskRoot = new GameObject("Part selection mask");
        maskWhite = new Material(selectionMaskShader);
        maskWhite.SetColor("_BaseColor", Color.white);
        maskBlack = new Material(selectionMaskShader);
        maskBlack.SetColor("_BaseColor", Color.black);
        for (int i = 0; i < parts.Length; i++)
        foreach (Renderer original in parts[i].GetComponentsInChildren<Renderer>())
        {
            var copy = new GameObject(original.name) { layer = 31 };
            copy.transform.SetParent(maskRoot.transform, false);
            copy.AddComponent<MeshFilter>().sharedMesh = original.GetComponent<MeshFilter>().sharedMesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterials = Enumerable.Repeat(maskBlack, original.sharedMaterials.Length).ToArray();
            maskSources.Add(original); maskCopies.Add(renderer); maskParts.Add(i);
        }
        maskCamera = new GameObject("Selection camera", typeof(Camera)).GetComponent<Camera>();
        maskCamera.transform.SetParent(maskRoot.transform, false);
        maskCamera.enabled = false;
        maskCamera.depth = viewCamera.depth - 2;
        maskCamera.cullingMask = 1 << 31;
        maskCamera.clearFlags = CameraClearFlags.SolidColor;
        maskCamera.backgroundColor = Color.black;
        maskCamera.allowHDR = false;
        maskCamera.allowMSAA = false;
        maskCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        viewCamera.cullingMask &= ~(1 << 31);
        outlineMaterial = new Material(selectionOutlineShader);
        var rect = Rect("Selected part outline", canvas.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        rect.SetAsFirstSibling();
        outlineImage = rect.gameObject.AddComponent<RawImage>();
        outlineImage.material = outlineMaterial;
        outlineImage.raycastTarget = false;
        outlineImage.enabled = false;
    }

    void ToggleIntroduction()
    {
        introductionEnabled = !introductionEnabled;
        if (!introductionEnabled) SelectPart(-1);
        introductionButton.image.color = introductionEnabled ? new Color(.80f, .88f, .86f) :
            new Color(.91f, .93f, .93f);
    }

    void BeginPartClick(bool overUI)
    {
        clickPending = !overUI;
        clickMoved = false;
        clickStart = Input.mousePosition;
    }

    bool PartClickMoved()
    {
        clickMoved |= Vector2.Distance(clickStart, Input.mousePosition) > 5;
        return clickMoved;
    }

    void EndPartClick(bool overUI)
    {
        if (clickPending && !overUI && !PartClickMoved())
            RegisterPartClick(PickPart(Input.mousePosition));
        else if (clickMoved) pendingPart = -1;
        clickPending = false;
    }

    void RegisterPartClick(int part)
    {
        if (part < 0)
        {
            pendingPart = -1;
            SelectPart(-1);
        }
        else if (pendingPart == part && Time.unscaledTime - pendingPartTime <= .65f)
        {
            introductionEnabled = true;
            SelectPart(part);
            pendingPart = -1;
        }
        else
        {
            pendingPart = part;
            pendingPartTime = Time.unscaledTime;
        }
    }

    int PickPart(Vector2 screen)
    {
        if (playing || progress < .9999f || !viewCamera.pixelRect.Contains(screen)) return -1;
        Physics.SyncTransforms();
        int index = -1;
        if (Physics.Raycast(viewCamera.ScreenPointToRay(screen), out RaycastHit hit, 100) &&
            pickParts.TryGetValue(hit.collider, out int found)) index = found;
        return index;
    }

    void SelectPart(int index)
    {
        if (!introductionPanel || selectedPart == index) return;
        selectedPart = index;
        introductionPanel.gameObject.SetActive(index >= 0 && introductionEnabled);
        if (index >= 0)
        {
            introductionTitle.text = (index + 1).ToString("00") + "  " + manifest.parts[index].label;
            introductionBody.text = PartDescriptions[index];
        }
        for (int i = 0; i < maskCopies.Count; i++)
            maskCopies[i].sharedMaterials = Enumerable.Repeat(
                maskParts[i] == index ? maskWhite : maskBlack,
                maskSources[i].sharedMaterials.Length).ToArray();
        if (outlineImage) outlineImage.enabled = index >= 0;
        if (maskCamera) maskCamera.enabled = index >= 0;
    }

    void UpdatePartInspection()
    {
        if (selectedPart < 0 || !maskCamera) return;
        int width = Mathf.Max(1, Mathf.RoundToInt(viewCamera.pixelWidth));
        int height = Mathf.Max(1, Mathf.RoundToInt(viewCamera.pixelHeight));
        if (!selectionTexture || selectionTexture.width != width || selectionTexture.height != height)
        {
            if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); }
            selectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                { name = "Visible selected component", antiAliasing = 1 };
            selectionTexture.Create();
            maskCamera.targetTexture = selectionTexture;
            outlineImage.texture = selectionTexture;
        }
        for (int i = 0; i < maskCopies.Count; i++)
        {
            Transform source = maskSources[i].transform, target = maskCopies[i].transform;
            target.SetPositionAndRotation(source.position, source.rotation);
            target.localScale = source.lossyScale;
        }
        maskCamera.transform.SetPositionAndRotation(viewCamera.transform.position, viewCamera.transform.rotation);
        maskCamera.orthographic = viewCamera.orthographic;
        maskCamera.orthographicSize = viewCamera.orthographicSize;
        maskCamera.aspect = viewCamera.aspect;
        maskCamera.nearClipPlane = viewCamera.nearClipPlane;
        maskCamera.farClipPlane = viewCamera.farClipPlane;
        RectTransform overlay = outlineImage.rectTransform;
        overlay.anchorMin = viewCamera.rect.min; overlay.anchorMax = viewCamera.rect.max;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
    }

    void DisposePartInspection()
    {
        if (maskCamera) maskCamera.targetTexture = null;
        if (selectionTexture) { selectionTexture.Release(); Destroy(selectionTexture); }
        if (maskRoot) Destroy(maskRoot);
        if (maskWhite) Destroy(maskWhite);
        if (maskBlack) Destroy(maskBlack);
        if (outlineMaterial) Destroy(outlineMaterial);
    }
}
