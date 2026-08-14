// 原子文件写入工具（todolist #2）：
// 内容先写入随机命名的 .tmp 文件 → File.Replace 原子替换目标文件（替换前的版本保留为 .bak）
// → 断电/异常中断时目标文件保持旧内容，且 .bak 可供恢复。目标不存在时退化为 File.Move。

namespace DeciLauncher;

internal static class AtomicFile
{
    /// <summary>
    /// 原子写入文本文件：
    /// 1. 内容先写入随机命名的临时文件（.{name}.{guid}.tmp）；
    /// 2. 若目标存在，File.Replace(临时文件 → path, backup: .bak)——原子替换，原内容留存在 .bak；
    /// 3. 目标不存在时 File.Move(临时文件 → path)；失败时 finally 清理临时文件残留。
    /// 任一步失败均不影响现有文件内容。
    /// </summary>
    internal static void Write(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // 随机临时名：即使未来出现并发写入方也不会交叉写坏同一临时文件
        var tmp = Path.Combine(dir ?? ".", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var bak = path + ".bak";
        try
        {
            File.WriteAllText(tmp, content);
            if (File.Exists(path))
                File.Replace(tmp, path, bak);
            else
                File.Move(tmp, path);
        }
        finally
        {
            // 失败时清理残留临时文件（成功路径中 tmp 已被 Replace/Move 消耗）
            if (File.Exists(tmp))
            {
                try { File.Delete(tmp); } catch { /* 清理失败不致命 */ }
            }
        }
    }
}
