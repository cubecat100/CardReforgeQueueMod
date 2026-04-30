#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CardReforgeQueueMod;

public static class RestSiteReforgeQueueUi
{
    private const string ConfirmUiName = "CardReforgeQueueRestSiteConfirm";
    private static readonly MethodInfo OwnerGetter = AccessTools.PropertyGetter(typeof(RestSiteOption), "Owner");
    private static readonly List<RestSiteReforgeQueueConfirmUi> ActiveConfirmUis = new();

    public static void EnsureInstalled(NRestSiteRoom room)
    {
        var smithOption = room.Options.OfType<SmithRestSiteOption>().FirstOrDefault();
        if (smithOption == null)
        {
            return;
        }

        var smithButton = room.GetButtonForOption(smithOption);
        if (smithButton == null)
        {
            return;
        }

        if (smithButton.GetNodeOrNull<RestSiteReforgeQueueConfirmUi>(ConfirmUiName) is { } existingUi)
        {
            existingUi.RefreshQueuePreview();
            return;
        }

        smithButton.AddChild(CreateConfirmUi(room, smithButton, smithOption));
    }

    public static void Register(RestSiteReforgeQueueConfirmUi ui)
    {
        if (ActiveConfirmUis.Contains(ui) == false)
        {
            ActiveConfirmUis.Add(ui);
        }
    }

    public static void Unregister(RestSiteReforgeQueueConfirmUi ui)
    {
        ActiveConfirmUis.Remove(ui);
    }

    public static void RefreshQueueDisplays(string? queuePath)
    {
        foreach (var ui in ActiveConfirmUis.ToArray())
        {
            if (ui.QueuePath == queuePath)
            {
                ui.RefreshQueuePreview();
            }
        }
    }

    private static Control CreateConfirmUi(NRestSiteRoom room, NRestSiteButton smithButton, SmithRestSiteOption smithOption)
    {
        var panel = new RestSiteReforgeQueueConfirmUi(room, smithButton)
        {
            Name = ConfirmUiName,
            CustomMinimumSize = new Vector2(220.0f, 116.0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 0,
        };

        panel.Position = new Vector2(0.0f, smithButton.Size.Y + 48.0f);
        panel.AddThemeStyleboxOverride("panel", RestSiteReforgeQueueConfirmUi.CreatePanelStyle());

        var root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220.0f, 116.0f),
        };
        root.AddThemeConstantOverride("separation", 4);
        panel.AddChild(root);

        panel.InitializeQueuePreview(root, smithOption);

        return panel;
    }

    public static Label CreateQueueLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(200.0f, 20.0f),
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        return label;
    }

    public static string? GetQueuePath(SmithRestSiteOption smithOption)
    {
        if (OwnerGetter.Invoke(smithOption, null) is not Player player)
        {
            return null;
        }

        return TopBarReforgeQueueUi.GetQueuePath(player);
    }

    public static IEnumerable<QueuedCardPreview> GetQueuedCards(SmithRestSiteOption smithOption)
    {
        if (OwnerGetter.Invoke(smithOption, null) is not Player player)
        {
            return System.Array.Empty<QueuedCardPreview>();
        }

        var queuePath = TopBarReforgeQueueUi.GetQueuePath(player);
        var cards = player.Deck.Cards.ToList();
        var queuedCards = new List<QueuedCardPreview>();

        foreach (var key in ReforgeQueueStorage.LoadKeys(queuePath))
        {
            var index = cards.FindIndex(card => ReforgeQueueCardRow.MatchesCardKey(card, key));
            if (index < 0)
            {
                continue;
            }

            queuedCards.Add(new QueuedCardPreview(key, cards[index].Title, 1));
            cards.RemoveAt(index);
        }

        return queuedCards
            .GroupBy(static item => item.Key)
            .Select(static group => new QueuedCardPreview(group.Key, group.First().Title, group.Count()));
    }

    public static void TryInstallAutoSelector(NDeckUpgradeSelectScreen screen, IReadOnlyList<CardModel> cards)
    {
        var player = cards.FirstOrDefault()?.Owner;
        var queuePath = TopBarReforgeQueueUi.GetQueuePath(player);
        if (string.IsNullOrEmpty(queuePath) == true)
        {
            return;
        }

        if (ReforgeQueueStorage.LoadAutoUpgradeEnabled(queuePath) == false)
        {
            return;
        }

        var card = GetTopQueuedCardInSelection(queuePath, cards);
        if (card == null)
        {
            return;
        }

        screen.AddChild(new UpgradeScreenAutoSelector(screen, card));
    }

    private static CardModel? GetTopQueuedCardInSelection(string? queuePath, IReadOnlyList<CardModel> cards)
    {
        foreach (var key in ReforgeQueueStorage.LoadKeys(queuePath))
        {
            var card = cards.FirstOrDefault(item =>
                ReforgeQueueCardRow.MatchesCardKey(item, key)
                && item.IsUpgradable == true
                && item.IsUpgraded == false);
            if (card != null)
            {
                return card;
            }
        }

        return null;
    }
}

