using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace WesternLemegeton
{
 public sealed class PassiveSlotView:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
 {
  public Image Icon,Border;public Text Tooltip;public string Description;
  public void OnPointerEnter(PointerEventData e){if(Tooltip){Tooltip.text=Description;Tooltip.gameObject.SetActive(true);}}
  public void OnPointerExit(PointerEventData e){if(Tooltip)Tooltip.gameObject.SetActive(false);}
 }
}
