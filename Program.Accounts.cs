// 离线账户认证
using MinecraftLaunch.Components.Authenticator;
// 文件操作（配置持久化）
using System.IO;
// JSON 序列化（替换手拼 JSON）
using System.Text.Json;
using System.Text.Json.Serialization;
// Photino 窗口（前端消息回传）
using Photino.NET;
// Game API（Account 类型缓存）
using MinecraftLaunch.Base.Models.Authentication;

namespace DeciLauncher;

partial class Program
{
    // 账户配置文件路径（%AppData%\.decilc\accounts.json）
    private static readonly string AccountsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".decilc");
    private static readonly string AccountsFilePath = Path.Combine(AccountsDir, "accounts.json");
    // 已创建的账户列表
    private static readonly List<AccountEntry> Accounts = [];
    // 已认证账户缓存（避免每次启动重新 Authenticate 导致 UUID 不一致）
    private static readonly Dictionary<string, Account> AuthenticatedAccounts = [];
    // 账户列表与认证缓存的访问锁：
    // 创建/删除在 Photino 消息线程执行，启动流程在后台线程读取（LaunchGame 的 await 之后），
    // 无锁并发读写 List/Dictionary 可能抛异常或破坏内部状态
    internal static readonly object AccountsLock = new();

    /// <summary>
    /// 账户数据结构（序列化为 JSON 数组格式存储至 accounts.json）
    /// </summary>
    private record AccountEntry(
        [property: JsonPropertyName("username")] string Username,
        [property: JsonPropertyName("uuid")] string Uuid,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("skinModel")] string SkinModel
    );

    /// <summary>
    /// 从 accounts.json 加载已保存的账户，并迁移旧格式
    /// </summary>
    private static void LoadAccounts()
    {
        // 迁移旧 Config/accounts.json → 新位置
        MigrateOldAccounts();

        if (!File.Exists(AccountsFilePath)) return;
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(AccountsFilePath));
            foreach (var elem in json.RootElement.EnumerateArray())
            {
                var username = elem.TryGetProperty("username", out var n) ? n.GetString() : null;
                var uuid = elem.TryGetProperty("uuid", out var u) ? u.GetString() : null;
                var type = elem.TryGetProperty("type", out var t) ? t.GetString() ?? "offline" : "offline";
                var skin = elem.TryGetProperty("skinModel", out var s) ? s.GetString() ?? "steve" : "steve";
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(uuid))
                    Accounts.Add(new AccountEntry(username, uuid, type, skin));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] 账户文件损坏: {ex.Message}");
        }
    }

    /// <summary>
    /// 迁移旧 Config/accounts.json 到新位置。
    /// 迁移前按 UUID 去重：即使 File.Delete 失败导致下次启动重复迁移，也不会产生重复条目
    /// </summary>
    private static void MigrateOldAccounts()
    {
        var oldPath = Path.Combine(AppContext.BaseDirectory, "Config", "accounts.json");
        if (!File.Exists(oldPath)) return;
        try
        {
            using var oldJson = JsonDocument.Parse(File.ReadAllText(oldPath));
            if (!oldJson.RootElement.TryGetProperty("accounts", out var accountsObj)) return;

            foreach (var prop in accountsObj.EnumerateObject())
            {
                var obj = prop.Value;
                var name = obj.TryGetProperty("displayName", out var n) ? n.GetString() : null;
                var uuid = obj.TryGetProperty("uuid", out var u) ? u.GetString() : null;
                var type = obj.TryGetProperty("type", out var t) ? t.GetString() ?? "offline" : "offline";
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(uuid) &&
                    !Accounts.Any(a => a.Uuid == uuid))
                    Accounts.Add(new AccountEntry(name, uuid, type, "steve"));
            }
            SaveAccounts();
            try
            {
                File.Delete(oldPath);
            }
            catch (Exception delEx)
            {
                // 删除失败不致命：下次启动的重复迁移会被上面的 UUID 去重挡住
                System.Diagnostics.Debug.WriteLine($"[WARN] 旧账户文件删除失败: {delEx.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] 旧账户迁移失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 保存账户列表到 accounts.json（锁内快照后序列化，避免与并发读交叉）
    /// </summary>
    private static void SaveAccounts()
    {
        try
        {
            AccountEntry[] snapshot;
            lock (AccountsLock)
            {
                snapshot = Accounts.ToArray();
            }
            Directory.CreateDirectory(AccountsDir);
            File.WriteAllText(AccountsFilePath, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] 保存账户失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建离线账户并持久化到文件
    /// </summary>
    private static void CreateOfflineAccount(PhotinoWindow window, string name)
    {
        try
        {
            var account = new OfflineAuthenticator().Authenticate(name);
            var uuid = account.Uuid.ToString();

            // 锁内只做列表/字典操作，消息发送移到锁外（Invoke 投递与 UI 线程交互，避免持锁等待）
            var exists = false;
            lock (AccountsLock)
            {
                exists = Accounts.Any(a => a.Uuid == uuid);
                if (!exists)
                {
                    var entry = new AccountEntry(account.Name, uuid, "offline", "steve");
                    Accounts.Add(entry);
                    AuthenticatedAccounts[uuid] = account;
                }
            }
            if (exists)
            {
                TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "account-error", message = L($"账户 {account.Name} 已存在", $"Account {account.Name} already exists") }));
                return;
            }
            SaveAccounts();
            SendAccountList(window);
        }
        catch (Exception ex)
        {
            TryNotifyWindow(window, JsonSerializer.Serialize(new { type = "account-error", message = ex.Message }));
        }
    }

    /// <summary>
    /// 删除指定 UUID 的账户并通知前端
    /// </summary>
    private static void DeleteAccount(PhotinoWindow window, string uuid)
    {
        lock (AccountsLock)
        {
            Accounts.RemoveAll(a => a.Uuid == uuid);
            AuthenticatedAccounts.Remove(uuid);
        }
        SaveAccounts();
        SendAccountList(window);
    }

    private static void SendAccountList(PhotinoWindow window)
    {
        // 锁内快照后释放，序列化在锁外执行，避免长时间持锁
        AccountEntry[] snapshot;
        lock (AccountsLock)
        {
            snapshot = Accounts.ToArray();
        }
        TryNotifyWindow(window, JsonSerializer.Serialize(new
        {
            type = "account-list",
            accounts = snapshot
        }));
    }

    /// <summary>
    /// 初始化账户模块（在应用启动时调用一次）：
    /// 加载账户列表，并预热离线账户的认证缓存，
    /// 避免每次启动游戏时重新 Authenticate（保证与注释承诺的行为一致）
    /// </summary>
    private static void InitializeAccounts()
    {
        LoadAccounts();

        // 预热缓存：仅 offline 类型可离线认证；其他类型（Microsoft/Yggdrasil）留待后续支持时处理
        foreach (var entry in Accounts)
        {
            if (entry.Type != "offline" || AuthenticatedAccounts.ContainsKey(entry.Uuid))
                continue;
            try
            {
                var account = new OfflineAuthenticator().Authenticate(entry.Username);
                // 防御：仅当确定性算法产出的 UUID 与存储一致时才缓存，避免启动时用到不一致的 UUID
                if (string.Equals(account.Uuid.ToString(), entry.Uuid, StringComparison.OrdinalIgnoreCase))
                    AuthenticatedAccounts[entry.Uuid] = account;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] 账户 {entry.Username} 认证预热失败: {ex.Message}");
            }
        }
    }
}
