// Copyright (c) 2026 Brooks-Wallace. MIT License.

using System;
using System.Collections.Generic;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatAreas;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Combat;

namespace DungeonMultitask;

public sealed record DungeonRow(string Id, string Name, string Difficulty, int Clears, bool Counts);

/// <summary>Reads dungeon clear counts from the open character. Quest dungeons are listed as not counting.</summary>
public static class DungeonProgress
{
    public static List<DungeonRow> Read(Game game, Action<string> log)
    {
        try
        {
            var clears = new Dictionary<string, double>(StringComparer.Ordinal);
            var rows = new List<DungeonRow>();
            if (game.Entities.TryGetObjectById("MelvorBase:Combat", out GameEntity? entity) && entity is CombatEntity combat)
            {
                foreach (var pair in combat.Statistics.DungeonCompleted.Values)
                    clears[pair.Key.Id] = pair.Value;

                foreach (BaseCombatArea area in combat.Areas.Config.Areas)
                {
                    if (area is not Dungeon dungeon) continue;
                    string name = dungeon.DisplayName.Text;
                    bool counts = !IsQuest(name, dungeon.Id);
                    clears.TryGetValue(dungeon.Id, out double n);
                    string difficulty = dungeon.Difficulty?.DisplayName.Text ?? "";
                    rows.Add(new DungeonRow(dungeon.Id, name, difficulty, (int)Math.Floor(n), counts));
                }
            }
            return rows;
        }
        catch (Exception e)
        {
            log("Dungeon progress could not be read: " + e.Message);
            return new List<DungeonRow>();
        }
    }

    public static bool IsQuest(string name, string id) =>
        name.Equals("Stonehopper Cave", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Abyssal Vault", StringComparison.OrdinalIgnoreCase)
        || id.Contains("Stonehopper", StringComparison.OrdinalIgnoreCase)
        || id.Contains("AbyssalVault", StringComparison.OrdinalIgnoreCase)
        || id.Contains("Abyssal_Vault", StringComparison.OrdinalIgnoreCase);
}
