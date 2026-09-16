using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    private InputSystem_Actions inputActions;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable()
    {
        
    }
    void OnDisable()
    {
        
    }
}
