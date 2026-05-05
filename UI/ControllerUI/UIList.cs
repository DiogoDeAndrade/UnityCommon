using UC;
using UnityEngine;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine.InputSystem;

public class UIList : BaseUIControl
{
    public delegate void OnSelectItem(UIListItem item);
    public event OnSelectItem onItemSelect;

    [SerializeField] private RectTransform  containerTransform;
    [SerializeField, ShowIf(nameof(hasMouseControl))]
    private Hypertag        playerTag;
    [SerializeField, ShowIf(nameof(hasMouseControl))]
    private PlayerInput     playerInput;
    [SerializeField, ShowIf(nameof(hasMouseControl)), InputPlayer(nameof(playerInput))]
    private UC.InputControl  scrollControl;
    [SerializeField, ShowIf(nameof(hasMouseControl))]
    private float           scrollSpeed = 1.0f;

    private UIListProvider provider;
    private readonly List<UIListItem> items = new List<UIListItem>();

    public interface UIListProvider
    {
        int itemCount { get; }

        void        Refresh();
        UIListItem  CreateItem(int index, RectTransform container);
    }

    bool hasMouseControl
    {
        get
        {
            if (parentGroup == null) parentGroup = GetComponentInParent<UIGroup>();
            if (parentGroup == null) return false;

            return parentGroup.enableMouseSupport;
        }
    }

    protected override void Start()
    {
        base.Start();        

        provider = GetComponent<UIListProvider>();
        if (provider == null)
        {
            Debug.LogError($"UIList on '{name}' requires a component implementing UIListProvider.", this);
            return;
        }

        Refresh();

        if (playerInput == null)
            playerInput = playerTag.FindFirst<PlayerInput>();
        if (hasMouseControl)
        {
            if (scrollControl.needPlayerInput) scrollControl.playerInput = playerInput;
        }
    }

    public override void NotifyEnable()
    {
        base.NotifyEnable();
        Refresh();
    }

    public void Refresh()
    {
        Clear();
        if (provider == null) return;

        provider.Refresh();

        int count = provider.itemCount;
        for (int i = 0; i < count; i++)
        {
            UIListItem item = provider.CreateItem(i, containerTransform);
            if (item == null) continue;

            item.index = i;
            item.ownerList = this;
            items.Add(item);
        }

        containerTransform.anchoredPosition = Vector2.zero;
    }

    public void Clear()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                Destroy(items[i].gameObject);
        }
        items.Clear();
    }

    public void SelectItem(UIListItem item)
    {
        onItemSelect?.Invoke(item);
    }

    protected override void Update()
    {
        base.Update();

        if ((hasMouseControl) && (parentGroup.uiEnable))
        {
            // Only scroll when the cursor is actually over our viewport.
            Vector2 mousePos = UC.InputControl.GetScreenMousePosition();
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePos, cam))
                return;

            var viewport = containerTransform.parent as RectTransform;

            float scrollAmount = scrollControl.GetAxis() * scrollSpeed;
            if (scrollAmount == 0f) return;

            scrollAmount = -scrollAmount;

            // Clamp so the list can't be dragged past either end.
            float contentHeight = containerTransform.rect.height;
            float viewportHeight = viewport.rect.height;
            float maxY = Mathf.Max(0f, contentHeight - viewportHeight);

            Vector2 pos = containerTransform.anchoredPosition;
            pos.y = Mathf.Clamp(pos.y + scrollAmount, 0f, maxY);
            containerTransform.anchoredPosition = pos;
        }
    }
}
