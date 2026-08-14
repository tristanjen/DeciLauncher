// 原子文件写入工具（todolist #2）：
// 写 .tmp → File.Replace 原子替换目标文件（替换前的版本保留为 .bak）→ 断电/异常中断时
// 目标文件保持旧内容，且 .bak 可供恢复。目标不存在时退化为 File.Move。

namespace DeciLauncher;

internal static class AtomicFile
{
    /// <summary>
    /// 原子写入文本文件：
    /// 1. 内容先写入 &lt;path&gt;.tmp；
    /// 2. 若目标存在，File.Replace(.tmp → path, backup: .bak)——原子替换，原内容留存在 .bak；
    /// 3. 目标不存在时 File.Move(.tmp → path)。
    /// 任一步失败均不影响现有文件内容。
    /// </summary>
    internal static void Write(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        var bak = path + ".bak";
        File.WriteAllText(tmp, content);
        if (File.Exists(path))
            File.Replace(tmp, path, bak);
        else
            File.Move(tmp, path);
    }
}
