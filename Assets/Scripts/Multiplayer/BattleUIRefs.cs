using UnityEngine;

/// <summary>
/// Put on any GameObject in the Battle scene (e.g. an empty "UI" object).
/// Drag your two on-screen HealthBar elements in - one for whichever fighter
/// belongs to the host, one for whichever belongs to the client.
///
/// This is pure LOCAL UI wiring, not networked - it runs independently and
/// identically on both players' own machines, which is exactly what's needed
/// since each player's Canvas is their own.
/// </summary>
public class BattleUIRefs : MonoBehaviour
{
    public static BattleUIRefs Instance { get; private set; }

    public HealthBar hostHealthBar;
    public HealthBar clientHealthBar;

    private void Awake() => Instance = this;
}
