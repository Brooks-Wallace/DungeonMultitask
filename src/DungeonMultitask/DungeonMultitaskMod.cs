// Copyright (c) 2026 Brooks-Wallace. MIT License.
// Window key and frame loop adapted from falorfrozen Multitask 0.2.2 (MIT).

using GamesByMalcs.Melvor2;
using Godot;
using MelvorModLoader;

namespace DungeonMultitask;

/// <summary>F5 opens the progress window. Earned slots keep earlier actions running beside the latest one.</summary>
public class DungeonMultitaskMod : IMod
{
    public static DungeonMultitaskMod? Instance { get; private set; }

    ModContext _ctx = null!;
    SlotWindow? _window;
    Game? _game;
    bool _wasDown;
    int _frame;
    ulong _lastTick;

    public SlotWindow? Window => _window;

    public void OnLoad(ModContext ctx)
    {
        Instance = this;
        _ctx = ctx;
        Slots.Dir = ctx.Directory;
        Slots.Log = ctx.Log;
        ctx.Frame += Tick;
        ctx.Log("Ready. Press F5 while a character is open.");
    }

    void Tick()
    {
        // The game is computing offline progress on its own thread: hands off.
        if (_ctx.Tree.Paused) return;

        // Notice a character being opened or closed a couple of times per second.
        if (++_frame % 30 == 0)
        {
            Game? game = GameAccess.CurrentGame;
            _game = game is { IsSaveLoaded: true } ? game : null;
            if (_game != null) Slots.Bind(_game);
            else
            {
                Slots.Unbind();
                if (_window is { Visible: true }) _window.Hide();
            }
        }

        ulong now = Time.GetTicksMsec();
        if (now - _lastTick >= 500)
        {
            _lastTick = now;
            if (_game != null) Slots.Tick(_game, now / 1000.0);
        }

        bool down = Input.IsKeyPressed(Key.F5);
        if (down && !_wasDown) ToggleWindow();
        _wasDown = down;
    }

    public void ToggleWindow()
    {
        if (_window is { Visible: true }) { _window.Hide(); return; }
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true })
        {
            _ctx.Log("F5: open a character first (the window does not work on the main menu).");
            return;
        }
        _game = game;
        Slots.Bind(game);
        _window ??= new SlotWindow(_ctx.Tree);
        _window.Show();
    }
}
