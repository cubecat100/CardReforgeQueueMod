#nullable enable
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CardReforgeQueueMod;

public static class TopBarReforgeQueueUi
{
    private const string ButtonName = "CardReforgeQueueButton";
    private const string PopupName = "CardReforgeQueuePopup";
    private const string IconRelativePath = "Source/icon.png";
    private static readonly FieldInfo PlayerField = AccessTools.Field(typeof(NTopBar), "_player");

    public static void EnsureInstalled(NTopBar topBar)
    {
        if (topBar.Map == null)
        {
            return;
        }

        var parent = topBar.Map.GetParent();
        if (parent == null)
        {
            return;
        }

        if (parent.GetNodeOrNull<Button>(ButtonName) != null)
        {
            return;
        }

        var button = CreateButton(topBar);
        parent.AddChild(button);

        var mapIndex = topBar.Map.GetIndex();
        parent.MoveChild(button, Math.Max(0, mapIndex));
    }

    public static string? GetQueuePath(Player? player)
    {
        var assemblyDirectory = GetAssemblyDirectory();
        if (string.IsNullOrEmpty(assemblyDirectory) == true)
        {
            return null;
        }

        var mode = SanitizePathPart(player?.RunState.GameMode.ToString() ?? "UnknownMode");
        var character = SanitizePathPart(player?.Character.Id.Entry ?? "UnknownCharacter");
        var playerId = player?.NetId.ToString() ?? "UnknownPlayer";

        return Path.Combine(assemblyDirectory, "Source", "queues", mode, $"{character}_{playerId}.txt");
    }

    private static Button CreateButton(NTopBar topBar)
    {
        var button = new Button
        {
            Name = ButtonName,
            Text = string.Empty,
            TooltipText = "Unupgraded cards",
            CustomMinimumSize = new Vector2(60.0f, 36.0f),
            FocusMode = Control.FocusModeEnum.All,
            ExpandIcon = true,
        };

        var icon = LoadButtonIcon();
        if (icon != null)
        {
            button.Icon = icon;
        }

        button.Pressed += () => TogglePopup(topBar);
        return button;
    }

    private static Texture2D? LoadButtonIcon()
    {
        var assemblyDirectory = GetAssemblyDirectory();
        if (string.IsNullOrEmpty(assemblyDirectory) == true)
        {
            return null;
        }

        var iconPath = Path.Combine(assemblyDirectory, IconRelativePath);
        if (File.Exists(iconPath) == false)
        {
            return null;
        }

        var image = Image.LoadFromFile(iconPath);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }

