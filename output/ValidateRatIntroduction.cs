using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class ValidateRatIntroduction
{
    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        var source = UnityEngine.Object.FindFirstObjectByType<GrainPestDirector>();
        var oldData = SaveSystem.Data;
        var root = new GameObject("Rat introduction validation");
        GrainContainer[] bags = null;
        var originalPests = UnityEngine.Object.FindObjectsByType<GrainPest>(FindObjectsSortMode.None);
        try
        {
            SaveSystem.Data = new PlayerData();
            SaveSystem.Data.openedHangars.Add(3);
            var director = root.AddComponent<GrainPestDirector>();
            director.hangar3 = source.hangar3;
            director.tutorialBagTemplate = source.tutorialBagTemplate;
            director.ratPrefab = source.ratPrefab;
            director.ratAnimation = source.ratAnimation;
            director.PrepareRatIntroduction();
            bags = (GrainContainer[])typeof(GrainPestDirector).GetField("preparedRatBags", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(director);
            return new { bags = bags?.Length ?? 0, ratsPerBag = bags?.Select(b => UnityEngine.Object.FindObjectsByType<GrainPest>(FindObjectsSortMode.None).Count(p => p.Target == b)).ToArray(), eligible = bags?.Select(b => GrainPest.IsNearHangar(b.transform.position)).ToArray() };
        }
        finally
        {
            foreach (var pest in UnityEngine.Object.FindObjectsByType<GrainPest>(FindObjectsSortMode.None))
                if (!originalPests.Contains(pest)) UnityEngine.Object.DestroyImmediate(pest.gameObject);
            if (bags != null) foreach (var bag in bags) if (bag != null) UnityEngine.Object.DestroyImmediate(bag.gameObject);
            UnityEngine.Object.DestroyImmediate(root);
            SaveSystem.Data = oldData;
        }
    }
}
