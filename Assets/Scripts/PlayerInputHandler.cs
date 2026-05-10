using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Point unique de lecture des inputs joueur.
/// Lit directement Keyboard.current et Mouse.current (pas besoin de configurer des InputActions).
/// Tous les autres systèmes consomment les événements ou lisent les propriétés.
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlayerInputHandler : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Événements — Actions discrètes (press)
    // ─────────────────────────────────────────────────────────────────────────

    public event Action OnAttackPressed;
    public event Action OnInteractPressed;
    public event Action OnToggleInventory;
    public event Action<int> OnSlotSelected;

    // ─────────────────────────────────────────────────────────────────────────
    // Propriétés — État continu (lu par-frame par les consumers)
    // ─────────────────────────────────────────────────────────────────────────

    public Vector2 MoveInput { get; private set; }
    public bool IsSprintHeld { get; private set; }
    public bool IsAimHeld { get; private set; }
    public Vector2 MousePosition { get; private set; }
    public Vector2 MouseDelta { get; private set; }
    public float ScrollDelta { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Update — Lecture centralisée de tous les inputs
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isLocalPlayer) return;

        ReadContinuousState();
        ReadDiscreteActions();
    }

    private void ReadContinuousState()
    {
        Vector2 move = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) move.y += 1f;
            if (Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.dKey.isPressed) move.x += 1f;
            if (Keyboard.current.aKey.isPressed) move.x -= 1f;

            if (move.sqrMagnitude > 1f) move.Normalize();

            IsSprintHeld = Keyboard.current.leftShiftKey.isPressed;
        }
        else
        {
            IsSprintHeld = false;
        }

        MoveInput = move;

        if (Mouse.current != null)
        {
            IsAimHeld = Mouse.current.rightButton.isPressed;
            MousePosition = Mouse.current.position.ReadValue();
            MouseDelta = Mouse.current.delta.ReadValue();
            ScrollDelta = Mouse.current.scroll.ReadValue().y;
        }
        else
        {
            IsAimHeld = false;
            MousePosition = Vector2.zero;
            MouseDelta = Vector2.zero;
            ScrollDelta = 0f;
        }
    }

    private void ReadDiscreteActions()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            OnAttackPressed?.Invoke();
        }

        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnInteractPressed?.Invoke();
        }

        if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
        {
            OnToggleInventory?.Invoke();
        }

        ReadSlotSelection();
    }

    private void ReadSlotSelection()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) OnSlotSelected?.Invoke(0);
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) OnSlotSelected?.Invoke(1);
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) OnSlotSelected?.Invoke(2);
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) OnSlotSelected?.Invoke(3);
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) OnSlotSelected?.Invoke(4);
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) OnSlotSelected?.Invoke(5);
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) OnSlotSelected?.Invoke(6);
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) OnSlotSelected?.Invoke(7);
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) OnSlotSelected?.Invoke(8);
    }
}