    private static string? GetAssemblyDirectory()
    {
        return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    private static string SanitizePathPart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) == true ? "Unknown" : sanitized;
    }

    private static void TogglePopup(NTopBar topBar)
    {
        var existing = topBar.GetNodeOrNull<Control>(PopupName);
        if (existing != null)
        {
            existing.QueueFree();
            return;
        }

        var popup = CreatePopup(topBar);
        topBar.AddChild(popup);
    }

    public static void ClosePopupFrom(Node node)
    {
        var current = node;
        while (current != null)
        {
            var popup = FindDescendantByName<Control>(current, PopupName);
            if (popup != null)
            {
                popup.QueueFree();
                return;
            }

            current = current.GetParent();
        }
    }

    private static Control CreatePopup(NTopBar topBar)
    {
        var popup = new PanelContainer
        {
            Name = PopupName,
            CustomMinimumSize = new Vector2(560.0f, 440.0f),
            Size = new Vector2(560.0f, 440.0f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 200,
        };

        popup.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        popup.Position = new Vector2(-590.0f, 72.0f);
        popup.AddThemeStyleboxOverride("panel", CreatePopupStyle());

        var player = GetPlayer(topBar);
        var queuePath = GetQueuePath(player);

        var root = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(560.0f, 440.0f),
            Size = new Vector2(560.0f, 440.0f),
        };
        root.AddThemeConstantOverride("separation", 8);
        popup.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);

        var title = new Label
        {
            Text = "Upgrade Queue",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        header.AddChild(title);

        var autoUpgradeCheckbox = new CheckBox
        {
            Text = "Auto upgrade",
            ButtonPressed = ReforgeQueueStorage.LoadAutoUpgradeEnabled(queuePath),
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(130.0f, 30.0f),
        };
        autoUpgradeCheckbox.AddThemeFontSizeOverride("font_size", 11);
        autoUpgradeCheckbox.Pressed += () =>
            ReforgeQueueStorage.SaveAutoUpgradeEnabled(queuePath, autoUpgradeCheckbox.ButtonPressed);
        header.AddChild(autoUpgradeCheckbox);

        var closeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(34.0f, 30.0f),
        };
        closeButton.Pressed += popup.QueueFree;
        header.AddChild(closeButton);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
        };
        body.AddThemeConstantOverride("separation", 8);
        root.AddChild(body);

        var queueList = new ReforgeQueueDropList("Queue", isQueueList: true, queuePath)
        {
            CustomMinimumSize = new Vector2(260.0f, 376.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
        };
        body.AddChild(queueList);

        var cardList = new ReforgeQueueDropList("Cards", isQueueList: false, queuePath)
        {
            CustomMinimumSize = new Vector2(260.0f, 376.0f),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
        };
        body.AddChild(cardList);

        var cards = GetUnupgradedCards(player).ToList();
        if (cards.Count == 0)
        {
            cardList.AddItem(new Label
            {
                Text = "No upgradable unupgraded cards.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            return popup;
        }

        title.Text += $" ({cards.Count})";

        AddGroupedRows(queueList, ReforgeQueueStorage.TakeQueuedCards(cards, queuePath), sortByTitle: false);
        AddGroupedRows(cardList, cards, sortByTitle: true);

        return popup;
    }

    public static void AddGroupedRows(ReforgeQueueDropList list, IEnumerable<CardModel> cards, bool sortByTitle)
    {
        var groups = cards.GroupBy(ReforgeQueueCardRow.GetCardKey);
        if (sortByTitle == true)
        {
            groups = groups.OrderBy(static group => group.First().Title, StringComparer.Ordinal);
        }

        foreach (var group in groups)
        {
            list.AddItem(new ReforgeQueueCardRow(group.ToArray()));
        }
    }

    private static StyleBoxFlat CreatePopupStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.03f, 0.035f, 0.045f, 0.94f),
            BorderColor = new Color(0.34f, 0.34f, 0.38f, 1.0f),
            ContentMarginLeft = 10.0f,
            ContentMarginRight = 10.0f,
            ContentMarginTop = 8.0f,
            ContentMarginBottom = 10.0f,
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        return style;
    }

    private static Player? GetPlayer(NTopBar topBar)
    {
        return PlayerField.GetValue(topBar) as Player;
    }

    private static IEnumerable<CardModel> GetUnupgradedCards(Player? player)
    {
        if (player == null)
        {
            return Array.Empty<CardModel>();
        }

        return player.Deck.Cards
            .Where(static card => card.IsUpgradable == true)
            .Where(static card => card.IsUpgraded == false)
            .OrderBy(static card => card.Title, StringComparer.Ordinal);
    }

    private static T? FindDescendantByName<T>(Node node, string name)
        where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T typedChild && child.Name == name)
            {
                return typedChild;
            }

            var descendant = FindDescendantByName<T>(child, name);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }
}

public static class ReforgeQueueStorage
{
    public static List<CardModel> TakeQueuedCards(List<CardModel> cards, string? queuePath)
    {
        var queuedCards = new List<CardModel>();
        foreach (var key in LoadKeys(queuePath))
        {
            var index = cards.FindIndex(card => ReforgeQueueCardRow.MatchesCardKey(card, key));
            if (index < 0)
            {
                continue;
            }

            queuedCards.Add(cards[index]);
            cards.RemoveAt(index);
        }

        return queuedCards;
    }

