// AtomicFile 原子写入测试(todolist #2):锁定 .tmp → File.Replace → .bak 行为

using DeciLauncher;

namespace DeciLauncher.Tests;

public class AtomicFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "decilauncher-tests", Guid.NewGuid().ToString("N"));
    private string PathOf(string name) => Path.Combine(_dir, name);

    // 目录内是否有任何残留临时文件（随机命名 .*.tmp）
    private bool HasTmpResidue(string subDir = "")
    {
        var dir = Path.Combine(_dir, subDir);
        if (!Directory.Exists(dir)) return false;
        return Directory.GetFiles(dir, ".*.tmp").Length > 0;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void Write_NewFile_CreatesContentAndNoTempResidue()
    {
        var target = PathOf("new.json");
        AtomicFile.Write(target, "v1");

        Assert.Equal("v1", File.ReadAllText(target));
        Assert.False(HasTmpResidue());
        Assert.False(File.Exists(target + ".bak"));
    }

    [Fact]
    public void Write_ExistingFile_ReplacesAtomicallyAndKeepsBackup()
    {
        var target = PathOf("existing.json");
        AtomicFile.Write(target, "v1");
        AtomicFile.Write(target, "v2");

        Assert.Equal("v2", File.ReadAllText(target));
        // 替换前的版本保留在 .bak
        Assert.Equal("v1", File.ReadAllText(target + ".bak"));
        Assert.False(HasTmpResidue());
    }

    [Fact]
    public void Write_RepeatedWrites_BackupTracksPreviousVersion()
    {
        var target = PathOf("series.json");
        AtomicFile.Write(target, "a");
        AtomicFile.Write(target, "b");
        AtomicFile.Write(target, "c");

        Assert.Equal("c", File.ReadAllText(target));
        Assert.Equal("b", File.ReadAllText(target + ".bak"));
        Assert.False(HasTmpResidue());
    }

    [Fact]
    public void Write_MissingDirectory_CreatesIt()
    {
        var target = PathOf("deep/nested/accounts.json");
        AtomicFile.Write(target, "data");

        Assert.Equal("data", File.ReadAllText(target));
        Assert.False(HasTmpResidue("deep/nested"));
    }

    [Fact]
    public void Write_InterruptedState_ExistingFileUnchanged()
    {
        // 模拟写入中断：仅存在残留临时文件时（断电/异常场景），目标文件保持旧内容。
        // 新实现使用随机临时名，残留文件不会与后续写入冲突
        var target = PathOf("interrupted.json");
        AtomicFile.Write(target, "good");
        File.WriteAllText(target + ".residue.tmp", "half-written");

        Assert.Equal("good", File.ReadAllText(target));
        // 下一次正常写入不受残留影响并完成替换
        AtomicFile.Write(target, "recovered");
        Assert.Equal("recovered", File.ReadAllText(target));
    }
}
