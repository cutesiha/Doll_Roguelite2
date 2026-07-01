using System.Collections.Generic;
using UnityEngine;

// 특정 씬에서만 플레이어 스프라이트에 색조(틴트)를 입힌다.
// PlayerController가 셋업 시 스프라이트 색을 흰색으로 되돌리므로 LateUpdate에서 매 프레임 재적용한다.
// (그림자/눈 소켓 같은 어두운 보조 렌더러는 제외)
[DisallowMultipleComponent]
public class PlayerSceneTint : MonoBehaviour
{
    [Tooltip("스프라이트 색에 곱해지는 틴트. 창백한 쿨톤은 파란빛이 살짝 도는 밝은 회청색.")]
    [SerializeField] Color tint = new Color(0.80f, 0.86f, 0.98f, 1f);

    readonly List<SpriteRenderer> targets = new List<SpriteRenderer>();
    int lastRendererCount = -1;

    void LateUpdate()
    {
        // 런타임에 눈 소켓 등 렌더러가 추가될 수 있으므로 개수가 바뀌면 다시 수집한다.
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        if (all.Length != lastRendererCount)
        {
            lastRendererCount = all.Length;
            targets.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.Contains("Shadow") || n.Contains("Socket"))
                    continue;
                targets.Add(all[i]);
            }
        }

        for (int i = 0; i < targets.Count; i++)
            if (targets[i] != null)
                targets[i].color = tint;
    }
}
