using UnityEngine;
/// <summary>Selects the authored held tool for the saved upgrade tier.</summary>
public sealed class HeldGrainToolLevels : MonoBehaviour
{
    public GameObject[] levels;
    private ShovelUpgradeSettings settings;
    private ShovelTool tool;
    private int selected=-1;
    public bool UsesAuthoredShovelPose => selected >= 1 && selected <= 3;
    private void Awake(){settings=Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);tool=GetComponent<ShovelTool>();Apply();}
    private void Update()=>Apply();
    private void Apply()
    {
        if(settings==null||!settings.IsConfigured||levels==null||levels.Length==0)return;
        int index=Mathf.Clamp(settings.TypeIndex(SaveSystem.Data?.shovelUpgradeLevel??0),0,levels.Length-1);
        if(index==selected)return;
        tool.CancelAction();
        for(int i=0;i<levels.Length;i++)if(levels[i]!=null)levels[i].SetActive(i==index);
        selected=index;
        if(levels[index]!=null)tool.BindShovel(levels[index].GetComponent<HeldShovelPose>());
    }
}
