using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using Vuforia;
using Debug = UnityEngine.Debug;

/// <summary>
/// Minimal paper-oriented instrumentation for the Vuforia Model Target route.
/// Writes one CSV row per Unity frame and prints copy-paste summary lines.
/// Assign an optional Ground Truth transform to compute translation/rotation error.
/// </summary>
public class VuforiaExperimentLogger : MonoBehaviour
{
    [Header("Experiment")]
    [Tooltip("Condition name written into CSV, e.g. rear_visible / occlusion_partial / front_glass")]
    public string conditionLabel = "unnamed";

    [Tooltip("If set, translation error is computed against this transform.")]
    public Transform groundTruth;

    [Tooltip("Plan B tape-measure GT only constrains position. Leave off unless you have independent orientation GT.")]
    public bool computeRotationError;

    [Header("Dataset labels (Vuforia has no public MotionHint/TrackingMode getters)")]
    public string datasetTrackingMode = "CAR";
    public string datasetMotionHint = "ADAPTIVE";

    [Header("Logging")]
    public bool writeCsv = true;
    public int consoleEveryNFrames = 120;
    public bool logGpu = true;
    public bool showOnScreenOverlay = true;

    ObserverBehaviour _observer;
    StreamWriter _csv;
    string _csvPath;
    string _summaryPath;
    readonly StringBuilder _line = new StringBuilder(512);
    bool _closed;
    float _lastStateCallbackUnscaled = -1f;

    float _sessionStartUnscaled;
    float _vuforiaStartedUnscaled = -1f;
    float _firstTrackedUnscaled = -1f;
    float _lastStateUpdateMs;
    float _lastLoggedUnscaled;

    int _frameIdx;
    int _framesTracked;
    int _framesExtended;
    int _framesLimited;
    int _framesNoPose;
    int _droppedFrames;
    int _statusChanges;

    double _sumFps;
    double _sumDtMs;
    double _sumStateUpdateMs;
    double _sumJitterCm;
    double _sumJitterDeg;
    double _sumTransErrCm;
    double _sumRotErrDeg;
    int _errorSamples;
    int _jitterSamples;
    int _gpuSamples;
    double _sumGfxMb;
    double _sumUnityAllocMb;

    Vector3 _prevPos;
    Quaternion _prevRot;
    bool _hasPrevPose;
    Status _prevStatus = Status.NO_POSE;
    bool _vuforiaStartedLogged;

