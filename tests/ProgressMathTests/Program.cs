using DungeonMultitask;

int failed = 0;

void Check(string name, bool ok)
{
    Console.WriteLine(ok ? "ok   " + name : "FAIL " + name);
    if (!ok) failed++;
}

Check("no clears is one action", ProgressMath.Earned(0, 0, 20) == 1);
Check("each new dungeon adds one", ProgressMath.Earned(1, 0, 20) == 2);
Check("three dungeons", ProgressMath.Earned(3, 0, 20) == 4);
Check("twelve dungeons", ProgressMath.Earned(12, 0, 20) == 13);
Check("six mastered dungeons add one", ProgressMath.Earned(12, 6, 20) == 14);
Check("twelve mastered dungeons add another", ProgressMath.Earned(12, 12, 20) == 15);
Check("mastery counts before every dungeon is cleared", ProgressMath.Earned(3, 6, 20) == 5);
Check("skill cap", ProgressMath.Earned(12, 12, 8) == 8);
Check("unknown skill cap does not shrink the ladder", ProgressMath.Earned(4, 0, 0) == 5);
Check("negative clears stay at one", ProgressMath.Earned(-2, -1, 20) == 1);

Check("next step is one more dungeon",
    ProgressMath.NextStep(2, 12, 0, 20) == "Clear 1 more dungeon for another action.");
Check("mastery step names how many 10-clear dungeons are left",
    ProgressMath.NextStep(12, 12, 2, 20) == "Take 4 more dungeons to 10 clears for another action.");
Check("one mastery dungeon left is singular",
    ProgressMath.NextStep(12, 12, 5, 20) == "Take 1 more dungeon to 10 clears for another action.");
Check("second mastery band",
    ProgressMath.NextStep(12, 12, 8, 20) == "Take 4 more dungeons to 10 clears for another action.");
Check("skill cap reached",
    ProgressMath.NextStep(12, 12, 12, 8) == "Every skill can already run at once.");
Check("ladder finished",
    ProgressMath.NextStep(12, 12, 12, 20) == "Every dungeon step is earned.");

return failed;
