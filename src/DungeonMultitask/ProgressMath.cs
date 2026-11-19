// Copyright (c) 2026 Brooks-Wallace. MIT License.

using System;

namespace DungeonMultitask;

/// <summary>
/// How many actions a character has earned. Pure: no game types, so the ladder can be checked on its own.
/// One action until the first standard dungeon is cleared, then one more per newly cleared dungeon.
/// Six dungeons at <see cref="MasteryClears"/> or more add one action, and twelve add one more.
/// The result never exceeds <paramref name="skillCap"/> when that cap is known.
/// </summary>
public static class ProgressMath
{
    public const int MasteryClears = 10;
    public const int FirstMasteryAt = 6;
    public const int SecondMasteryAt = 12;

    public static int Earned(int uniqueCleared, int mastered, int skillCap)
    {
        int earned = 1 + Math.Max(0, uniqueCleared);
        if (mastered >= FirstMasteryAt) earned++;
        if (mastered >= SecondMasteryAt) earned++;
        if (skillCap > 0) earned = Math.Min(earned, skillCap);
        return Math.Max(1, earned);
    }

    public static string NextStep(int uniqueCleared, int standardCount, int mastered, int skillCap)
    {
        if (standardCount <= 0)
            return "No standard dungeons were found on this character yet.";
        int earned = Earned(uniqueCleared, mastered, skillCap);
        if (skillCap > 0 && earned >= skillCap)
            return "Every skill can already run at once.";
        if (uniqueCleared < standardCount)
            return "Clear 1 more dungeon for another action.";
        if (mastered < FirstMasteryAt)
            return MasteryLeft(FirstMasteryAt - mastered);
        if (mastered < SecondMasteryAt)
            return MasteryLeft(SecondMasteryAt - mastered);
        return "Every dungeon step is earned.";
    }

    static string MasteryLeft(int left) =>
        left == 1
            ? "Take 1 more dungeon to 10 clears for another action."
            : $"Take {left} more dungeons to 10 clears for another action.";
}