    const string CsvHeader =
        "unix_ms,unity_time_s,frame_idx,condition,target_name," +
        "status,status_info,success_tracked,success_extended," +
        "pos_x_m,pos_y_m,pos_z_m,quat_x,quat_y,quat_z,quat_w," +
        "cam_pos_x_m,cam_pos_y_m,cam_pos_z_m,cam_dist_m," +
        "gt_pos_x_m,gt_pos_y_m,gt_pos_z_m,gt_quat_x,gt_quat_y,gt_quat_z,gt_quat_w," +
        "trans_err_cm,rot_err_deg,jitter_cm,jitter_deg," +
        "frame_dt_ms,fps,state_update_ms,registration_ms,e2e_since_vuforia_ms," +
        "dropped_this_frame,dropped_frames_total," +
        "unity_alloc_mb,gfx_driver_mb,graphics_memory_mb," +
        "tracking_optimization,motion_hint,tracking_mode,world_center_mode";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindObjectOfType<VuforiaExperimentLogger>() != null)
            return;

        var go = new GameObject("VuforiaExperimentLogger");
        go.AddComponent<VuforiaExperimentLogger>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        _sessionStartUnscaled = Time.unscaledTime;
        _observer = GetComponent<ObserverBehaviour>();
        if (_observer == null)
            _observer = FindObjectOfType<ObserverBehaviour>();

        if (writeCsv)
            OpenCsv();
    }

    void OnEnable()
    {
        if (VuforiaApplication.Instance != null)
        {
            VuforiaApplication.Instance.OnVuforiaInitialized += OnVuforiaInitialized;
            VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;
        }

        BindObserver();
        TryBindStateUpdated();
    }

    void OnDisable()
    {
        if (VuforiaApplication.Instance != null)
        {
            VuforiaApplication.Instance.OnVuforiaInitialized -= OnVuforiaInitialized;
            VuforiaApplication.Instance.OnVuforiaStarted -= OnVuforiaStarted;
        }

        UnbindObserver();
        UnbindStateUpdated();
        WriteSummaryAndClose("OnDisable");
    }

    void OnApplicationQuit()
    {
        WriteSummaryAndClose("OnApplicationQuit");
    }

    void OnVuforiaInitialized(VuforiaInitError error)
    {
        Debug.Log($"[VUFORIA_EXP] event=vuforia_initialized error={error} condition={conditionLabel}");
        BindObserver();
    }

    void OnVuforiaStarted()
    {
        _vuforiaStartedUnscaled = Time.unscaledTime;
        _vuforiaStartedLogged = true;
        Debug.Log($"[VUFORIA_EXP] event=vuforia_started unity_time_s={F(_vuforiaStartedUnscaled)} condition={conditionLabel}");
        BindObserver();
        TryBindStateUpdated();
    }

    void BindObserver()
    {
        if (_observer == null)
            _observer = FindObjectOfType<ObserverBehaviour>();
        if (_observer == null)
            return;

        _observer.OnTargetStatusChanged -= OnTargetStatusChanged;
        _observer.OnTargetStatusChanged += OnTargetStatusChanged;
    }

    void UnbindObserver()
    {
        if (_observer != null)
            _observer.OnTargetStatusChanged -= OnTargetStatusChanged;
    }

    void TryBindStateUpdated()
    {
        if (VuforiaBehaviour.Instance == null || VuforiaBehaviour.Instance.World == null)
            return;
        VuforiaBehaviour.Instance.World.OnStateUpdated -= OnStateUpdated;
        VuforiaBehaviour.Instance.World.OnStateUpdated += OnStateUpdated;
    }

    void UnbindStateUpdated()
    {
        if (VuforiaBehaviour.Instance == null || VuforiaBehaviour.Instance.World == null)
            return;
        VuforiaBehaviour.Instance.World.OnStateUpdated -= OnStateUpdated;
    }

    void OnStateUpdated()
    {
        var now = Time.unscaledTime;
        if (_lastStateCallbackUnscaled >= 0f)
        {
            _lastStateUpdateMs = (now - _lastStateCallbackUnscaled) * 1000f;
            _sumStateUpdateMs += _lastStateUpdateMs;
        }
        _lastStateCallbackUnscaled = now;
    }

    void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
    {
        _statusChanges++;
        var now = Time.unscaledTime;
        if (targetStatus.Status == Status.TRACKED && _firstTrackedUnscaled < 0f)
            _firstTrackedUnscaled = now;

        var registrationMs = RegistrationMs();
        var posePos = behaviour.transform.position;
        var poseRot = behaviour.transform.rotation;
        var cam = Camera.main;
        if (cam != null)
        {
            posePos = cam.transform.InverseTransformPoint(behaviour.transform.position);
            poseRot = Quaternion.Inverse(cam.transform.rotation) * behaviour.transform.rotation;
        }
        Debug.Log(
            $"[VUFORIA_EXP] event=status_change target={behaviour.TargetName} " +
            $"status={targetStatus.Status} status_info={targetStatus.StatusInfo} " +
            $"registration_ms={F(registrationMs)} " +
            $"cam_pos_m={FormatVec(posePos)} cam_dist_m={F(posePos.magnitude)} " +
            $"quat_xyzw={FormatQuat(poseRot)} " +
            $"condition={conditionLabel}");
        _prevStatus = targetStatus.Status;
    }

    void LateUpdate()
    {
        _frameIdx++;
        var dt = Time.unscaledDeltaTime;
        var dtMs = dt * 1000f;
        var fps = dt > 1e-6f ? 1f / dt : 0f;
        _sumDtMs += dtMs;
        _sumFps += fps;

        var expectedDt = ExpectedFrameDt();
        var droppedThisFrame = expectedDt > 0f && dt > expectedDt * 1.5f ? 1 : 0;
        if (droppedThisFrame == 1)
            _droppedFrames++;

        if (VuforiaBehaviour.Instance != null && !_vuforiaStartedLogged)
            TryBindStateUpdated();

        if (_observer == null)
            BindObserver();

        var status = Status.NO_POSE;
        var statusInfo = StatusInfo.UNKNOWN;
        var pos = Vector3.zero;
        var rot = Quaternion.identity;
        var camPos = Vector3.zero;
        var targetName = "";
        var cam = Camera.main;

        if (_observer != null)
        {
            status = _observer.TargetStatus.Status;
            statusInfo = _observer.TargetStatus.StatusInfo;
            pos = _observer.transform.position;
            rot = _observer.transform.rotation;
            targetName = _observer.TargetName;
            if (cam != null)
                camPos = cam.transform.InverseTransformPoint(pos);
            else
                camPos = pos;
        }

        CountStatus(status);

        float jitterCm = float.NaN;
        float jitterDeg = float.NaN;
        if (_hasPrevPose && IsPoseValid(status))
        {
            jitterCm = Vector3.Distance(camPos, _prevPos) * 100f;
            jitterDeg = Quaternion.Angle(_prevRot, rot);
            _sumJitterCm += jitterCm;
            _sumJitterDeg += jitterDeg;
            _jitterSamples++;
        }

        float transErrCm = float.NaN;
        float rotErrDeg = float.NaN;
        Vector3 gtPos = Vector3.zero;
        Quaternion gtRot = Quaternion.identity;
        bool hasGt = groundTruth != null;
        if (hasGt)
        {
            gtPos = groundTruth.position;
            gtRot = groundTruth.rotation;
            var gtCam = cam != null ? cam.transform.InverseTransformPoint(gtPos) : gtPos;
            if (IsPoseValid(status))
            {
                transErrCm = Vector3.Distance(camPos, gtCam) * 100f;
                _sumTransErrCm += transErrCm;
                _errorSamples++;
                if (computeRotationError)
                {
                    rotErrDeg = Quaternion.Angle(rot, gtRot);
                    _sumRotErrDeg += rotErrDeg;
                }
            }
        }

        if (IsPoseValid(status))
        {
            _prevPos = camPos;
            _prevRot = rot;
            _hasPrevPose = true;
        }

        float unityAllocMb = float.NaN;
        float gfxMb = float.NaN;
        float graphicsMemoryMb = SystemInfo.graphicsMemorySize;
        if (logGpu)
        {
            unityAllocMb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            gfxMb = Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f);
            _sumUnityAllocMb += unityAllocMb;
            _sumGfxMb += gfxMb;
            _gpuSamples++;
        }

        var registrationMs = RegistrationMs();
        var e2eMs = _vuforiaStartedUnscaled >= 0f
            ? (Time.unscaledTime - _vuforiaStartedUnscaled) * 1000f
            : float.NaN;

        if (writeCsv && _csv != null)
        {
            _line.Length = 0;
            Append(_line, UnixMs());
            _line.Append(',');
            Append(_line, Time.unscaledTime);
            _line.Append(',');
            _line.Append(_frameIdx);
            _line.Append(',');
            AppendCsvString(_line, conditionLabel);
            _line.Append(',');
            AppendCsvString(_line, targetName);
            _line.Append(',');
            _line.Append(status);
            _line.Append(',');
            _line.Append(statusInfo);
            _line.Append(',');
            _line.Append(status == Status.TRACKED ? 1 : 0);
            _line.Append(',');
            _line.Append(status == Status.TRACKED || status == Status.EXTENDED_TRACKED ? 1 : 0);
            _line.Append(',');
            Append(_line, pos.x); _line.Append(',');
            Append(_line, pos.y); _line.Append(',');
            Append(_line, pos.z); _line.Append(',');
            Append(_line, rot.x); _line.Append(',');
            Append(_line, rot.y); _line.Append(',');
            Append(_line, rot.z); _line.Append(',');
            Append(_line, rot.w); _line.Append(',');
            Append(_line, camPos.x); _line.Append(',');
            Append(_line, camPos.y); _line.Append(',');
            Append(_line, camPos.z); _line.Append(',');
            Append(_line, camPos.magnitude); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtPos.x : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtPos.y : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtPos.z : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtRot.x : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtRot.y : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtRot.z : float.NaN); _line.Append(',');
            AppendMaybe(_line, hasGt ? gtRot.w : float.NaN); _line.Append(',');
            AppendMaybe(_line, transErrCm); _line.Append(',');
            AppendMaybe(_line, rotErrDeg); _line.Append(',');
            AppendMaybe(_line, jitterCm); _line.Append(',');
            AppendMaybe(_line, jitterDeg); _line.Append(',');
            Append(_line, dtMs); _line.Append(',');
            Append(_line, fps); _line.Append(',');
            Append(_line, _lastStateUpdateMs); _line.Append(',');
            AppendMaybe(_line, registrationMs); _line.Append(',');
            AppendMaybe(_line, e2eMs); _line.Append(',');
            _line.Append(droppedThisFrame); _line.Append(',');
            _line.Append(_droppedFrames); _line.Append(',');
            AppendMaybe(_line, unityAllocMb); _line.Append(',');
            AppendMaybe(_line, gfxMb); _line.Append(',');
            Append(_line, graphicsMemoryMb); _line.Append(',');
            _line.Append(GetTrackingOptimization()); _line.Append(',');
            _line.Append(GetMotionHint()); _line.Append(',');
            _line.Append(GetTrackingMode()); _line.Append(',');
            _line.Append(VuforiaBehaviour.Instance != null
                ? VuforiaBehaviour.Instance.WorldCenterMode.ToString()
                : "");
            _csv.WriteLine(_line.ToString());
            if (_frameIdx % 30 == 0)
                _csv.Flush();
        }

        if (consoleEveryNFrames > 0 && _frameIdx % consoleEveryNFrames == 0)
        {
            Debug.Log(
                $"[VUFORIA_EXP] event=frame frame={_frameIdx} fps={F(fps)} dt_ms={F(dtMs)} " +
                $"status={status} cam_pos_m={FormatVec(camPos)} cam_dist_m={F(camPos.magnitude)} " +
                $"trans_err_cm={F(transErrCm)} rot_err_deg={F(rotErrDeg)} " +
                $"jitter_cm={F(jitterCm)} jitter_deg={F(jitterDeg)} " +
                $"state_update_ms={F(_lastStateUpdateMs)} dropped_frames={_droppedFrames} " +
                $"gfx_mb={F(gfxMb)} unity_alloc_mb={F(unityAllocMb)} " +
                $"success_rate_tracked_pct={F(SuccessRateTrackedPct())}");
        }

        _lastLoggedUnscaled = Time.unscaledTime;
        _prevStatus = status;
    }

    void OnGUI()
    {
        if (!showOnScreenOverlay)
            return;

        var fps = Time.unscaledDeltaTime > 1e-6f ? 1f / Time.unscaledDeltaTime : 0f;
        var status = _observer != null ? _observer.TargetStatus.Status.ToString() : "NO_OBSERVER";
        var cam = Camera.main;
        var camPos = Vector3.zero;
        if (_observer != null && cam != null)
            camPos = cam.transform.InverseTransformPoint(_observer.transform.position);
        var gtReady = groundTruth != null;
        var text =
            $"VUFORIA_EXP  {conditionLabel}\n" +
            $"status={status}  fps={fps:F1}  dropped={_droppedFrames}  gt={(gtReady ? "ON 40cm" : "OFF")}\n" +
            $"cam_pos_m={F(camPos.x)}, {F(camPos.y)}, {F(camPos.z)}  cam_dist_m={F(camPos.magnitude)}\n" +
            $"registration_ms={F(RegistrationMs())}  success_tracked%={F(SuccessRateTrackedPct())}\n" +
            $"csv={_csvPath}";
        GUI.color = Color.black;
        GUI.Label(new Rect(13, 13, 1100, 130), text);
        GUI.color = Color.green;
        GUI.Label(new Rect(12, 12, 1100, 130), text);
    }

    void CountStatus(Status status)
    {
        switch (status)
        {
            case Status.TRACKED:
                _framesTracked++;
                break;
            case Status.EXTENDED_TRACKED:
                _framesExtended++;
                break;
            case Status.LIMITED:
                _framesLimited++;
                break;
            default:
                _framesNoPose++;
                break;
        }
    }

    static bool IsPoseValid(Status status)
    {
        return status == Status.TRACKED || status == Status.EXTENDED_TRACKED;
    }

    float RegistrationMs()
    {
        if (_firstTrackedUnscaled < 0f || _vuforiaStartedUnscaled < 0f)
            return float.NaN;
        return (_firstTrackedUnscaled - _vuforiaStartedUnscaled) * 1000f;
    }

    float SuccessRateTrackedPct()
    {
        return _frameIdx <= 0 ? 0f : 100f * _framesTracked / _frameIdx;
    }

    float ExpectedFrameDt()
    {
        if (Application.targetFrameRate > 0)
            return 1f / Application.targetFrameRate;
        if (QualitySettings.vSyncCount > 0)
            return QualitySettings.vSyncCount / 60f;
        return 1f / 60f;
    }

    string GetTrackingOptimization()
    {
        return "DEFAULT";
    }

    string GetMotionHint()
    {
        return datasetMotionHint;
    }

    string GetTrackingMode()
    {
        return datasetTrackingMode;
    }

    void OpenCsv()
    {
        try
        {
            var dir = ResolveLogDir();
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var safeCondition = Sanitize(conditionLabel);
            _csvPath = Path.Combine(dir, $"vuforia_frames_{safeCondition}_{stamp}.csv");
            _summaryPath = Path.Combine(dir, $"vuforia_summary_{safeCondition}_{stamp}.txt");
            _csv = new StreamWriter(_csvPath, false, new UTF8Encoding(false));
            _csv.WriteLine(CsvHeader);
            _csv.Flush();
            Debug.Log($"[VUFORIA_EXP] event=csv_open path={_csvPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VUFORIA_EXP] event=csv_open_failed error={ex.Message}");
            _csv = null;
        }
    }

    void WriteSummaryAndClose(string reason)
    {
        if (_closed)
            return;
        _closed = true;

        if (_csv != null)
        {
            try
            {
                _csv.Flush();
                _csv.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VUFORIA_EXP] event=csv_close_failed error={ex.Message}");
            }
            _csv = null;
        }

        if (_frameIdx <= 0)
            return;

        var meanFps = Mean(_sumFps, _frameIdx);
        var meanDt = Mean(_sumDtMs, _frameIdx);
        var meanTrackMs = Mean(_sumStateUpdateMs, _frameIdx);
        var meanJitterCm = Mean(_sumJitterCm, _jitterSamples);
        var meanJitterDeg = Mean(_sumJitterDeg, _jitterSamples);
        var meanTrans = Mean(_sumTransErrCm, _errorSamples);
        var meanRot = Mean(_sumRotErrDeg, _errorSamples);
        var meanGfx = Mean(_sumGfxMb, _gpuSamples);
        var meanUnity = Mean(_sumUnityAllocMb, _gpuSamples);
        var successTracked = SuccessRateTrackedPct();
        var successExtended = _frameIdx <= 0
            ? 0f
            : 100f * (_framesTracked + _framesExtended) / _frameIdx;
        var droppedPct = _frameIdx <= 0 ? 0f : 100f * _droppedFrames / _frameIdx;
        var durationS = Time.unscaledTime - _sessionStartUnscaled;

        var summary =
            "========== VUFORIA_EXP SUMMARY ==========\n" +
            $"reason={reason}\n" +
            $"condition={conditionLabel}\n" +
            $"csv={_csvPath}\n" +
            $"duration_s={F(durationS)}\n" +
            $"frames_total={_frameIdx}\n" +
            $"frames_tracked={_framesTracked}\n" +
            $"frames_extended={_framesExtended}\n" +
            $"frames_limited={_framesLimited}\n" +
            $"frames_no_pose={_framesNoPose}\n" +
            $"status_changes={_statusChanges}\n" +
            $"success_rate_tracked_pct={F(successTracked)}\n" +
            $"success_rate_tracked_or_extended_pct={F(successExtended)}\n" +
            $"registration_ms={F(RegistrationMs())}\n" +
            $"fps_mean={F(meanFps)}\n" +
            $"latency_frame_dt_ms_mean={F(meanDt)}\n" +
            $"tracking_time_ms_mean={F(meanTrackMs)}\n" +
            $"end_to_end_latency_ms=UNKNOWN_capture_to_photon__use_frame_dt_ms_as_proxy\n" +
            $"network_latency_ms=N/A\n" +
            $"pose_jitter_cm_mean={F(meanJitterCm)}\n" +
            $"pose_jitter_deg_mean={F(meanJitterDeg)}\n" +
            $"translation_error_cm_mean={F(meanTrans)}\n" +
            $"rotation_error_deg_mean={F(meanRot)}\n" +
            $"translation_error_note={(groundTruth != null ? "computed_vs_groundTruth_transform" : "MISSING_GT_assign_groundTruth")}\n" +
            $"dropped_frames={_droppedFrames}\n" +
            $"dropped_frames_pct={F(droppedPct)}\n" +
            $"unity_alloc_mb_mean={F(meanUnity)}\n" +
            $"gfx_driver_mb_mean={F(meanGfx)}\n" +
            $"graphics_memory_mb={SystemInfo.graphicsMemorySize}\n" +
            $"gpu={SystemInfo.graphicsDeviceName}\n" +
            $"device={SystemInfo.deviceModel}\n" +
            $"unity={Application.unityVersion}\n" +
            $"platform={Application.platform}\n" +
            "==========================================";

        Debug.Log(summary);

        if (!string.IsNullOrEmpty(_summaryPath))
        {
            try
            {
                File.WriteAllText(_summaryPath, summary + "\n", new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VUFORIA_EXP] event=summary_write_failed error={ex.Message}");
            }
        }
    }

    static string ResolveLogDir()
    {
#if UNITY_EDITOR
        var projectRoot = Directory.GetParent(Application.dataPath);
        return projectRoot != null
            ? Path.Combine(projectRoot.FullName, "Vuforia", "logs")
            : Path.Combine(Application.persistentDataPath, "vuforia_logs");
#else
        return Path.Combine(Application.persistentDataPath, "vuforia_logs");
#endif
    }

    static long UnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "unnamed";
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }

    static double Mean(double sum, int n)
    {
        return n <= 0 ? double.NaN : sum / n;
    }

    static void Append(StringBuilder sb, float value)
    {
        sb.Append(float.IsNaN(value) ? "" : value.ToString("G9", CultureInfo.InvariantCulture));
    }

    static void Append(StringBuilder sb, double value)
    {
        sb.Append(double.IsNaN(value) ? "" : value.ToString("G9", CultureInfo.InvariantCulture));
    }

    static void Append(StringBuilder sb, long value)
    {
        sb.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendMaybe(StringBuilder sb, float value)
    {
        Append(sb, value);
    }

    static void AppendCsvString(StringBuilder sb, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        if (value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0)
        {
            sb.Append('"');
            sb.Append(value.Replace("\"", "\"\""));
            sb.Append('"');
        }
        else
        {
            sb.Append(value);
        }
    }

    static string F(float value)
    {
        return float.IsNaN(value) ? "NA" : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    static string F(double value)
    {
        return double.IsNaN(value) ? "NA" : value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    static string FormatVec(Vector3 v)
    {
        return $"{F(v.x)},{F(v.y)},{F(v.z)}";
    }

    static string FormatQuat(Quaternion q)
    {
        return $"{F(q.x)},{F(q.y)},{F(q.z)},{F(q.w)}";
    }
}