public sealed partial class UpgradeScreenAutoSelector : Node
{
    private static readonly MethodInfo OnCardClickedMethod = AccessTools.Method(
        typeof(NDeckUpgradeSelectScreen),
        "OnCardClicked");
    private static readonly MethodInfo ConfirmSelectionMethod = AccessTools.Method(
        typeof(NDeckUpgradeSelectScreen),
        "ConfirmSelection");
    private readonly NDeckUpgradeSelectScreen screen;
    private readonly CardModel card;
    private int framesToWait = 1;

    public UpgradeScreenAutoSelector(NDeckUpgradeSelectScreen screen, CardModel card)
    {
        this.screen = screen;
        this.card = card;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta)
    {
        if (framesToWait > 0)
        {
            framesToWait -= 1;
            return;
        }

        OnCardClickedMethod.Invoke(screen, new object[] { card });
        ConfirmSelectionMethod.Invoke(screen, new object?[] { null });
        QueueFree();
    }
}

public readonly record struct QueuedCardPreview(string Key, string Title, int Count);

public sealed partial class RestSiteQueuePreviewList : VBoxContainer
{
    private readonly string? queuePath;
    private readonly List<string> fullQueueKeys;

    public RestSiteQueuePreviewList(string? queuePath, IEnumerable<string> queueKeys)
    {
        this.queuePath = queuePath;
        fullQueueKeys = queueKeys.ToList();
        MouseFilter = MouseFilterEnum.Stop;
        AddThemeConstantOverride("separation", 3);
    }

    public void AddItem(RestSiteQueuePreviewRow row)
    {
        AddChild(row);
    }

    public void MoveRow(RestSiteQueuePreviewRow draggedRow, RestSiteQueuePreviewRow targetRow, bool afterTarget)
    {
        var oldParent = draggedRow.GetParent();
        if (oldParent != this)
        {
            oldParent?.RemoveChild(draggedRow);
            AddChild(draggedRow);
        }

        var targetIndex = targetRow.GetIndex();
        if (afterTarget == true)
        {
            targetIndex += 1;
        }

        MoveChild(draggedRow, targetIndex);
        SavePreviewOrder();
    }

    private void SavePreviewOrder()
    {
        var previewKeys = GetChildren()
            .OfType<RestSiteQueuePreviewRow>()
            .SelectMany(static row => row.GetCardKeys())
            .ToArray();

        ReforgeQueueStorage.Save(queuePath, previewKeys.Concat(fullQueueKeys.Skip(previewKeys.Length)));
    }
}

public sealed partial class RestSiteQueuePreviewRow : PanelContainer
{
    private static RestSiteQueuePreviewRow? draggedRow;

    public string CardKey { get; }
    public int CardCount { get; }

    public RestSiteQueuePreviewRow(string cardKey, string title, int count = 1)
    {
        CardKey = cardKey;
        CardCount = count;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(200.0f, 20.0f);
        AddThemeStyleboxOverride("panel", CreateStyle());

        var label = new Label
        {
            Text = $"{title} x{CardCount}",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(190.0f, 20.0f),
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        AddChild(label);
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        draggedRow = this;
        var previewSize = new Vector2(200.0f, 20.0f);
        var previewRoot = new Control
        {
            CustomMinimumSize = Vector2.Zero,
            Size = Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore,
            TopLevel = true,
            ZAsRelative = false,
            ZIndex = 4096,
        };

        var preview = new RestSiteQueuePreviewRow(CardKey, GetTitle(), CardCount)
        {
            CustomMinimumSize = previewSize,
            Size = previewSize,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.78f),
            MouseFilter = MouseFilterEnum.Ignore,
            Position = -previewSize / 2.0f,
        };
        previewRoot.AddChild(preview);
        SetDragPreview(previewRoot);
        return CardKey;
    }

