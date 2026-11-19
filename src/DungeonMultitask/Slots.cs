// Action-keeping adapted from falorfrozen Multitask 0.2.2 (MIT), commit c69867e0.
// Copyright (c) 2026 falorfrozen
// Dungeon-earned capacity: Copyright (c) 2026 Brooks-Wallace

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionQueue;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Actions;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Combat;
using GamesByMalcs.Melvor2.GameMenus.Footer;
using MelvorModLoader;

namespace DungeonMultitask;

/// <summary>One of the player's recent actions that is being kept running.</summary>
public sealed class Kept
{
    public string Entity { get; set; } = "";      // the skill, e.g. MelvorBase:Mining
    public string SkillName { get; set; } = "";
    public string TargetId { get; set; } = "";    // the rock, recipe, monster...
    public string Extra { get; set; } = "";       // combat only: the area
    public string Name { get; set; } = "";        // "Coal"

    /// <summary>What it is doing right now, in words (not saved).</summary>
    [JsonIgnore] public string State = "";
    /// <summary>True while it is stopped and waiting to be started again by this mod.</summary>
    [JsonIgnore] internal bool Pending;
    /// <summary>The Action Queue started it: the queue decides when it runs, this mod never starts it again.</summary>
    [JsonIgnore] internal bool ByQueue;
    [JsonIgnore] internal double RetryAt;
    [JsonIgnore] internal long Pile;              // a recipe: how many crafts the bank had materials for at the last look
    [JsonIgnore] internal double GrewAt;          // ...and when that number last went up

    [JsonIgnore] public string Title => $"{SkillName}: {Name}";
}

/// <summary>
/// The game stops every running action when a new one starts. This puts that list aside for a moment
/// and starts the previous action again, so several of the player's recent actions run together.
/// How many is earned from dungeon clears (see <see cref="ProgressMath"/>). The player can turn the
/// bonus off, or use fewer than they have earned.
///   - start something new            -> it joins, the oldest drops out when there is no room
///   - choose another rock/recipe     -> that skill simply does the new thing (a skill can only do one)
///   - press the game's own Stop      -> that action is dropped
///   - materials run out              -> it waits and goes on when there are some again
///   - the character dies             -> the fight is dropped, it never walks back in
/// </summary>
public static class Slots
{
    const double RetrySeconds = 2;
    /// <summary>A recipe that waits for materials starts again once there is enough for this many crafts...</summary>
    const int PileSize = 10;
    /// <summary>...or once the materials have not grown for this long.</summary>
    const double SettleSeconds = 30;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_activeActions")]
    static extern ref List<ActionComponent> Running(ActiveActionManager manager);

    sealed class Saved
    {
        public bool? Enabled { get; set; }
        public int? Limit { get; set; }
        public List<Kept> Recent { get; set; } = new();
    }

    public static string Dir = "";
    public static Action<string> Log { get; set; } = _ => { };
    static string FilePath => Path.Combine(Dir, "dungeonmultitask.json");

    static Game? s_game;
    static string s_character = "";
    static Dictionary<string, ActionComponent> s_actions = new();
    static GameFooter? s_footer;
    static volatile bool s_died;
    /// <summary>Set by the game's own "an action started" event: the game has just stopped everything else for it.</summary>
    static volatile bool s_gameStartedSomething;
    static volatile ActionComponent? s_lastStarted;
    static bool s_restoring, s_startedNow;
    static string s_published = "", s_savedAs = "";
    static bool s_enabled = true;
    static int s_limit; // 0 = use every action that has been earned
    static int s_earned = 1;
    static int s_skillCap;
    static string s_next = "";
    static string s_progress = "";
    static List<DungeonRow> s_rows = new();
    static bool s_announced;
    static List<Kept> s_recent = new();

    /// <summary>The actions being kept, the latest first.</summary>
    public static IReadOnlyList<Kept> Recent => s_recent;
    public static bool Enabled => s_enabled;
    /// <summary>Actions granted by dungeon clears, before the voluntary limit and the off switch.</summary>
    public static int Earned => s_earned;
    /// <summary>Voluntary ceiling. 0 means "use everything earned".</summary>
    public static int Limit => s_limit;
    public static int SkillCap => s_skillCap;
    public static string NextStep => s_next;
    public static IReadOnlyList<DungeonRow> Dungeons => s_rows;
    /// <summary>How many actions may run together right now.</summary>
    public static int Capacity => !s_enabled ? 1 : s_limit > 0 ? Math.Min(s_earned, s_limit) : s_earned;
    /// <summary>How many times this mod started an action again since the character was opened.</summary>
    public static int Starts { get; private set; }
    /// <summary>The last thing worth telling the player.</summary>
    public static string Note { get; private set; } = "";
    public static event Action? Changed;

