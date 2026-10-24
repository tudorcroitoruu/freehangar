using System;
using System.Threading.Tasks;
using UnityEngine;

// Run after PreviewCornCarry in a fresh Play Mode session.
public static class ValidatePourRange
{
    private static PlayerController player;
    private static ShovelTool tool;
    public static async Task<string> Main()
    {
        Time.timeScale = 1;
        Application.runInBackground = true;
        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        tool = player.GetComponentInChildren<ShovelTool>();
        var bag = UnityEngine.Object.FindFirstObjectByType<GrainContainer>();
        float initialLoad = tool.Kilograms, initialBag = bag.Kilograms;
        if (initialLoad < 4.9f) throw new Exception("Expected a loaded shovel");
        // The scene's current standing position and moved sack reproduced the reported failure.
        player.transform.position = new Vector3(-.8f, .04f, -8.35f);
        player.characterCamera.transform.LookAt(bag.PourPoint);
        Physics.SyncTransforms();
        Ray ray = player.characterCamera.ViewportPointToRay(new Vector3(.5f, .5f));
        if (!bag.GetComponent<Collider>().Raycast(ray, out RaycastHit hit, 4f) || hit.distance <= 2.6f)
            throw new Exception("Reproduction must hit the bag between 2.6 and 4 metres");
        float distance = hit.distance;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = ray.GetPoint(1.4f);
            wall.transform.localScale = Vector3.one * .5f;
            Physics.SyncTransforms();
            tool.TryUse(player.characterCamera, player.transform);
            await Gesture();
            if (bag.Kilograms != initialBag || tool.Kilograms != initialLoad) throw new Exception("Poured through an obstruction");
        }
        finally { UnityEngine.Object.Destroy(wall); }
        await Task.Delay(50);
        player.transform.position = bag.transform.position + new Vector3(0, .1f, -5f);
        player.characterCamera.transform.LookAt(bag.PourPoint);
        Physics.SyncTransforms();
        tool.TryUse(player.characterCamera, player.transform);
        await Gesture();
        if (bag.Kilograms != initialBag || tool.Kilograms != initialLoad) throw new Exception("Pour range was not bounded");
        player.transform.position = new Vector3(-.8f, .04f, -8.35f);
        player.characterCamera.transform.LookAt(bag.PourPoint);
        Physics.SyncTransforms();
        tool.TryUse(player.characterCamera, player.transform);
        await Gesture();
        if (tool.Kilograms > .001f || Mathf.Abs(bag.Kilograms - initialBag - initialLoad) > .001f)
            throw new Exception("Reported bag approach still fails to transfer all corn");
        return "PASS: bag at " + distance + "m accepts " + initialLoad +
            "kg from the reproduced approach; blocked and over-4m attempts transfer nothing; mass conserved.";
    }
    private static async Task Gesture()
    {
        for (int i = 0; i < 90; i++)
        {
            tool.Tick(1f / 60f); player.SendMessage("LateUpdate");
            await Task.Delay(17);
        }
    }
}