    public static void Save(string? queuePath, IEnumerable<string> keys)
    {
        if (string.IsNullOrEmpty(queuePath) == true)
        {
            return;
        }

        var directory = Path.GetDirectoryName(queuePath);
        if (string.IsNullOrEmpty(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllLines(queuePath, keys);
        ReforgeQueueDropList.RefreshQueueDisplays(queuePath);
        RestSiteReforgeQueueUi.RefreshQueueDisplays(queuePath);
    }

    public static void RemoveFirst(string? queuePath, string key)
    {
        var keys = LoadKeys(queuePath).ToList();
        var index = keys.FindIndex(item => item == key);
        if (index < 0)
        {
            return;
        }

        keys.RemoveAt(index);
        Save(queuePath, keys);
    }

    public static bool LoadAutoUpgradeEnabled(string? queuePath)
    {
        var settingsPath = GetAutoUpgradeSettingsPath(queuePath);
        if (string.IsNullOrEmpty(settingsPath) == true || File.Exists(settingsPath) == false)
        {
            return true;
        }

        var value = File.ReadAllText(settingsPath).Trim();
        return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) == false;
    }

    public static void SaveAutoUpgradeEnabled(string? queuePath, bool enabled)
    {
        var settingsPath = GetAutoUpgradeSettingsPath(queuePath);
        if (string.IsNullOrEmpty(settingsPath) == true)
        {
            return;
        }

        var directory = Path.GetDirectoryName(settingsPath);
        if (string.IsNullOrEmpty(directory) == false)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsPath, enabled ? "true" : "false");
    }

    public static IEnumerable<string> LoadKeys(string? queuePath)
    {
        if (string.IsNullOrEmpty(queuePath) == true || File.Exists(queuePath) == false)
        {
            return Array.Empty<string>();
        }

        return File.ReadAllLines(queuePath)
            .Where(static line => string.IsNullOrWhiteSpace(line) == false)
            .Select(static line => line.Trim())
            .ToArray();
    }

    private static string? GetAutoUpgradeSettingsPath(string? queuePath)
    {
        return string.IsNullOrEmpty(queuePath) == true
            ? null
            : $"{queuePath}.auto";
    }
}

public sealed partial class ReforgeQueueDropList : PanelContainer
{
    private static readonly List<ReforgeQueueDropList> ActiveLists = new();
    private readonly VBoxContainer content = new();
    private readonly Label emptyLabel = new();
    private readonly bool isQueueList;
    private readonly string? queuePath;

    public ReforgeQueueDropList(string title, bool isQueueList, string? queuePath)
    {
        this.isQueueList = isQueueList;
        this.queuePath = queuePath;
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.Fill;

        AddThemeStyleboxOverride("panel", CreateStyle());

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        var label = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(0.0f, 22.0f),
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(label);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0.0f, 330.0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
            ClipContents = true,
        };
        root.AddChild(scroll);

        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        content.SizeFlagsVertical = SizeFlags.Fill;
        content.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(content);