    // ---------------------------------------------------------------- loading and saving

    static Dictionary<string, Saved> Read()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, Saved>>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new(); }   // also an older file with another layout: start clean
    }

    static string Signature() => $"{s_enabled},{s_limit}|{string.Join(";", s_recent.Select(k => $"{k.Entity},{k.TargetId},{k.Extra}"))}";

    static string Fingerprint() =>
        Signature() + "|" + s_progress + "|" + string.Join(";", s_recent.Select(k => k.State)) + "|" + Note;

    static void Save()
    {
        if (s_character == "") return;
        string now = Signature();
        if (now == s_savedAs) return;
        try
        {
            // Only this character's entry is replaced in what is on disk right now.
            Dictionary<string, Saved> all = Read();
            all[s_character] = new Saved { Enabled = s_enabled, Limit = s_limit, Recent = s_recent };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
            s_savedAs = now;
        }
        catch (Exception e) { Log("The slots could not be saved: " + e.Message); }
    }

    public static void Bind(Game game)
    {
        if (ReferenceEquals(s_game, game)) return;
        s_game = game;
        s_character = game.CharacterName;
        s_actions = Targets.Skills(game).ToDictionary(s => s.Entity.Id, s => s.Action);
        s_footer = null;
        s_died = false;
        Starts = 0;
        Note = "";
        Read().TryGetValue(s_character, out Saved? saved);
        s_enabled = saved?.Enabled ?? true;
        s_limit = Math.Max(0, saved?.Limit ?? 0);
        s_announced = false;
        RefreshProgress(game);
        s_announced = true;
        s_recent = (saved?.Recent ?? new List<Kept>()).Where(k => k.Entity != "" && k.TargetId != "" && s_actions.ContainsKey(k.Entity))
            .GroupBy(k => k.Entity).Select(g => g.First()).ToList();
        // What was running when the game was closed is brought back by the game itself. Anything on file that
        // is not running now is waiting to be started again (only looked at while a slot is on).
        foreach (Kept k in s_recent) k.Pending = true;
        s_savedAs = Signature();
        PublishCapacity(Capacity);
        s_gameStartedSomething = false;
        // The game raises this whenever any action starts - also when the same one is started again, which
        // looks like "nothing changed" afterwards although the game stopped everything else for it.
        game.ActionManager.ActionStarted += (_, e) =>
        {
            if (!ReferenceEquals(game, s_game) || s_restoring) return;
            s_gameStartedSomething = true;
            s_lastStarted = e.Action;
        };
        // A death must never turn into "walk straight back in". The event can arrive on the game's own
        // thread (offline progress), so it only raises a flag; the next tick acts on it.
        if (game.Entities.TryGetObjectById("MelvorBase:Combat", out GameEntity? combat) && combat is CombatEntity fighter)
            fighter.Action.PlayerDied += (_, _) => { if (ReferenceEquals(game, s_game)) s_died = true; };
        Changed?.Invoke();
    }

    /// <summary>No character is open any more.</summary>
    public static void Unbind()
    {
        if (s_game == null) return;
        s_game = null;
        s_character = "";
        s_announced = false;
        PublishCapacity(1);
        Publish(new List<string>());
        Targets.PublishPlayerKept(Array.Empty<string>());
    }

    // ---------------------------------------------------------------- the switches

    static void PublishCapacity(int capacity)
    {
        try { ((Godot.SceneTree)Godot.Engine.GetMainLoop()).Root.SetMeta(Targets.CapacityMeta, capacity); } catch { }
    }

    public static void SetEnabled(bool on)
    {
        if (s_enabled == on) return;
        s_enabled = on;
        PublishCapacity(Capacity);
        Note = on ? "" : "Earned slots are off. One action at a time.";
        if (s_game != null) Trim(s_game, stopExtras: true);
        Save();
        Changed?.Invoke();
    }

    /// <summary>0 uses every earned action. Any other value is a ceiling, clamped to what is earned.</summary>
    public static void SetLimit(int limit)
    {
        int next = limit <= 0 || limit >= s_earned ? 0 : limit;
        if (s_limit == next) return;
        s_limit = next;
        PublishCapacity(Capacity);
        Note = "";
        if (s_game != null) Trim(s_game, stopExtras: true);
        Save();
        Changed?.Invoke();
    }

    static void RefreshProgress(Game game)
    {
        int skills = s_actions.Count > 0 ? s_actions.Count : Targets.Skills(game).Count;
        s_skillCap = skills;
        s_rows = DungeonProgress.Read(game, Log);
        int standard = s_rows.Count(r => r.Counts);
        int unique = s_rows.Count(r => r.Counts && r.Clears >= 1);
        int mastered = s_rows.Count(r => r.Counts && r.Clears >= ProgressMath.MasteryClears);
        int earned = ProgressMath.Earned(unique, mastered, skills);
        s_next = ProgressMath.NextStep(unique, standard, mastered, skills);
        s_progress = $"{unique}/{standard}/{mastered}/{earned}";
        if (s_announced && earned > s_earned && s_character != "")
            Note = $"Dungeon progress raised the cap to {earned} actions.";
        s_earned = earned;
    }

    // ---------------------------------------------------------------- running

    static bool IsRunning(Kept k) =>
        s_actions.TryGetValue(k.Entity, out ActionComponent? action) && Targets.IsDoing(action, new Target(k.TargetId, k.Name, k.Extra));

    /// <summary>Call a couple of times per second while a character is open. <paramref name="now"/> is any steady clock in seconds.</summary>
    public static void Tick(Game game, double now)
    {
        if (!ReferenceEquals(game, s_game)) return;
        RefreshProgress(game);
        string before = Fingerprint();
        PublishCapacity(Capacity);
        try { Look(game, now); }
        catch (Exception e) { Log("Dungeon Multitask failed: " + e.Message); }
        Save();
        if (before != Fingerprint()) Changed?.Invoke();
    }

    static void Look(Game game, double now)
    {
        // 1. A death: the fight is dropped for good.
        if (s_died)
        {
            s_died = false;
            Kept? fight = s_recent.FirstOrDefault(k => k.Entity == "MelvorBase:Combat");
            if (fight != null)
            {
                s_recent.Remove(fight);
                Note = $"Your character died, so {fight.Title} was dropped.";
            }
        }

        // 2. What runs now. Something new, or a skill doing another thing than before, is the player's latest
        //    choice and goes to the front.
        var queueSkills = new HashSet<string>(Targets.QueueDriving().Split('|', StringSplitOptions.RemoveEmptyEntries));
        bool somethingNew = s_gameStartedSomething;
        s_gameStartedSomething = false;
        foreach (ActionComponent action in game.ActionManager.ActiveActions.ToList())
        {
            if (!s_actions.TryGetValue(action.Entity.Id, out ActionComponent? known) || !ReferenceEquals(known, action)) continue;
            Target? target = Targets.Current(action);
            if (target == null) continue;
            Kept? kept = s_recent.FirstOrDefault(k => k.Entity == action.Entity.Id);
            if (kept == null)
            {
                kept = new Kept { Entity = action.Entity.Id, SkillName = action.Entity.DisplayName.Text };
                s_recent.Insert(0, kept);
                somethingNew = true;
            }
            else if (kept.TargetId != target.Id || kept.Extra != target.Extra)
            {
                s_recent.Remove(kept);
                s_recent.Insert(0, kept);
                somethingNew = true;
            }
            kept.TargetId = target.Id;
            kept.Extra = target.Extra;
            kept.Name = target.Name;
            kept.Pending = false;
            kept.ByQueue = queueSkills.Contains(kept.Entity);
            kept.State = "running";
        }

        // What the game started last is the player's latest action, also when it is the same one as before.
        ActionComponent? last = s_lastStarted;
        s_lastStarted = null;
        if (last != null && s_recent.FindIndex(k => k.Entity == last.Entity.Id) is var at && at > 0 && last.IsStarted)
        {
            Kept latest = s_recent[at];
            s_recent.RemoveAt(at);
            s_recent.Insert(0, latest);
        }

        // One action at a time: remember what runs, start nothing.
        if (Capacity == 1)
        {
            s_recent.RemoveAll(k => !IsRunning(k));
            Trim(game, stopExtras: false);
            Publish(new List<string>());
            Targets.PublishPlayerKept(s_recent.Where(k => !k.ByQueue).Select(k => k.Entity));
            return;
        }

        // 3. What does not run.
        foreach (Kept kept in s_recent.ToList())
        {
            if (IsRunning(kept)) continue;
            if (kept.ByQueue) { s_recent.Remove(kept); continue; }   // the queue's own step: never ours to start again
            if (kept.Pending) continue;
            if (somethingNew) { kept.Pending = true; kept.RetryAt = 0; continue; }   // stopped by the game because of the new action
            // It stopped while nothing new was started: the player pressed Stop, or it ran out of something.
            ActionComponent action = s_actions[kept.Entity];
            if (Targets.Batches(game, action, new Target(kept.TargetId, kept.Name, kept.Extra)) == 0) { kept.Pending = true; continue; }   // out of materials: wait
            s_recent.Remove(kept);
        }

        // 4. No more than fits. A cap of one is the plain game.
        Trim(game, stopExtras: false);

        // 5. Start again what is waiting.
        foreach (Kept kept in s_recent.Where(k => k.Pending).Reverse().ToList()) Restore(game, kept, now);

        var held = s_recent.Skip(1).Where(IsRunning).Select(k => k.Entity).ToList();
        Publish(held);
        // The Action Queue only starts its "while idle" action in a slot nobody else holds, running or waiting.
        Targets.PublishPlayerKept(s_recent.Where(k => !k.ByQueue).Select(k => k.Entity));
        Order(game);
    }

    static void Trim(Game game, bool stopExtras)
    {
        while (s_recent.Count > Capacity)
        {
            // The Action Queue's own steps keep their place; the oldest of the player's own actions makes room.
            int at = s_recent.FindLastIndex(k => !k.ByQueue);
            if (at < 0) at = s_recent.Count - 1;
            Kept oldest = s_recent[at];
            s_recent.RemoveAt(at);
            // Normally it is not running any more (the game stopped it for the new action). It still is when
            // the queue started a step beside it, or when a slot was just switched off.
            if (IsRunning(oldest)) s_actions[oldest.Entity].TryStop(force: true);
        }
    }

    static void Restore(Game game, Kept kept, double now)
    {
        ActionComponent action = s_actions[kept.Entity];
        var target = new Target(kept.TargetId, kept.Name, kept.Extra);
        if (action.IsStarted)
        {
            // The skill went on to something else in the meantime; step 2 picks that up at the next look.
            kept.State = "waiting: this skill is doing something else";
            return;
        }
        if (now < kept.RetryAt) return;
        kept.RetryAt = now + RetrySeconds;

        if (!Targets.CanStart(action, target, out string problem))
        {
            s_recent.Remove(kept);
            Note = $"{kept.Title} was dropped: {problem}.";
            return;
        }
        // A recipe that eats what another action makes would start and stop for every single fish, and the
        // game reports each stop. So it waits for a small pile - or, when the pile has stopped growing
        // (nothing is making more), works through whatever there is.
        long batches = Targets.Batches(game, action, target);
        if (batches > kept.Pile) kept.GrewAt = now;
        kept.Pile = batches;
        if (batches == 0) { kept.State = "waiting: the materials have run out"; return; }
        if (batches < PileSize && now - kept.GrewAt < SettleSeconds) { kept.State = $"waiting: collecting materials ({batches}/{PileSize})"; return; }

        if (!Beside(game, () => Targets.Start(game, action, target, out problem))) { kept.State = "waiting: " + problem; return; }
        Starts++;
        s_startedNow = true;
        kept.Pending = false;
        kept.RetryAt = 0;
        kept.State = "running";
    }

    /// <summary>Runs the game's own start code while the game sees nothing running, so it stops nothing.</summary>
    static bool Beside(Game game, Func<bool> start)
    {
        List<ActionComponent> running = Running(game.ActionManager);
        List<ActionComponent> kept = running.ToList();
        running.Clear();
        s_restoring = true;
        try { return start(); }
        finally
        {
            s_restoring = false;
            foreach (ActionComponent action in kept)
                if (action.IsStarted && !running.Contains(action)) running.Add(action);
        }
    }

    /// <summary>
    /// The game shows the last action of its list in the bottom bar. The list is kept in the order "oldest
    /// first, latest last", so the bar shows what the player did last.
    /// </summary>
    static void Order(Game game)
    {
        List<ActionComponent> running = Running(game.ActionManager);
        if (running.Count < 2) { s_startedNow = false; return; }
        int Rank(ActionComponent a)
        {
            int at = s_recent.FindIndex(k => k.Entity == a.Entity.Id);
            return at < 0 ? -1 : at;   // something this mod does not know: treat it as the very latest
        }
        List<ActionComponent> wanted = running.OrderByDescending(Rank).ToList();
        bool reordered = !wanted.SequenceEqual(running);
        if (reordered)
        {
            running.Clear();
            running.AddRange(wanted);
        }
        // The bar bound itself to whatever started last (also to what this mod just started again).
        if (!reordered && !s_startedNow) return;
        s_startedNow = false;
        try
        {
            if (s_footer == null || !Godot.GodotObject.IsInstanceValid(s_footer)) s_footer = GameAccess.FindNode<GameFooter>();
            s_footer?.Update();
        }
        catch (Exception e) { Log("The bottom bar could not be refreshed: " + e.Message); }
    }

    /// <summary>Tells other mods (the Action Queue) which skills are being kept running beside the latest action.</summary>
    static void Publish(List<string> held)
    {
        string text = string.Join("|", held);
        if (text == s_published) return;
        s_published = text;
        try { ((Godot.SceneTree)Godot.Engine.GetMainLoop()).Root.SetMeta(Targets.HeldMeta, text); } catch { }
    }
}
