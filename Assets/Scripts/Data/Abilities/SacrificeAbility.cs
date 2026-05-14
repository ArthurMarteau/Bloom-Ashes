using UnityEngine;

[CreateAssetMenu(fileName = "Sacrifice_Ability", menuName = "Bloom and Ashes/Abilities/Sacrifice")]
public class SacrificeAbility : AbilityData
{
    public int watersapGranted = 10;

    public override IAbility CreateExecutor() => new SacrificeExecutor(this);
}
