
using UnityEngine;
using UnityEngine.Events;

public class ReceiveStringEvents : MonoBehaviour
{
    [SerializeField] private SendReceiveStringEventSO sendReceiveStringEventSO;
    [SerializeField] private UnityEvent<string> response;

    private void OnEnable()
    {
        sendReceiveStringEventSO.OnEventRaised += OnEventRaised;
    }

    private void OnDisable()
    {
        sendReceiveStringEventSO.OnEventRaised -= OnEventRaised;
    }

    private void OnEventRaised(string result)
    {
        response?.Invoke(result);
        
    }
}