    public IEnumerable<string> GetCardKeys()
    {
        return Enumerable.Repeat(CardKey, CardCount);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return draggedRow != null
            && draggedRow != this
            && GetParent() is RestSiteQueuePreviewList;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (draggedRow == null || GetParent() is not RestSiteQueuePreviewList list)
        {
            return;
        }

        list.MoveRow(draggedRow, this, atPosition.Y > Size.Y / 2.0f);
        draggedRow = null;
    }

    private string GetTitle()
    {
        var text = GetChildren().OfType<Label>().FirstOrDefault()?.Text ?? CardKey;
        var suffix = $" x{CardCount}";
        return text.EndsWith(suffix, System.StringComparison.Ordinal) == true
            ? text[..^suffix.Length]
            : text;
    }

    private static StyleBoxFlat CreateStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.10f, 0.12f, 0.88f),
            BorderColor = new Color(0.42f, 0.42f, 0.46f, 1.0f),
            ContentMarginLeft = 4.0f,
            ContentMarginRight = 4.0f,
            ContentMarginTop = 2.0f,
            ContentMarginBottom = 2.0f,
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(3);
        return style;
    }
}

public sealed partial class RestSiteReforgeQueueConfirmUi : PanelContainer
{
    private readonly NRestSiteRoom room;
    private readonly NRestSiteButton smithButton;
    private VBoxContainer? root;
    private SmithRestSiteOption? smithOption;
    private Control? queuePreview;

    public string? QueuePath { get; private set; }

    public RestSiteReforgeQueueConfirmUi(NRestSiteRoom room, NRestSiteButton smithButton)
    {
        this.room = room;
        this.smithButton = smithButton;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void InitializeQueuePreview(VBoxContainer previewRoot, SmithRestSiteOption option)
    {
        root = previewRoot;
        smithOption = option;
        QueuePath = RestSiteReforgeQueueUi.GetQueuePath(option);
        RefreshQueuePreview();
    }

    public override void _EnterTree()
    {
        RestSiteReforgeQueueUi.Register(this);
    }

    public override void _ExitTree()
    {
        RestSiteReforgeQueueUi.Unregister(this);
    }

    public override void _Process(double delta)
    {
        Visible = IsActuallyVisible(room) == true
            && IsActuallyVisible(smithButton) == true;
    }

    public void RefreshQueuePreview()
    {
        if (root == null || smithOption == null)
        {
            return;
        }

        if (queuePreview != null)
        {
            root.RemoveChild(queuePreview);
            queuePreview.QueueFree();
            queuePreview = null;
        }

        QueuePath = RestSiteReforgeQueueUi.GetQueuePath(smithOption);
        var queuedCards = RestSiteReforgeQueueUi.GetQueuedCards(smithOption).ToArray();
        if (queuedCards.Length == 0)
        {
            var emptyLabel = RestSiteReforgeQueueUi.CreateQueueLabel("Queue is empty");
            emptyLabel.Modulate = new Color(0.72f, 0.72f, 0.76f, 0.85f);
            root.AddChild(emptyLabel);
            queuePreview = emptyLabel;
            return;
        }

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(200.0f, 72.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(scroll);
        queuePreview = scroll;

        var previewList = new RestSiteQueuePreviewList(
            QueuePath,
            queuedCards.SelectMany(static item => Enumerable.Repeat(item.Key, item.Count)))
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(previewList);

        foreach (var item in queuedCards)
        {
            previewList.AddItem(new RestSiteQueuePreviewRow(item.Key, item.Title, item.Count));
        }
    }

    public static StyleBoxFlat CreatePanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.035f, 0.045f, 0.90f),
            BorderColor = new Color(0.70f, 0.56f, 0.28f, 1.0f),
            ContentMarginLeft = 6.0f,
            ContentMarginRight = 6.0f,
            ContentMarginTop = 4.0f,
            ContentMarginBottom = 4.0f,
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        return style;
    }

    private static bool IsActuallyVisible(CanvasItem item)
    {
        var current = item;
        while (current != null)
        {
            if (current.Visible == false)
            {
                return false;
            }

            current = current.GetParent() as CanvasItem;
        }

        return true;
    }
}
