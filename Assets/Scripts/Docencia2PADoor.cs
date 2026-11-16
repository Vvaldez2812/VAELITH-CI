using UnityEngine;
namespace Vaelith.Docencia2 {
 // Independent PA door: never references or edits PB objects.
 public class Docencia2PADoor : MonoBehaviour {
  public Transform hinge;
  public float inwardAngle=90;
  public bool isOpen;
  public void SetOpen(bool value) {
   isOpen=value;
   if(hinge){var interactive=hinge.GetComponent<Docencia2InteractiveDoor>();
    if(interactive)interactive.SetOpen(value);
    else hinge.localRotation=Quaternion.Euler(0,value?inwardAngle:0,0);}
  }
 }
}
