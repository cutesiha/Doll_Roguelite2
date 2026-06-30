using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] string targetTag = "Player";
    [SerializeField] Vector2 backgroundSize = new Vector2(38.4f, 21.6f);
    [SerializeField] Vector2 backgroundCenter = Vector2.zero;
    [SerializeField] float orthographicSize = 6.2f;
    [SerializeField, Min(0.1f)] float followSharpness = 20f;
    [SerializeField] bool clampToBackground = true;

    Camera targetCamera;

    void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.orthographic = true;
        targetCamera.orthographicSize = orthographicSize;
        ResolveTarget();
    }

    void Start()
    {
        SnapToTarget();
    }

    public void ConfigureBounds(Vector2 size, Vector2 center, float cameraOrthographicSize, bool clamp)
    {
        backgroundSize = new Vector2(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y));
        backgroundCenter = center;
        orthographicSize = Mathf.Max(0.1f, cameraOrthographicSize);
        clampToBackground = clamp;

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = orthographicSize;
        }

        SnapToTarget();
    }

    void LateUpdate()
    {
        if (target == null)
            ResolveTarget();

        if (target == null)
            return;

        Vector3 desiredPosition = GetTargetCameraPosition();
        float followT = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followT);
    }

    void ResolveTarget()
    {
        if (target != null || string.IsNullOrWhiteSpace(targetTag))
            return;

        GameObject targetObject = GameObject.FindWithTag(targetTag);
        if (targetObject != null)
            target = targetObject.transform;
    }

    void SnapToTarget()
    {
        if (target == null)
            ResolveTarget();

        if (target != null)
            transform.position = GetTargetCameraPosition();
    }

    Vector3 GetTargetCameraPosition()
    {
        // 한다리 hop 비주얼이 transform에 실리므로 카메라는 rb.position(물리 위치)을 따라간다.
        // rb가 없으면 transform.position을 그대로 사용한다.
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        float x = targetRb != null ? targetRb.position.x : target.position.x;
        float y = targetRb != null ? targetRb.position.y : target.position.y;
        Vector3 desiredPosition = new Vector3(x, y, transform.position.z);
        return clampToBackground ? ClampToBackground(desiredPosition) : desiredPosition;
    }

    Vector3 ClampToBackground(Vector3 position)
    {
        float cameraHalfHeight = targetCamera.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * targetCamera.aspect;

        float minX = backgroundCenter.x - backgroundSize.x * 0.5f + cameraHalfWidth;
        float maxX = backgroundCenter.x + backgroundSize.x * 0.5f - cameraHalfWidth;
        float minY = backgroundCenter.y - backgroundSize.y * 0.5f + cameraHalfHeight;
        float maxY = backgroundCenter.y + backgroundSize.y * 0.5f - cameraHalfHeight;

        position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : backgroundCenter.x;
        position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : backgroundCenter.y;
        return position;
    }
}
