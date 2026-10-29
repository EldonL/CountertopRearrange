using UnityEngine;
using UnityEngine.Events;
[CreateAssetMenu(menuName = "Events/SendReceiveStringEvent", fileName = "SendReceiveStringEventSO")]
public class SendReceiveStringEventSO : ScriptableObject
{
    public UnityAction<string> OnEventRaised;

    public void RaiseEvent(string result)
    {
        if (OnEventRaised != null)
            OnEventRaised.Invoke(result);
    }
}
