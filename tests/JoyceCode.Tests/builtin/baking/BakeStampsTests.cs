using System;
using System.IO;
using builtin.baking;
using Xunit;

namespace JoyceCode.Tests.builtin.baking;

/// <summary>
/// KAR-501: a change of the baker, not only of its inputs, re-bakes a file.
/// </summary>
public class BakeStampsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bakestamps-" + Guid.NewGuid().ToString("N"));

    public BakeStampsTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }


    [Fact]
    public void SameStamp_InputsOlder_Skips()
    {
        var d = BakeDecision.Of(null, "model-v1-abc", "model-v1-abc");
        Assert.False(d.Bake);
    }


    [Fact]
    public void DifferentBaker_Rebakes_AndSaysWhy()
    {
        var d = BakeDecision.Of(null, "model-v1-abc", "model-v1-def");
        Assert.True(d.Bake);
        Assert.Contains("model-v1-abc", d.Reason);
        Assert.Contains("model-v1-def", d.Reason);
    }


    [Fact]
    public void MissingStamp_Rebakes_UnlessAdopted()
    {
        Assert.True(BakeDecision.Of(null, null, "model-v1-abc").Bake);
        Assert.False(BakeDecision.Of(null, null, "scenario-v1", adoptUnstamped: true).Bake);
    }


    [Fact]
    public void StaleInputs_Rebake_WhateverTheStamp()
    {
        var d = BakeDecision.Of("Model is newer", "model-v1-abc", "model-v1-abc");
        Assert.True(d.Bake);
        Assert.Equal("Model is newer", d.Reason);
        Assert.True(BakeDecision.Of("Output missing", null, "scenario-v1", adoptUnstamped: true).Bake);
    }


    [Fact]
    public void Manifest_RoundTrips_AndForgets()
    {
        var m = BakeManifest.Load(_dir);
        Assert.Null(m.StampOf("mo-x"));
        m.Record("mo-x", "model-v1-abc");
        m.Record("ac-y", "animation-v1-abc");
        m.Forget("ac-y");
        m.Save(_dir);

        var again = BakeManifest.Load(_dir);
        Assert.Equal("model-v1-abc", again.StampOf("mo-x"));
        Assert.Null(again.StampOf("ac-y"));
        Assert.False(File.Exists(Path.Combine(_dir, BakeManifest.FileName + ".tmp")));
    }


    [Fact]
    public void CorruptManifest_IsEmpty_SoEverythingRebakes()
    {
        File.WriteAllText(Path.Combine(_dir, BakeManifest.FileName), "{ not json");
        Assert.Null(BakeManifest.Load(_dir).StampOf("mo-x"));
    }


    [Fact]
    public void BakerHash_ChangesWithCode_NotWithSymbolsOrOrder()
    {
        var baker = Path.Combine(_dir, "baker");
        Directory.CreateDirectory(Path.Combine(baker, "runtimes"));
        File.WriteAllText(Path.Combine(baker, "Chushi.dll"), "code 1");
        File.WriteAllText(Path.Combine(baker, "runtimes", "libassimp.so"), "native");
        File.WriteAllText(Path.Combine(baker, "Chushi.pdb"), "symbols 1");
        string h1 = BakerIdentity.HashDirectory(baker);

        File.WriteAllText(Path.Combine(baker, "Chushi.pdb"), "symbols 2");
        File.WriteAllText(Path.Combine(baker, "Chushi.xml"), "docs");
        Assert.Equal(h1, BakerIdentity.HashDirectory(baker));

        File.WriteAllText(Path.Combine(baker, "Chushi.dll"), "code 2");
        Assert.NotEqual(h1, BakerIdentity.HashDirectory(baker));

        File.WriteAllText(Path.Combine(baker, "Chushi.dll"), "code 1");
        File.WriteAllText(Path.Combine(baker, "runtimes", "libassimp.so"), "native 2");
        Assert.NotEqual(h1, BakerIdentity.HashDirectory(baker));
    }


    [Fact]
    public void Stamps_NameKindAndVersion()
    {
        Assert.Equal($"model-v{BakeVersions.Model}-abc", BakerIdentity.ModelStamp("abc"));
        Assert.Equal($"animation-v{BakeVersions.Animation}-abc", BakerIdentity.AnimationStamp("abc"));
        Assert.Equal($"scenario-v{BakeVersions.Scenario}", BakerIdentity.ScenarioStamp());
    }
}
