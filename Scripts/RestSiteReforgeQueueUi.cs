#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CardReforgeQueueMod;

public static class RestSiteReforgeQueueUi
{
    private const string ConfirmUiName = "CardReforgeQueueRestSiteConfirm";
    private static readonly MethodInfo OwnerGetter = AccessTools.PropertyGetter(typeof(RestSiteOption), "Owner");
    private static readonly FieldInfo SmithSelectionField = AccessTools.Field(typeof(SmithRestSiteOption), "_selection");
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

        panel.Position = new Vector2(0.0f, smithButton.Size.Y + 32.0f);
        panel.AddThemeStyleboxOverride("panel", RestSiteReforgeQueueConfirmUi.CreatePanelStyle());

        var root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220.0f, 116.0f),
        };
        root.AddThemeConstantOverride("separation", 4);
        panel.AddChild(root);

        var checkbox = new CheckBox
        {
            Text = "Auto upgrade",
            ButtonPressed = true,
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(200.0f, 28.0f),
        };
        checkbox.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(checkbox);

        panel.InitializeQueuePreview(root, smithOption, checkbox);

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

    public static bool TryCreateAutoUpgradeTask(SmithRestSiteOption smithOption, out Task<bool> task)
    {
        task = Task.FromResult(false);
        if (IsAutoUpgradeEnabled(smithOption) == false)
        {
            return false;
        }

        if (TryGetTopQueuedCard(smithOption, out var card, out var queuePath, out var cardKey) == false)
        {
            return false;
        }

        if (GetOwner(smithOption) is not { } player)
        {
            return false;
        }

        SmithSelectionField.SetValue(smithOption, new[] { card });
        task = AutoUpgradeQueuedCard(player, card, queuePath, cardKey);
        return true;
    }

    private static bool IsAutoUpgradeEnabled(SmithRestSiteOption smithOption)
    {
        var queuePath = GetQueuePath(smithOption);
        return ActiveConfirmUis
            .FirstOrDefault(ui => ui.QueuePath == queuePath)?
            .IsAutoUpgradeEnabled == true;
    }

    private static bool TryGetTopQueuedCard(
        SmithRestSiteOption smithOption,
        out CardModel card,
        out string? queuePath,
        out string cardKey)
    {
        queuePath = GetQueuePath(smithOption);
        cardKey = string.Empty;
        card = null!;

        var keys = ReforgeQueueStorage.LoadKeys(queuePath).ToArray();
        if (keys.Length == 0)
        {
            return false;
        }

        if (GetOwner(smithOption) is not { } player)
        {
            return false;
        }

        var cards = player.Deck.Cards.ToList();
        foreach (var key in keys)
        {
            var index = cards.FindIndex(item =>
                ReforgeQueueCardRow.MatchesCardKey(item, key)
                && item.IsUpgradable == true
                && item.IsUpgraded == false);
            if (index < 0)
            {
                continue;
            }

            card = cards[index];
            cardKey = key;
            return true;
        }

        return false;
    }

    private static async Task<bool> AutoUpgradeQueuedCard(Player player, CardModel card, string? queuePath, string cardKey)
    {
        CardCmd.Upgrade(card, CardPreviewStyle.None);
        await Hook.AfterRestSiteSmith(player.RunState, player);
        return true;
    }

    private static Player? GetOwner(SmithRestSiteOption smithOption)
    {
        return OwnerGetter.Invoke(smithOption, null) as Player;
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
    private CheckBox? autoUpgradeCheckbox;
    private VBoxContainer? root;
    private SmithRestSiteOption? smithOption;
    private Control? queuePreview;

    public string? QueuePath { get; private set; }
    public bool IsAutoUpgradeEnabled => autoUpgradeCheckbox?.ButtonPressed == true;

    public RestSiteReforgeQueueConfirmUi(NRestSiteRoom room, NRestSiteButton smithButton)
    {
        this.room = room;
        this.smithButton = smithButton;
        ProcessMode = ProcessModeEnum.Always;
    }

    public void InitializeQueuePreview(VBoxContainer previewRoot, SmithRestSiteOption option, CheckBox checkbox)
    {
        autoUpgradeCheckbox = checkbox;
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
