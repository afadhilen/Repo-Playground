using UnityEngine;

public class CameraPanning : MonoBehaviour
{
    public float speed = 2f;
    public float amount = 2f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float offsetX = Mathf.Sin(Time.time * speed) * amount;
        float offsetY = Mathf.Cos(Time.time * speed * 0.5f) * (amount / 2);

        transform.position = startPosition + new Vector3(offsetX, offsetY, 0);
    }
}