using UnityEngine;

public class GearCollectible : Collectible
{
    [Header("Gear")]
    public string gearId;

    private void Reset()
    {
        collectibleType = CollectibleType.Gear;
    }

    public override bool TryCollect(Collider other)
    {
        collectibleType = CollectibleType.Gear;

        if (!string.IsNullOrWhiteSpace(gearId))
            collectibleId = gearId;

        return base.TryCollect(other);
    }
}
