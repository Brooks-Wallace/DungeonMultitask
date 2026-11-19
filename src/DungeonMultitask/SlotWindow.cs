// Copyright (c) 2026 Brooks-Wallace. MIT License.
// Window chrome adapted from falorfrozen Multitask 0.2.2 (MIT), commit c69867e0.

using System.Collections.Generic;
using System.Linq;
using CheatMenu;
using Godot;

namespace DungeonMultitask;

/// <summary>The F5 window: earned capacity, the next dungeon step, and what is running together.</summary>
public sealed class SlotWindow
{
    readonly CanvasLayer _layer = new() { Layer = 118, Name = "DungeonMultitask", Visible = false };
    readonly Label _status = Style.Label("", Style.Muted);
    readonly Label _summary = Style.Label("", Style.Text);
    readonly Label _next = Style.Label("", Style.Muted, 13);
    readonly Button _enabled = new() { ToggleMode = true, CustomMinimumSize = new Vector2(70, 0) };
    readonly Button _fewer = new() { Text = "Fewer" };
    readonly Button _more = new() { Text = "More" };
    readonly VBoxContainer _running = new();
    readonly VBoxContainer _dungeons = new();
    string _dungeonKey = "";
    bool _syncing;

    public bool Visible => _layer.Visible;

    public SlotWindow(SceneTree tree)
    {
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.25f), MouseFilter = Control.MouseFilterEnum.Ignore };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(shade);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _layer.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(780, 0), Theme = Style.Build() };
        StyleBoxFlat frame = Style.Box(Style.Window, 8, Style.AccentDim, 1);
        frame.ShadowColor = new Color(0, 0, 0, 0.55f);
        frame.ShadowSize = 14;
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 0);
        panel.AddChild(root);

        StyleBoxFlat headBox = Style.Box(Style.Sidebar, 0, null, 0, 18, 12);
        headBox.CornerRadiusTopLeft = headBox.CornerRadiusTopRight = 7;
        headBox.BorderColor = Style.Line;
        headBox.BorderWidthBottom = 1;
        var head = new PanelContainer();
        head.AddThemeStyleboxOverride("panel", headBox);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(Style.Label("DUNGEON MULTITASK", Style.Accent, 20));
        Label hint = Style.Label("Clear dungeons to keep more actions running.", Style.Muted);
        hint.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hint.VerticalAlignment = VerticalAlignment.Center;
        hint.ClipText = true;
        header.AddChild(hint);
        var close = new Button { Text = "Close (F5)" };
        close.Pressed += Hide;
        header.AddChild(close);
        head.AddChild(header);
        root.AddChild(head);

        var margin = new MarginContainer();
        foreach (string m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(m, 16);
        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 10);
        margin.AddChild(body);
        root.AddChild(margin);

        var switchCard = new PanelContainer();
        switchCard.AddThemeStyleboxOverride("panel", Style.Box(Style.Card, 5, null, 0, 12, 10));
        var switchCol = new VBoxContainer();
        switchCol.AddThemeConstantOverride("separation", 8);
        var switchRow = new HBoxContainer();
        switchRow.AddThemeConstantOverride("separation", 10);
        switchRow.AddChild(Style.Label("Earned slots", Style.Text));
        StyleBoxFlat on = Style.Box(Style.GoodDim, 5, Style.Good, 1, 12, 5);
        _enabled.AddThemeStyleboxOverride("pressed", on);
        _enabled.AddThemeStyleboxOverride("hover_pressed", on);
        _enabled.TooltipText = "ON: use the actions your dungeons have earned. OFF: one action at a time.";
        _enabled.Toggled += pressed => { if (!_syncing) Slots.SetEnabled(pressed); };
        switchRow.AddChild(_enabled);
        switchRow.AddChild(_fewer);
        switchRow.AddChild(_more);
        _fewer.TooltipText = "Run fewer actions than you have earned.";
        _more.TooltipText = "Use one more of the actions you have earned.";
        _fewer.Pressed += () => { if (!_syncing && Slots.Capacity > 1) Slots.SetLimit(Slots.Capacity - 1); };
        _more.Pressed += () =>
        {
            if (_syncing || Slots.Capacity >= Slots.Earned) return;
            int next = Slots.Capacity + 1;
            Slots.SetLimit(next >= Slots.Earned ? 0 : next);
        };
        _summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _summary.HorizontalAlignment = HorizontalAlignment.Right;
        switchRow.AddChild(_summary);
        switchCol.AddChild(switchRow);
        _next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        switchCol.AddChild(_next);
        switchCard.AddChild(switchCol);
        body.AddChild(switchCard);

        var listCard = new PanelContainer();
        listCard.AddThemeStyleboxOverride("panel", Style.Box(Style.Card, 5, null, 0, 12, 10));
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        list.AddChild(Style.Label("Running together right now (your latest first):", Style.Muted, 12));
        _running.AddThemeConstantOverride("separation", 4);
        list.AddChild(_running);
        listCard.AddChild(list);
        body.AddChild(listCard);

        var dungeonCard = new PanelContainer();
        dungeonCard.AddThemeStyleboxOverride("panel", Style.Box(Style.Card, 5, null, 0, 12, 10));
        var dungeonCol = new VBoxContainer();
        dungeonCol.AddThemeConstantOverride("separation", 6);
        dungeonCol.AddChild(Style.Label("Standard dungeons. A first clear adds an action. 10 clears masters it.", Style.Muted, 12));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 260) };
        _dungeons.AddThemeConstantOverride("separation", 3);
        _dungeons.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(_dungeons);
        dungeonCol.AddChild(scroll);
        dungeonCard.AddChild(dungeonCol);
        body.AddChild(dungeonCard);

        StyleBoxFlat footBox = Style.Box(Style.Sidebar, 0, null, 0, 18, 8);
        footBox.CornerRadiusBottomLeft = footBox.CornerRadiusBottomRight = 7;
        footBox.BorderColor = Style.Line;
        footBox.BorderWidthTop = 1;
        var foot = new PanelContainer();
        foot.AddThemeStyleboxOverride("panel", footBox);
        _status.ClipText = true;
        _status.CustomMinimumSize = new Vector2(0, 20);
        foot.AddChild(_status);
        root.AddChild(foot);

        Slots.Changed += Repaint;
        tree.Root.AddChild(_layer);
    }

    public void Show()
    {
        Repaint();
        _layer.Visible = true;
    }

    public void Hide() => _layer.Visible = false;

    void Repaint()
    {
        _syncing = true;
        _enabled.ButtonPressed = Slots.Enabled;
        _enabled.Text = Slots.Enabled ? "ON" : "OFF";
        _syncing = false;

        int room = Slots.Capacity;
        int earned = Slots.Earned;
        _summary.Text = room == 1 && earned == 1
            ? "One action at a time."
            : room == earned
                ? $"Using all {earned} earned actions."
                : $"Using {room} of {earned} earned actions.";
        _summary.AddThemeColorOverride("font_color", room == 1 ? Style.Muted : Style.Good);
        _next.Text = Slots.NextStep;
        _fewer.Disabled = room <= 1;
        _more.Disabled = !Slots.Enabled || room >= earned;

        IReadOnlyList<Kept> recent = Slots.Recent;
        while (_running.GetChildCount() < room)
            _running.AddChild(Style.Label("", Style.Text));
        for (int i = 0; i < _running.GetChildCount(); i++)
        {
            var line = (Label)_running.GetChild(i);
            line.Visible = i < room;
            if (i >= room) continue;
            if (i >= recent.Count)
            {
                line.Text = i == 0
                    ? "1. (nothing yet: start an action in the game)"
                    : $"{i + 1}. (free: the action before your next one will go here)";
                line.AddThemeColorOverride("font_color", Style.Muted);
                continue;
            }
            Kept kept = recent[i];
            string state = kept.State == "" ? "starting..." : kept.State;
            line.Text = $"{i + 1}. {kept.Title}  -  {state}";
            line.AddThemeColorOverride("font_color", kept.State == "running" ? Style.Good : kept.State.StartsWith("waiting") ? Style.Accent : Style.Text);
        }

        string dungeonKey = string.Join("|", Slots.Dungeons.Where(r => r.Counts).Select(r => $"{r.Id}:{r.Clears}:{r.Difficulty}:{r.Name}"));
        if (dungeonKey != _dungeonKey)
        {
            _dungeonKey = dungeonKey;
            foreach (Node child in _dungeons.GetChildren())
            {
                _dungeons.RemoveChild(child);
                child.Free();
            }
            string group = "";
            foreach (DungeonRow row in Slots.Dungeons.Where(r => r.Counts))
            {
                if (row.Difficulty != group)
                {
                    group = row.Difficulty;
                    _dungeons.AddChild(Style.Label(group == "" ? "Dungeons" : group, Style.Accent, 13));
                }
                string mark = row.Clears <= 0
                    ? "not cleared"
                    : row.Clears >= ProgressMath.MasteryClears ? $"{row.Clears} clears, mastered" : $"{row.Clears} clear{(row.Clears == 1 ? "" : "s")}";
                _dungeons.AddChild(Style.Label($"{row.Name}   {mark}", row.Clears > 0 ? Style.Text : Style.Muted));
            }
            if (_dungeons.GetChildCount() == 0)
                _dungeons.AddChild(Style.Label("No standard dungeons were found.", Style.Muted));
        }

        int running = recent.Count(k => k.State == "running");
        _status.Text = Slots.Note != "" ? Slots.Note
            : !Slots.Enabled ? "Earned slots are off."
            : room == 1 ? "Clear a dungeon to earn a second action."
            : running <= 1 ? "Start another action in the game: the one you are doing now will keep running."
            : $"{running} actions are running together.";
    }
}
