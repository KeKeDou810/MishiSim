using System;
using System.Linq;
using NUnit.Framework;

public sealed class RoomIdentityTests
{
    // Networking lives in Assembly-CSharp; the Unity test assembly accesses its public API by reflection.
    private static Type Identity => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType("Mishi.Networking.RoomIdentity")).First(t => t != null);
    private static object Call(string method, params object[] args) => Identity.GetMethod(method).Invoke(null, args);
    private static string Token() => (string)Call("RandomToken");
    private static string Proof(byte[] key, string room, string nonce, string version, string name, string resume) =>
        (string)Call("Proof", key, room, nonce, version, name, resume);

    [Test] public void DiscoveryRequiresProjectVersionAndProtocol()
    {
        string game = (string)Identity.GetField("Game").GetValue(null);
        string protocol = Identity.GetField("Protocol").GetValue(null).ToString();
        Assert.AreEqual(true, Call("Compatible", game, "1.2", protocol, "1.2"));
        Assert.AreEqual(false, Call("Compatible", "another-spacewar-game", "1.2", protocol, "1.2"));
        Assert.AreEqual(false, Call("Compatible", game, "1.1", protocol, "1.2"));
        Assert.AreEqual(false, Call("Compatible", game, "1.2", "old-protocol", "1.2"));
    }

    [Test] public void PasswordProofCannotBeReusedForAnotherChallengeRoomOrIdentity()
    {
        string salt = Token(), nonce = Token(), resume = Token();
        var key = (byte[])Call("Key", "测试密码", salt);
        var wrong = (byte[])Call("Key", "错误密码", salt);
        string proof = Proof(key, "room", nonce, "1", "玩家", resume);
        Assert.AreEqual(proof, Proof(key, "room", nonce, "1", "玩家", resume));
        Assert.AreNotEqual(proof, Proof(wrong, "room", nonce, "1", "玩家", resume));
        Assert.AreNotEqual(proof, Proof(key, "new-room", nonce, "1", "玩家", resume));
        Assert.AreNotEqual(proof, Proof(key, "room", Token(), "1", "玩家", resume));
        Assert.AreNotEqual(proof, Proof(key, "room", nonce, "2", "玩家", resume));
        Assert.AreNotEqual(proof, Proof(key, "room", nonce, "1", "其他人", resume));
        Assert.AreNotEqual(proof, Proof(key, "room", nonce, "1", "玩家", Token()));
        Assert.AreNotEqual(proof, Proof(key, "room", nonce, "1", "玩家", null));
    }

    [Test] public void CredentialsPreserveSpacesNormalizeUnicodeAndRejectControls()
    {
        Assert.AreEqual(" a ", Call("Password", " a "));
        Assert.AreEqual("é", Call("Password", "e\u0301"));
        Assert.AreEqual("", Call("Password", new object[] { null }));
        Assert.Throws<System.Reflection.TargetInvocationException>(() => Call("Password", new string('x', 65)));
        Assert.Throws<System.Reflection.TargetInvocationException>(() => Call("Password", "a\nb"));
        Assert.AreEqual(false, Call("Equal", null, null));
        Assert.AreEqual(false, Call("Equal", "abc", "abd"));
        Assert.AreEqual(false, Call("Equal", "abc", "ab"));
        Assert.AreEqual(true, Call("Equal", "abc", "abc"));
        Assert.AreEqual(32, Convert.FromBase64String(Token()).Length);
        Assert.AreNotEqual(Token(), Token());
    }
}
