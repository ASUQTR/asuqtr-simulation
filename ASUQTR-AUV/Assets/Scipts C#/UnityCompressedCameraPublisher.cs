using System;
using UnityEngine;

/// <summary>
/// Captures a Unity Camera and publishes JPEG frames through rosbridge as
/// sensor_msgs/CompressedImage.
/// </summary>
public class UnityCompressedCameraPublisher : MonoBehaviour
{
    [Header("Camera")]
    public Camera cameraSource;

    [Header("ROS")]
    public string topic = "/unity/camera/front/compressed";
    public string frameId = "camera_front";

    [Header("Capture")]
    [Min(1)] public int width = 640;
    [Min(1)] public int height = 360;
    [Min(0.1f)] public float publishRateHz = 8f;
    [Range(1, 100)] public int jpegQuality = 60;
    public bool publishEnabled = true;

    private RenderTexture captureRenderTexture;
    private Texture2D captureTexture;
    private float nextPublishTime;
    private bool topicAdvertised;
    private bool firstImageLogged;
    private bool captureErrorLogged;

    void Awake()
    {
        if (cameraSource == null)
            cameraSource = GetComponent<Camera>();
    }

    void Start()
    {
        EnsureCaptureResources();
        Debug.Log(
            $"[UnityCameraPublisher] Initialized {name}: {width}x{height} at {publishRateHz:F1} Hz -> {topic}",
            this
        );
    }

    void LateUpdate()
    {
        if (!publishEnabled || cameraSource == null)
            return;

        SimpleRosSocket socket = SimpleRosSocket.Instance;
        if (socket == null || !socket.IsConnected)
            return;

        TryAdvertiseTopic(socket);

        if (Time.unscaledTime < nextPublishTime)
            return;

        nextPublishTime = Time.unscaledTime + 1f / Mathf.Max(0.1f, publishRateHz);
        CaptureAndPublish(socket);
    }

    void TryAdvertiseTopic(SimpleRosSocket socket)
    {
        if (topicAdvertised)
            return;

        socket.Advertise(topic, "sensor_msgs/CompressedImage");
        topicAdvertised = true;
    }

    void EnsureCaptureResources()
    {
        int captureWidth = Mathf.Max(1, width);
        int captureHeight = Mathf.Max(1, height);

        if (captureRenderTexture != null &&
            captureTexture != null &&
            captureRenderTexture.width == captureWidth &&
            captureRenderTexture.height == captureHeight)
        {
            return;
        }

        ReleaseCaptureResources();

        captureRenderTexture = new RenderTexture(
            captureWidth,
            captureHeight,
            24,
            RenderTextureFormat.ARGB32
        )
        {
            name = $"{name}_ROS_Capture",
            useMipMap = false,
            autoGenerateMips = false
        };
        captureRenderTexture.Create();

        captureTexture = new Texture2D(
            captureWidth,
            captureHeight,
            TextureFormat.RGB24,
            false
        )
        {
            name = $"{name}_ROS_JPEG"
        };
    }

    void CaptureAndPublish(SimpleRosSocket socket)
    {
        RenderTexture previousTarget = cameraSource.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Rect previousRect = cameraSource.rect;

        try
        {
            EnsureCaptureResources();

            cameraSource.targetTexture = captureRenderTexture;
            cameraSource.rect = new Rect(0f, 0f, 1f, 1f);
            cameraSource.Render();

            RenderTexture.active = captureRenderTexture;
            captureTexture.ReadPixels(
                new Rect(0, 0, captureRenderTexture.width, captureRenderTexture.height),
                0,
                0,
                false
            );
            captureTexture.Apply(false, false);

            byte[] jpegData = captureTexture.EncodeToJPG(Mathf.Clamp(jpegQuality, 1, 100));
            string base64Data = Convert.ToBase64String(jpegData);

            double now = Time.timeAsDouble;
            uint sec = (uint)now;
            uint nanosec = (uint)((now - sec) * 1e9);

            string message =
                "{\"op\":\"publish\"," +
                "\"topic\":\"" + topic + "\"," +
                "\"msg\":{" +
                    "\"header\":{" +
                        "\"stamp\":{\"sec\":" + sec + ",\"nanosec\":" + nanosec + "}," +
                        "\"frame_id\":\"" + frameId + "\"" +
                    "}," +
                    "\"format\":\"jpeg\"," +
                    "\"data\":\"" + base64Data + "\"" +
                "}}";

            socket.Send(message);

            if (!firstImageLogged)
            {
                firstImageLogged = true;
                Debug.Log($"[UnityCameraPublisher] First image published on {topic}", this);
            }
        }
        catch (Exception exception)
        {
            if (!captureErrorLogged)
            {
                captureErrorLogged = true;
                Debug.LogError(
                    $"[UnityCameraPublisher] Capture failed for {name}: {exception.Message}",
                    this
                );
            }
        }
        finally
        {
            cameraSource.targetTexture = previousTarget;
            cameraSource.rect = previousRect;
            RenderTexture.active = previousActive;
        }
    }

    void ReleaseCaptureResources()
    {
        if (captureRenderTexture != null)
        {
            if (captureRenderTexture.IsCreated())
                captureRenderTexture.Release();

            Destroy(captureRenderTexture);
            captureRenderTexture = null;
        }

        if (captureTexture != null)
        {
            Destroy(captureTexture);
            captureTexture = null;
        }
    }

    void OnDestroy()
    {
        ReleaseCaptureResources();
    }
}
