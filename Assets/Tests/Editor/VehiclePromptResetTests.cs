using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class VehiclePromptResetTests
{
    private GameObject root;
    private PlayerInteractor interactor;
    private float previousTimeScale;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale=Time.timeScale;Time.timeScale=1;
        root=new GameObject("Vehicle prompt regression");
        interactor=Child("Player").AddComponent<PlayerInteractor>();
    }
    [TearDown]
    public void TearDown(){Object.DestroyImmediate(root);Time.timeScale=previousTimeScale;}

    [Test]
    public void AimedParkedVehicleOffersResetAndLosingTargetCancelsHold()
    {
        var bike=CreateBike();
        var reset=bike.gameObject.AddComponent<VehicleReset>();reset.SetHomePose();
        Vector3 home=bike.transform.position;
        bike.transform.position+=Vector3.right*5;
        CallResetTarget(bike);
        var data=ResetPrompt();
        Assert.AreEqual(1,data.AdditionalActions.Length);
        Assert.That(data.AdditionalActions[0].Key,Is.EqualTo(KeyCode.R));
        Assert.IsFalse(reset.TickHold(true,reset.HoldSeconds*.5f));
        Assert.Greater(reset.Progress,0);
        CallResetTarget(null);
        Assert.That(reset.Progress,Is.Zero);
        CallResetTarget(bike);
        Assert.IsTrue(reset.TickHold(true,reset.HoldSeconds));
        Assert.IsTrue(reset.IsResetting);
        typeof(VehicleReset).GetMethod("AdvanceReset",Private).Invoke(reset,new object[]{.6f});
        Assert.IsFalse(reset.IsResetting);
        Assert.That(bike.transform.localScale,Is.EqualTo(Vector3.one));
        Assert.That(Vector3.Distance(bike.transform.position,home),Is.LessThan(.001f));
    }

    [Test]
    public void AnimatedResetKeepsPhysicsScaleAndRestoresVisualScaleOnRepeatedResets()
    {
        var bike=CreateBike();
        bike.transform.localScale=new Vector3(1.2f,1.2f,1.2f);
        var reset=bike.gameObject.AddComponent<VehicleReset>();reset.SetHomePose();
        for(int i=0;i<3;i++)
        {
            reset.TickHold(false,.01f);
            Assert.IsTrue(reset.TickHold(true,reset.HoldSeconds));
            var pose=bike.GetComponent<VehicleGroundAlignment>().Pose;
            Vector3 normalPoseScale=pose.localScale;
            typeof(VehicleReset).GetMethod("AdvanceReset",Private).Invoke(reset,new object[]{.24f});
            Assert.That(bike.transform.localScale,Is.EqualTo(Vector3.one*1.2f));
            Vector3 position=pose.localPosition;
            bike.GetComponent<VehicleGroundAlignment>().Align(1f);
            Assert.That(pose.localPosition,Is.EqualTo(position));
            typeof(VehicleReset).GetMethod("AdvanceReset",Private).Invoke(reset,new object[]{.4f});
            Assert.That(pose.localScale,Is.EqualTo(normalPoseScale));
            Assert.That(bike.transform.localScale,Is.EqualTo(Vector3.one*1.2f));
            Assert.IsFalse(reset.IsResetting);
        }
    }

    [Test]
    public void IdleWheelbarrowDoesNotOfferOrPerformReset()
    {
        var barrow=Child("Idle barrow").AddComponent<WheelbarrowCargo>();
        var reset=barrow.gameObject.AddComponent<VehicleReset>();reset.SetHomePose();
        CallResetTarget(barrow);
        Assert.IsNull(ResetPrompt().AdditionalActions);
        Assert.IsFalse(reset.TickHold(true,reset.HoldSeconds+1));
    }

    [Test]
    public void RiddenVehicleCannotResetAndCancelsAnExistingHold()
    {
        var bike=CreateBike();
        var reset=bike.gameObject.AddComponent<VehicleReset>();reset.SetHomePose();
        bike.transform.position+=Vector3.right*5;
        Vector3 parkedPosition=bike.transform.position;
        Assert.IsFalse(reset.TickHold(true,reset.HoldSeconds*.5f));
        bike.SetRidden(true);
        Assert.IsFalse(reset.CanReset);
        Assert.IsFalse(reset.TickHold(true,reset.HoldSeconds));
        Assert.That(reset.Progress,Is.Zero);
        Assert.IsFalse(reset.IsResetting);
        Assert.That(bike.transform.position,Is.EqualTo(parkedPosition));
    }

    [Test]
    public void PushedWheelbarrowKeepsResetButNeverUsesBagFillProgress()
    {
        var barrow=Child("Pushed barrow").AddComponent<WheelbarrowCargo>();
        var reset=barrow.gameObject.AddComponent<VehicleReset>();reset.SetHomePose();
        Set(barrow,"pusher",interactor);interactor.PushedWheelbarrow=barrow;
        CallResetTarget(null);
        var data=ResetPrompt();
        Assert.AreEqual(1,data.AdditionalActions.Length);
        Assert.That(data.AdditionalActions[0].Key,Is.EqualTo(KeyCode.R));
        Assert.IsFalse(data.ShowProgress);Assert.IsFalse(data.AdditionalActions[0].ShowProgress);
        Set(barrow,"pusher",null);CallResetTarget(barrow);
        Assert.IsNull(ResetPrompt().AdditionalActions);
    }

    [Test]
    public void VehiclePromptAndItsExtraActionsClearPreviouslyVisibleBagFill()
    {
        var canvas=Child("Prompt canvas",typeof(RectTransform)).GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(600,600);
        var panel=Child("Prompt panel",typeof(RectTransform));panel.transform.SetParent(canvas,false);var panelRect=panel.GetComponent<RectTransform>();panelRect.sizeDelta=new Vector2(200,50);
        var progress=Child("Bag fill",typeof(RectTransform));progress.transform.SetParent(panel.transform,false);
        var fill=Child("Bag fill image",typeof(RectTransform),typeof(Image)).GetComponent<Image>();fill.transform.SetParent(progress.transform,false);
        var status=Child("Bag fill text",typeof(RectTransform),typeof(Text)).GetComponent<Text>();status.transform.SetParent(progress.transform,false);
        var prompt=canvas.gameObject.AddComponent<InteractionPrompt>();
        Set(prompt,"panel",panelRect);Set(prompt,"canvasRect",canvas);Set(prompt,"bagSliderFill",progress);Set(prompt,"bagFill",fill);Set(prompt,"bagFillStatus",status);
        var camera=Child("Prompt camera",typeof(Camera)).GetComponent<Camera>();
        var bagData=new InteractionPromptData(Vector3.forward,"Fill"){ShowProgress=true,Progress=.5f,ProgressText="5 / 10 kg"};
        prompt.Show(camera,bagData);Assert.IsTrue(progress.activeSelf);
        var vehicleData=new InteractionPromptData(Vector3.forward,"Drive vehicle"){AdditionalActions=new[]{new InteractionPromptAction("Upgrade vehicle",KeyCode.F)}};
        prompt.Show(camera,vehicleData);
        Assert.IsTrue(prompt.IsVisible);Assert.IsFalse(progress.activeSelf);
        var rows=(System.Collections.IList)typeof(InteractionPrompt).GetField("actionRows",Private).GetValue(prompt);
        Assert.IsFalse(((GameObject)rows[0].GetType().GetField("progress").GetValue(rows[0])).activeSelf);
        prompt.Show(camera,bagData);Assert.IsTrue(progress.activeSelf);
        prompt.Hide();Assert.IsFalse(progress.activeSelf);Assert.That(fill.fillAmount,Is.Zero);Assert.That(status.text,Is.Empty);
    }

    private BicycleController CreateBike()
    {
        var bike=Child("Vehicle").AddComponent<BicycleController>();
        bike.transform.position=new Vector3(25000,1000,25000);bike.motorDriven=true;
        bike.steering=Child("Steering").transform;bike.steering.SetParent(bike.transform,false);
        bike.frontWheel=Child("Front wheel").transform;bike.frontWheel.SetParent(bike.transform,false);
        bike.rearWheel=Child("Rear wheel").transform;bike.rearWheel.SetParent(bike.transform,false);
        return bike;
    }
    private InteractionPromptData ResetPrompt()
    {
        object[] args={new InteractionPromptData(Vector3.forward,"Drive vehicle")};
        typeof(PlayerInteractor).GetMethod("AddResetAction",Private).Invoke(interactor,args);
        return (InteractionPromptData)args[0];
    }
    private void CallResetTarget(IInteractable target) => typeof(PlayerInteractor).GetMethod("UpdateResetTarget",Private).Invoke(interactor,new object[]{target});
    private GameObject Child(string name,params System.Type[] components){var go=new GameObject(name,components);go.transform.SetParent(root.transform,false);return go;}
    private static void Set(object target,string name,object value) => target.GetType().GetField(name,Private).SetValue(target,value);
}
