using UnityEngine;

// Applies the effect of a jewel (Gem) when the player activates it with Q. Each jewel type
// triggers a different temporary buff / utility. The jewel is consumed by the caller.
public static class JewelAbility
{
    const float GuardDuration = 4f;   // 무적 지속
    const float HasteDuration = 5f;   // 질주 지속
    const float HasteMultiplier = 1.7f;
    const float PowerDuration = 6f;   // 광폭 지속
    const int PowerBonus = 2;         // 추가 공격력

    // Returns true if the effect was applied (and the jewel should be consumed).
    public static bool Activate(BodyPart jewel)
    {
        if (jewel == null || !jewel.IsJewel)
            return false;

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return false;

        switch (jewel.jewelType)
        {
            case JewelType.Guard:
            {
                PlayerDamageReceiver receiver = playerObject.GetComponent<PlayerDamageReceiver>();
                if (receiver == null)
                    return false;
                receiver.SetInvincible(GuardDuration);
                Announce("무적 보석 발동! 잠시 무적");
                break;
            }
            case JewelType.Haste:
            {
                PlayerController controller = playerObject.GetComponent<PlayerController>();
                if (controller == null)
                    return false;
                controller.ApplySpeedBuff(HasteMultiplier, HasteDuration);
                Announce("질주 보석 발동! 이동속도 증가");
                break;
            }
            case JewelType.Power:
            {
                PlayerAttack attack = playerObject.GetComponent<PlayerAttack>();
                if (attack == null)
                    return false;
                attack.ApplyTemporaryDamageBonus(PowerBonus, PowerDuration);
                Announce("광폭 보석 발동! 공격력 증가");
                break;
            }
            case JewelType.Mend:
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory == null)
                    return false;
                inventory.RepairAllParts();
                Announce("수선 보석 발동! 부위 회복");
                break;
            }
            default:
                return false;
        }

        SoundManager.PlayPanel();
        CameraShake.Shake(0.12f, 0.12f);
        return true;
    }

    static void Announce(string message)
    {
        RunHudUI hud = Object.FindFirstObjectByType<RunHudUI>();
        if (hud != null)
            hud.ShowDiaryText(message);
    }
}
