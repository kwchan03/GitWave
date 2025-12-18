using LibGit2Sharp;
using OpenTap;
using System.IO;

public static class TestPlanHelper
{
    public static TestPlan GetTestPlanFromHEAD(Repository repo, string repoRelativeFile)
    {
        var commit = repo.Head?.Tip ?? throw new InvalidOperationException("HEAD has no commits.");
        return LoadTestPlanFromCommit(repo, commit, repoRelativeFile);
    }

    public static TestPlan GetTestPlanFromIndex(Repository repo, string repoRelativeFile)
    {
        var ie = repo.Index[NormalizePath(repoRelativeFile)];
        if (ie == null) return null;

        var blob = repo.Lookup<Blob>(ie.Id);
        var text = blob.GetContentText();
        return DeserializeTestPlan(text);
    }

    public static TestPlan GetTestPlanFromWorkingDirectory(Repository repo, string repoRelativeFile)
    {
        var fullPath = Path.Combine(repo.Info.WorkingDirectory, repoRelativeFile);
        if (!File.Exists(fullPath)) return null;

        var text = File.ReadAllText(fullPath);
        return DeserializeTestPlan(text);
    }

    public static TestPlan GetTestPlanFromCommit(Repository repo, string repoRelativeFile, string commitShaOrPrefix)
    {
        var commit = repo.Commits.FirstOrDefault(c => c.Sha.StartsWith(commitShaOrPrefix, StringComparison.OrdinalIgnoreCase));
        if (commit == null) throw new ArgumentException($"Commit '{commitShaOrPrefix}' not found.");

        return LoadTestPlanFromCommit(repo, commit, repoRelativeFile);
    }

    // ─── Helpers ──────────────────────────────────────────────

    public static TestPlan LoadTestPlanFromCommit(Repository repo, Commit commit, string repoRelativeFile)
    {
        var norm = NormalizePath(repoRelativeFile);
        var entry = commit.Tree[norm];
        if (entry?.TargetType != TreeEntryTargetType.Blob) return null;

        var blob = (Blob)entry.Target;
        var text = blob.GetContentText();
        return DeserializeTestPlan(text);
    }

    public static TestPlan DeserializeTestPlan(string text)
    {
        var serializer = new TapSerializer();
        return serializer.DeserializeFromString(
            text,
            TypeData.FromType(typeof(TestPlan))) as TestPlan;
    }

    private static string NormalizePath(string repoRelative)
    {
        return repoRelative.Replace('\\', '/').TrimStart('/');
    }
}
