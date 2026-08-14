// 离线账户 UUID 确定性验证:
// 原版 Minecraft 的离线 UUID = Java UUID.nameUUIDFromBytes("OfflinePlayer:" + name),
// 即 MD5 → 设置 version(字节 6)与 variant(字节 8)位 → 大端字符串表示。
// 此测试锁定 MinecraftLaunch OfflineAuthenticator 与上述算法一致(即 todolist
// 「离线 UUID 确定性」由库本身满足)。

using System.Security.Cryptography;
using System.Text;
using MinecraftLaunch.Components.Authenticator;

namespace DeciLauncher.Tests;

public class OfflineAuthenticatorTests
{
    [Fact]
    public void OfflineAuthenticator_SameName_ProducesSameUuid()
    {
        var a1 = new OfflineAuthenticator().Authenticate("Tristan");
        var a2 = new OfflineAuthenticator().Authenticate("Tristan");
        Assert.Equal(a1.Uuid, a2.Uuid);
    }

    [Fact]
    public void OfflineAuthenticator_UuidMatchesMojangOfflineAlgorithm()
    {
        var account = new OfflineAuthenticator().Authenticate("Tristan");

        var md5 = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:Tristan"));
        // Java UUID.nameUUIDFromBytes: 版本位 3 + RFC 4122 变体位
        md5[6] = (byte)((md5[6] & 0x0F) | 0x30);
        md5[8] = (byte)((md5[8] & 0x3F) | 0x80);
        // 大端解释与 Java UUID.toString() 一致
        var expected = new Guid(md5, bigEndian: true);
        Assert.Equal(expected, account.Uuid);
    }

    [Fact]
    public void OfflineAuthenticator_CanonicalWellKnownValue()
    {
        // 独立计算的原版离线 UUID(Java 算法),作为金丝雀值防止库升级改变算法
        var account = new OfflineAuthenticator().Authenticate("Tristan");
        Assert.Equal(Guid.Parse("b953cca5-dd51-3fd6-9460-dbd9f0e5a603"), account.Uuid);
    }
}
