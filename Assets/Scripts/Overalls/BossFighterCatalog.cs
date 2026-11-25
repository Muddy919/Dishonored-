using UnityEngine;

/// <summary>
/// Maps Character Select indices to fighter prefabs for Boss Fights spawning.
/// Loaded from Resources/BossFighterCatalog.
/// </summary>
[CreateAssetMenu(menuName = "BusterBots/Boss Fighter Catalog")]
public class BossFighterCatalog : ScriptableObject
{
    static BossFighterCatalog cached;

    [Tooltip("Order MUST match Character Select characterIndex: Pacer, Buster, Gastino, Xebec, Hendrix, Bigbata, Macky, Rodeo, Glamour")]
    public GameObject[] fighters = new GameObject[9];

    public static BossFighterCatalog Get()
    {
        if (cached != null)
            return cached;

        cached = Resources.Load<BossFighterCatalog>("BossFighterCatalog");
        return cached;
    }

    public GameObject GetFighter(int characterIndex)
    {
        if (fighters == null || characterIndex < 0 || characterIndex >= fighters.Length)
            return null;
        return fighters[characterIndex];
    }
}
