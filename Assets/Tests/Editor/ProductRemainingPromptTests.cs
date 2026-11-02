using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ProductRemainingPromptTests
{
    private GameObject root;
    private InventoryManager manager;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Product capacity prompts");
        manager = root.AddComponent<InventoryManager>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    [TestCase(true)]
    [TestCase(false)]
    public void RemainingCapacityCountsOnlyActiveInstancesOfTheSameProduct(bool shop)
    {
        var product = CreateProduct(shop, 16);
        var first = Register("FreeBag");
        Register("FreeBag");
        Register("FreeBag");
        Register("Other product");
        Assert.IsTrue(product.TryGetPrompt(null, out var prompt));
        Assert.AreEqual("Left: 13/16", prompt.SecondaryDescription);
        first.SetActive(false);
        product.TryGetPrompt(null, out prompt);
        Assert.AreEqual("Left: 14/16", prompt.SecondaryDescription);
        Object.DestroyImmediate(first);
        product.TryGetPrompt(null, out prompt);
        Assert.AreEqual("Left: 14/16", prompt.SecondaryDescription);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void FullCapacityShowsZeroAndUnlimitedProductsHaveNoSecondaryDescription(bool shop)
    {
        var product = CreateProduct(shop, 1);
        Register("FreeBag");
        product.TryGetPrompt(null, out var prompt);
        Assert.AreEqual("Left: 0/1", prompt.SecondaryDescription);
        Set(product, "maxActiveInScene", 0);
        product.TryGetPrompt(null, out prompt);
        Assert.IsTrue(string.IsNullOrEmpty(prompt.SecondaryDescription));
    }

    [Test]
    public void SecondaryDescriptionClearsWhenTargetChangesOrPromptHides()
    {
        var panel = Child("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(300, 100);
        var prompt = panel.gameObject.AddComponent<InteractionPrompt>();
        var secondary = Child("Secondary", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI))
            .GetComponent<TMPro.TMP_Text>();
        secondary.transform.SetParent(panel);
        var camera = Child("Camera", typeof(Camera)).GetComponent<Camera>();
        Set(prompt, "panel", panel);
        Set(prompt, "canvasRect", panel);
        Set(prompt, "descriptionTwo", secondary);
        var data = new InteractionPromptData(Vector3.forward * 2, "Take bag") { SecondaryDescription = "Left: 13/16" };
        prompt.Show(camera, data);
        Assert.IsTrue(secondary.gameObject.activeSelf);
        Assert.AreEqual("Left: 13/16", secondary.text);
        prompt.Show(camera, new InteractionPromptData(Vector3.forward * 2, "Ride bicycle"));
        Assert.IsFalse(secondary.gameObject.activeSelf);
        Assert.AreEqual("", secondary.text);
        prompt.Show(camera, data);
        prompt.Hide();
        Assert.IsFalse(secondary.gameObject.activeSelf);
        Assert.AreEqual("", secondary.text);
    }

    private IInteractable CreateProduct(bool shop, int limit)
    {
        var host = Child("Product");
        Component product = shop ? (Component)host.AddComponent<ShopProduct>() : host.AddComponent<InventoryProduct>();
        Set(product, "productID", "FreeBag");
        Set(product, "maxActiveInScene", limit);
        return (IInteractable)product;
    }

    private GameObject Register(string id)
    {
        var instance = Child("Active " + id);
        Assert.IsNotNull(manager.RegisterInstance(instance, id, 16));
        return instance;
    }

    private GameObject Child(string name, params System.Type[] components)
    {
        var child = new GameObject(name, components);
        child.transform.SetParent(root.transform);
        return child;
    }

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
