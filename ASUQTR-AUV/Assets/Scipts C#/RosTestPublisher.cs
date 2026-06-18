/*using UnityEngine;

public class RosTestPublisher : MonoBehaviour
{
    void Start()
    {
        WebSocketClient.Instance.OnConnected += OnRosConnected;
    }

    void OnRosConnected()
    {
        Debug.Log("ROS connected → publishing test point");

        WebSocketClient.Instance.PublishPointStamped(
            "/test_point",
            new Vector3(1f, 2f, 3f)
        );
    }

    void OnDestroy()
    {
        if (WebSocketClient.Instance != null)
            WebSocketClient.Instance.OnConnected -= OnRosConnected;
    }
}
*/