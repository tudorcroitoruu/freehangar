using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

public class SteamBuildValidationTests
{
    [Test]
    public void MissingSettingsRejectBuild()
    {
        Assert.Throws<BuildFailedException>(() => SteamBuildValidation.ValidateSettings(null));
    }

    [TestCase(0u)]
    [TestCase(480u)]
    public void UnsetOrTestAppIdRejectsBuild(uint appId)
    {
        var settings = ScriptableObject.CreateInstance<SteamAchievementSettings>();
        try
        {
            settings.appId = appId;
            Assert.Throws<BuildFailedException>(() => SteamBuildValidation.ValidateSettings(settings));
        }
        finally { Object.DestroyImmediate(settings); }
    }

    [Test]
    public void ConfiguredAppIdPassesConfigurationCheck()
    {
        var settings = ScriptableObject.CreateInstance<SteamAchievementSettings>();
        try
        {
            settings.appId = 123456;
            Assert.DoesNotThrow(() => SteamBuildValidation.ValidateSettings(settings));
        }
        finally { Object.DestroyImmediate(settings); }
    }
}
