using System;
using UnityEngine;

public class CharacterPromptReceiver : MonoBehaviour, ICharacterPromptReceiver
{
    public Action<Vector2> OnMoveInput { get; set; }
    public Action OnJumpInput { get; set; }
    public Action OnCancelJumpInput { get; set; }
    public Action OnJumpCancel { get; set; }
}
