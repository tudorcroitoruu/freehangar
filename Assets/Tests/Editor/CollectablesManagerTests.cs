using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class CollectablesManagerTests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    private GameObject Make(string name, params System.Type[] components)
    {
        var obj = new GameObject(name, components);
        objects.Add(obj);
        return obj;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var obj in objects) if (obj != null) Object.DestroyImmediate(obj);
        objects.Clear();
    }

    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private BuriedFind Find(string id)
    {
        var find = Make(id, typeof(BuriedFind)).GetComponent<BuriedFind>();
        find.ConfigureChestKey(id, id);
        return find;
    }

    [Test]
    public void ProgressCountsUniqueListIdsAndExcludesOtherSavedEvents()
    {
        var a = Find("test.key.a");
        var b = Find("test.key.b");
        b.gameObject.SetActive(false);
        var list = new CollectablesManager.Collection(BuriedFind.Kind.Key, "Keys");
        list.collectables.AddRange(new[] { a, a, Find("test.key.a"), b, null, Find("") });
        var data = new PlayerData { foundSecrets = new[] { "test.key.a", "test.key.a", "barrier", "pest.intro" } };
        list.GetProgress(data, out int found, out int total);
        Assert.AreEqual(1, found);
        Assert.AreEqual(2, total);
        list.GetProgress(null, out found, out total);
        Assert.AreEqual(0, found);
        Assert.AreEqual(2, total);
    }

    [Test]
    public void ScenePopulationIncludesInactiveAndLaterSpawnedFindsWithoutDuplicates()
    {
        var manager = Make("Manager", typeof(CollectablesManager)).GetComponent<CollectablesManager>();
        var first = Find("test.key.first");
        first.gameObject.SetActive(false);
        manager.PopulateFromScene();
        var later = Find("test.key.later");
        manager.PopulateFromScene();
        manager.PopulateFromScene();
        var collection = manager.Collections[0];
        Assert.AreEqual(1, collection.collectables.FindAll(item => item == first).Count);
        Assert.AreEqual(1, collection.collectables.FindAll(item => item == later).Count);
    }

    [Test]
    public void PopupQueuesImagesAndTitlesThenRestoresExistingWarning()
    {
        var root = Make("Popup", typeof(RectTransform), typeof(CanvasGroup), typeof(AchievementsPopup));
        var popup = root.GetComponent<AchievementsPopup>();
        var title = Make("Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        var image = Make("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Set(popup, "achievementsCanvasGroup", root.GetComponent<CanvasGroup>());
        Set(popup, "achievementsTitle", title);
        Set(popup, "achievementsImage", image);
        popup.Initialize();
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        try
        {
            root.SetActive(false);
            popup.Show(sprite, "Keys 1/5", 1f);
            popup.Show(null, "Chests 1/3", 1f);
            popup.ShowWarning(WarningType.VehicleFull, 10f);
            popup.Tick(.2f);
            Assert.IsTrue(root.activeSelf);
            Assert.AreEqual("Keys 1/5", title.text);
            Assert.AreSame(sprite, image.sprite);
            Assert.AreEqual(1f, root.GetComponent<CanvasGroup>().alpha);
            popup.Tick(.81f);
            Assert.AreEqual("Chests 1/3", title.text);
            Assert.IsFalse(image.enabled);
            popup.Tick(.5f);
            Assert.AreEqual("Chests 1/3", title.text);
            popup.Tick(.51f);
            Assert.AreEqual(WarningType.VehicleFull, popup.CurrentType);
            popup.Tick(10f);
            Assert.IsNull(popup.CurrentType);
            Assert.AreEqual(0f, root.GetComponent<CanvasGroup>().alpha);
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }
}