        emptyLabel.Text = isQueueList == true ? "Drop cards here" : "No cards";
        emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
        emptyLabel.VerticalAlignment = VerticalAlignment.Center;
        emptyLabel.Modulate = new Color(0.72f, 0.72f, 0.76f, 0.75f);
        emptyLabel.CustomMinimumSize = new Vector2(0.0f, 48.0f);
        emptyLabel.AddThemeFontSizeOverride("font_size", 11);
        content.AddChild(emptyLabel);
    }

    public override void _EnterTree()
    {
        if (ActiveLists.Contains(this) == false)
        {
            ActiveLists.Add(this);
        }
    }

    public override void _ExitTree()
    {
        ActiveLists.Remove(this);
    }

    public void AddItem(Control item)
    {
        if (emptyLabel.GetParent() == content)
        {
            content.RemoveChild(emptyLabel);
        }

        content.AddChild(item);
        UpdateEmptyLabel();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return ReforgeQueueCardRow.DraggedRow != null;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var draggedRow = ReforgeQueueCardRow.DraggedRow;
        if (draggedRow == null)
        {
            return;
        }

        var oldParent = draggedRow.GetParent();
        var oldList = FindDropList(oldParent);
        oldParent?.RemoveChild(draggedRow);
        content.AddChild(draggedRow);
        oldList?.UpdateEmptyLabel();
        UpdateEmptyLabel();
        SaveQueue();
        ReforgeQueueCardRow.ClearDraggedRow();
    }

    public void SaveQueue()
    {
        var queueList = FindQueueList(this);
        if (queueList == null)
        {
            return;
        }

        ReforgeQueueStorage.Save(queueList.queuePath, queueList.GetCardKeys());
    }

    public static void RefreshQueueDisplays(string? queuePath)
    {
        foreach (var popup in ActiveLists
            .Where(list => list.queuePath == queuePath)
            .Select(FindPopupRoot)
            .Where(static popup => popup != null)
            .Distinct()
            .ToArray())
        {
            RefreshPopupQueue(popup!, queuePath);
        }
    }

    private IEnumerable<string> GetCardKeys()
    {
        return content.GetChildren()
            .OfType<ReforgeQueueCardRow>()
            .SelectMany(static row => row.GetCardKeys());
    }

    public void UpdateEmptyLabel()
    {
        var hasCardRows = content.GetChildren().OfType<ReforgeQueueCardRow>().Any();
        if (hasCardRows == false && emptyLabel.GetParent() == null)
        {
            content.AddChild(emptyLabel);
        }
        else if (hasCardRows == true && emptyLabel.GetParent() == content)
        {
            content.RemoveChild(emptyLabel);
        }
    }

    private static void RefreshPopupQueue(Node popup, string? queuePath)
    {
        var lists = FindDescendants<ReforgeQueueDropList>(popup).ToArray();
        var queueList = lists.FirstOrDefault(static list => list.isQueueList == true);
        var cardList = lists.FirstOrDefault(static list => list.isQueueList == false);
        if (queueList == null || cardList == null)
        {
            return;
        }

        var detachedRows = queueList.DetachCardRows()
            .Concat(cardList.DetachCardRows())
            .ToList();
        var cards = detachedRows
            .SelectMany(static row => row.Cards)
            .Where(static card => card.IsUpgradable == true && card.IsUpgraded == false)
            .ToList();

        foreach (var row in detachedRows)
        {
            row.QueueFree();
        }

        var queuedCards = new List<CardModel>();
        foreach (var key in ReforgeQueueStorage.LoadKeys(queuePath))
        {
            var index = cards.FindIndex(card => ReforgeQueueCardRow.MatchesCardKey(card, key));
            if (index < 0)
            {
                continue;
            }

            queuedCards.Add(cards[index]);
            cards.RemoveAt(index);
        }

        TopBarReforgeQueueUi.AddGroupedRows(queueList, queuedCards, sortByTitle: false);
        TopBarReforgeQueueUi.AddGroupedRows(cardList, cards, sortByTitle: true);
        queueList.UpdateEmptyLabel();
        cardList.UpdateEmptyLabel();
    }

    private IEnumerable<ReforgeQueueCardRow> DetachCardRows()
    {
        var rows = content.GetChildren().OfType<ReforgeQueueCardRow>().ToArray();
        foreach (var row in rows)
        {
            content.RemoveChild(row);
        }

        UpdateEmptyLabel();
        return rows;
    }

    private static ReforgeQueueDropList? FindQueueList(Node node)
    {
        var popup = FindPopupRoot(node);
        return popup == null
            ? null
            : FindDescendants<ReforgeQueueDropList>(popup).FirstOrDefault(static list => list.isQueueList == true);
    }

    private static ReforgeQueueDropList? FindDropList(Node? node)
    {
        var current = node;
        while (current != null)
        {
            if (current is ReforgeQueueDropList list)
            {
                return list;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static Node? FindPopupRoot(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current.Name == "CardReforgeQueuePopup")
            {
                return current;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static IEnumerable<T> FindDescendants<T>(Node node)
        where T : Node
    {
        foreach (var child in node.GetChildren())
        {
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static StyleBoxFlat CreateStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.06f, 0.82f),
            BorderColor = new Color(0.42f, 0.42f, 0.46f, 1.0f),
            ContentMarginLeft = 8.0f,
            ContentMarginRight = 8.0f,
            ContentMarginTop = 8.0f,
            ContentMarginBottom = 8.0f,
        };

        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        return style;
    }
}

public sealed partial class ReforgeQueueCardRow : PanelContainer
{
    private readonly List<CardModel> cards;
    private static ReforgeQueueCardRow? draggedRow;
    private const float DefaultPreviewWidth = 244.0f;
    private const float DefaultPreviewHeight = 56.0f;

    public string CardKey { get; }
    public IReadOnlyList<CardModel> Cards => cards;
    public int CardCount => cards.Count;
    public static ReforgeQueueCardRow? DraggedRow => draggedRow;

    public ReforgeQueueCardRow(CardModel card)
        : this(new[] { card })
    {
    }

    public ReforgeQueueCardRow(IEnumerable<CardModel> cards)
    {
        this.cards = cards.ToList();
        if (this.cards.Count == 0)
        {
            throw new ArgumentException("Card row requires at least one card.", nameof(cards));
        }

        var card = this.cards[0];
        CardKey = GetCardKey(card);
        Name = $"CardReforgeQueueRow_{CardKey}";
        MouseFilter = MouseFilterEnum.Stop;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        CustomMinimumSize = new Vector2(0.0f, 48.0f);

        AddThemeStyleboxOverride("panel", CreateStyle(card.Type));

        var outer = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0.0f, 48.0f),
        };
        outer.AddThemeConstantOverride("separation", 2);
        AddChild(outer);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 6);
        outer.AddChild(row);

        row.AddChild(new Label
        {
            Text = card.Title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var upgradeLabel = new Label
        {
            Text = $"x{CardCount}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(40.0f, 0.0f),
        };
        upgradeLabel.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(upgradeLabel);

        if (card.Enchantment != null)
        {
            var enchantmentText = card.Enchantment.Title.GetFormattedText();
            if (string.IsNullOrWhiteSpace(enchantmentText) == false)
            {
                var enchantmentLabel = new Label
                {
                    Text = enchantmentText,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    Modulate = new Color(0.92f, 0.88f, 0.70f, 1.0f),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                };
                enchantmentLabel.AddThemeFontSizeOverride("font_size", 10);
                outer.AddChild(enchantmentLabel);
            }
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        draggedRow = this;
        var preview = CreateDragPreview();
        SetDragPreview(preview);

        return Name.ToString();
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return draggedRow != null && draggedRow != this && GetParent() != null;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (draggedRow == null || draggedRow == this)
        {
            return;
        }

        var parent = GetParent();
        if (parent == null)
        {
            return;
        }

        var oldParent = draggedRow.GetParent();
        var oldList = FindContainingDropList(oldParent);
        if (oldParent != parent)
        {
            oldParent?.RemoveChild(draggedRow);
            parent.AddChild(draggedRow);
        }

        var targetIndex = GetIndex();
        if (atPosition.Y > Size.Y / 2.0f)
        {
            targetIndex += 1;
        }

        parent.MoveChild(draggedRow, targetIndex);
        oldList?.UpdateEmptyLabel();
        var newList = FindContainingDropList(parent);
        newList?.UpdateEmptyLabel();
        newList?.SaveQueue();
        ClearDraggedRow();
    }

    public static void ClearDraggedRow()
    {
        draggedRow = null;
    }

    public IEnumerable<string> GetCardKeys()
    {
        return cards.Select(GetCardKey);
    }

    public static string GetCardKey(CardModel card)
    {
        var enchantmentKey = card.Enchantment == null
            ? "none"
            : string.IsNullOrWhiteSpace(card.Enchantment.Id.Entry) == false
                ? card.Enchantment.Id.Entry
                : card.Enchantment.Title.GetFormattedText();
        return $"{GetBaseCardKey(card)}|enchantment:{enchantmentKey}";
    }

    public static bool MatchesCardKey(CardModel card, string key)
    {
        return GetCardKey(card) == key || GetBaseCardKey(card) == key;
    }

    private static string GetBaseCardKey(CardModel card)
    {
        return string.IsNullOrWhiteSpace(card.Id.Entry) == false
            ? card.Id.Entry
            : card.Title;
    }

    private Control CreateDragPreview()
    {
        var card = cards[0];
        var previewSize = new Vector2(DefaultPreviewWidth, DefaultPreviewHeight);
        var previewRoot = new Control
        {
            CustomMinimumSize = Vector2.Zero,
            Size = Vector2.Zero,
            MouseFilter = MouseFilterEnum.Ignore,
            TopLevel = true,
            ZAsRelative = false,
            ZIndex = 4096,
        };

        var preview = new PanelContainer
        {
            CustomMinimumSize = previewSize,
            Size = previewSize,
            ClipContents = true,
            Modulate = new Color(1.0f, 1.0f, 1.0f, 0.78f),
            MouseFilter = MouseFilterEnum.Ignore,
            Position = -previewSize / 2.0f,
        };
        preview.AddThemeStyleboxOverride("panel", CreateStyle(card.Type));

        var row = new HBoxContainer
        {
            CustomMinimumSize = previewSize,
            Size = previewSize,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
        };
        row.AddThemeConstantOverride("separation", 6);
        preview.AddChild(row);

        row.AddChild(new Label
        {
            Text = card.Title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        });

        var upgradeLabel = new Label
        {
            Text = $"x{CardCount}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(40.0f, 0.0f),
        };
        upgradeLabel.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(upgradeLabel);

        previewRoot.AddChild(preview);
        return previewRoot;
    }

    private static ReforgeQueueDropList? FindContainingDropList(Node? node)
    {
        var current = node;
        while (current != null)
        {
            if (current is ReforgeQueueDropList list)
            {
                return list;
            }

            current = current.GetParent();
        }

        return null;
    }

    private static StyleBoxFlat CreateStyle(CardType type)
    {
        var style = new StyleBoxFlat
        {
            BgColor = GetBackgroundColor(type),
            BorderColor = GetBorderColor(type),
            ContentMarginLeft = 8.0f,
            ContentMarginRight = 8.0f,
            ContentMarginTop = 6.0f,
            ContentMarginBottom = 6.0f,
        };

        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(4);
        return style;
    }

    private static Color GetBackgroundColor(CardType type)
    {
        return type switch
        {
            CardType.Attack => new Color(0.38f, 0.10f, 0.08f, 0.88f),
            CardType.Skill => new Color(0.10f, 0.25f, 0.15f, 0.88f),
            CardType.Power => new Color(0.18f, 0.13f, 0.36f, 0.88f),
            _ => new Color(0.14f, 0.14f, 0.14f, 0.88f),
        };
    }

    private static Color GetBorderColor(CardType type)
    {
        return type switch
        {
            CardType.Attack => new Color(0.85f, 0.28f, 0.20f, 1.0f),
            CardType.Skill => new Color(0.30f, 0.72f, 0.42f, 1.0f),
            CardType.Power => new Color(0.54f, 0.42f, 0.95f, 1.0f),
            _ => new Color(0.62f, 0.62f, 0.62f, 1.0f),
        };
    }
}
