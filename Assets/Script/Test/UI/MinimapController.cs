using UnityEngine;

public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

    [Header("Camera Settings")]
    [SerializeField] private float orthographicSize = 22f;
    [SerializeField] private float heightOffset = 25f;
    [SerializeField] private int renderTextureSize = 512;

    private Camera _minimapCamera;
    private RenderTexture _minimapTexture;
    private Transform _playerTransform;

    public RenderTexture MinimapTexture => _minimapTexture;
    public float OrthographicSize => _minimapCamera != null ? _minimapCamera.orthographicSize : orthographicSize;

    public float CameraYawRotation
    {
        get
        {
            if (Camera.main != null)
                return Camera.main.transform.eulerAngles.y;
            if (_playerTransform != null)
                return _playerTransform.eulerAngles.y;
            return 0f;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SetupRenderTexture();
        SetupCamera();
        UpdatePlayerReference();
    }

    private void SetupRenderTexture()
    {
        if (_minimapTexture == null)
        {
            _minimapTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "MinimapRenderTexture",
                filterMode = FilterMode.Bilinear
            };
            _minimapTexture.Create();
        }
    }

    private void SetupCamera()
    {
        GameObject camObj = new GameObject("MinimapCamera_Internal");
        camObj.transform.SetParent(transform);
        
        _minimapCamera = camObj.AddComponent<Camera>();
        _minimapCamera.orthographic = true;
        _minimapCamera.orthographicSize = orthographicSize;
        _minimapCamera.nearClipPlane = 0.5f;
        _minimapCamera.farClipPlane = 100f;
        _minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        _minimapCamera.backgroundColor = new Color(12f / 255f, 16f / 255f, 24f / 255f, 1f);
        _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Exclude UI, Ignore Raycast, Player, and Weapon layers
        int cullingMask = ~(1 << LayerMask.NameToLayer("UI") | 1 << LayerMask.NameToLayer("Ignore Raycast"));
        int pLayer = LayerMask.NameToLayer("Player");
        if (pLayer >= 0) cullingMask &= ~(1 << pLayer);
        int wLayer = LayerMask.NameToLayer("Weapon");
        if (wLayer >= 0) cullingMask &= ~(1 << wLayer);
        
        _minimapCamera.cullingMask = cullingMask;
        _minimapCamera.useOcclusionCulling = false;
        _minimapCamera.targetTexture = _minimapTexture;
        _minimapCamera.enabled = false; // Disabled by default until widget requests active view
    }

    private void Start()
    {
        UpdatePlayerReference();
    }

    private void UpdatePlayerReference()
    {
        if (_playerTransform == null)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj != null)
            {
                _playerTransform = pObj.transform;
            }
        }
    }

    public void SetCameraActive(bool active)
    {
        if (_minimapCamera != null)
        {
            _minimapCamera.enabled = active;
        }
    }

    private void LateUpdate()
    {
        UpdatePlayerReference();

        if (_playerTransform != null && _minimapCamera != null)
        {
            Vector3 pos = _playerTransform.position;
            _minimapCamera.transform.position = new Vector3(pos.x, pos.y + heightOffset, pos.z);
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    private bool _previousFogState;
    private float _previousShadowDistance;
    private bool _isShadowDistanceModified;

    private void OnEnable()
    {
        Camera.onPreRender += OnCameraPreRender;
        Camera.onPostRender += OnCameraPostRender;
    }

    private void OnDisable()
    {
        Camera.onPreRender -= OnCameraPreRender;
        Camera.onPostRender -= OnCameraPostRender;
        RestoreShadowDistance();
    }

    private void OnCameraPreRender(Camera cam)
    {
        if (_minimapCamera != null && cam == _minimapCamera)
        {
            _previousFogState = RenderSettings.fog;
            RenderSettings.fog = false;

            if (!_isShadowDistanceModified && QualitySettings.shadowDistance > 0f)
            {
                _previousShadowDistance = QualitySettings.shadowDistance;
                QualitySettings.shadowDistance = 0f;
                _isShadowDistanceModified = true;
            }
        }
    }

    private void OnCameraPostRender(Camera cam)
    {
        if (_minimapCamera != null && cam == _minimapCamera)
        {
            RenderSettings.fog = _previousFogState;
            RestoreShadowDistance();
        }
    }

    private void RestoreShadowDistance()
    {
        if (_isShadowDistanceModified)
        {
            QualitySettings.shadowDistance = _previousShadowDistance;
            _isShadowDistanceModified = false;
        }
    }

    private void OnDestroy()
    {
        OnDisable();

        if (_minimapTexture != null)
        {
            if (_minimapCamera != null)
            {
                _minimapCamera.targetTexture = null;
            }
            _minimapTexture.Release();
            Destroy(_minimapTexture);
            _minimapTexture = null;
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
