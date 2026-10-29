using UnityEngine;
public sealed class LaunchRoomInteractable : MonoBehaviour, IInteractable
{
    public HangarLaunchSequence sequence;
    public bool isSwitch;
    public bool TryGetPrompt(PlayerInteractor interactor,out InteractionPromptData prompt)
    {
        prompt=default;if(sequence==null||sequence.Running)return false;
        if(isSwitch){if((!sequence.DoorOpen && !sequence.BypassRestrictions) || !IsInsideRoom(interactor))return false;prompt=new InteractionPromptData(transform.position,"Switch on power");prompt.RequiresFreeHands=interactor!=null&&interactor.HandsOccupied;return true;}
        if(sequence.DoorOpen)return false;
        bool ready=sequence.CanOpen;prompt=new InteractionPromptData(transform.position,ready?"Open door":"Clear Hangar 5 and this doorway",ready);return true;
    }
    private bool IsInsideRoom(PlayerInteractor interactor)
    {
        if (interactor == null || sequence == null) return false;
        Vector3 p = sequence.transform.InverseTransformPoint(interactor.transform.position);
        return p.x > -6.95f && p.x < 6.95f && p.z > 0.15f && p.z < 10.7f;
    }
    public bool TryInteract(PlayerInteractor interactor)=>sequence!=null&&(isSwitch?(IsInsideRoom(interactor) && sequence.UseSwitch(interactor)):sequence.UseDoor());
}



