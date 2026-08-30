# CODEBASE_RESEARCH_AUDIT

**Audit date:** 2026-08-21  
**Scope:** `/Users/tongbingwen/Tracking using Vuforia`  
**Evidence policy:** 结论均给路径+行号。无法确认标 **UNKNOWN**，不适用标 **N/A**。README/注释/配置与代码冲突时，优先信任 **序列化运行配置与 SDK 枚举文档**，其次 **Unity 截图中的 Console**，最后才是 `Vuforia/vuforia method.md` 的定性叙述。

**Post-audit note (2026-08-21):** 审计完成后新增了 `Assets/Scripts/VuforiaExperimentLogger.cs` 与 Plan B 卷尺 GT。§1–§16 保留审计当时的只读结论。原始 prompt 的 **§17 Paper-ready Metrics Feasibility Table** 已按 2026-08-21 Editor 实验填完；逐次 CSV/SUMMARY 记录在 **§21**。

---

## 1. Repository Identity

| Field | Finding | Status |
|---|---|---|
| repo name | `Tracking using Vuforia`（Unity 产品名与 GitHub repo 名一致） | implemented |
| Git remote | `https://github.com/Cx330-cpu/Tracking-using-Vuforia.git` | implemented |
| Git history | `main` 上 **No commits yet**；工作区几乎全是 untracked | unknown / incomplete |
| owner / author | GitHub owner: `Cx330-cpu`。Unity `companyName: DefaultCompany` | partially implemented |
| repo purpose | 用 Vuforia **Model Target** 对真实物体做 6DoF 跟踪，并在 Unity 中挂虚拟物体做 AR overlay / object replacement | partially implemented |
| route / method name | **Vuforia Model Target 6DoF tracking**（商业 CAD/几何模型目标跟踪） | implemented |
| route objective | 识别并跟踪 **iPhone 16 Pro Max**，输出 Unity 下的 6DoF pose，用于 AR object replacement | partially implemented |
| implementation status | **partially implemented**：Editor Play Mode 跟踪 demo 已接通；论文级 GT、误差、latency/FPS instrumentation、可复现实验脚本在审计当时均缺失 | partially implemented |

**与 AR / VR / pose / tracking / replacement / perception 的关系**

- **AR tracking / pose estimation / object replacement:** 直接相关。场景里有 `ARCamera` + `ModelTarget` + 子物体 `手持电话.glb`。
- **VR / HMD:** 无 XR/OpenXR/AR Foundation 运行时配置。`XRSettings.asset` 为 `"VR Device Disabled"`。
- **Perception pipeline:** 不是自研分割/深度/神经网络 pipeline，而是闭源 Vuforia Engine 视觉跟踪。

**证据**

- 产品名 / 公司名：`ProjectSettings/ProjectSettings.asset` L15–16
- Unity 版本：`ProjectSettings/ProjectVersion.txt` L1–2（`2022.3.62f3`）
- GitHub remote：`Cx330-cpu/Tracking-using-Vuforia.git`
- 场景入口：`Assets/Scenes/SampleScene.unity` L258 (`ARCamera`), L554 (`ModelTarget`)
- 目标名 / dataset：同文件 L635–637；`Assets/StreamingAssets/Vuforia/iPhone16ProMax.xml` L4
- 文档宣称：`Vuforia/vuforia method.md` L1–25, L276–291
- XR 关闭：`ProjectSettings/XRSettings.asset` L2–8

---

## 2. High-level Pipeline

```
Physical object (iPhone 16 Pro Max)
  -> RGB camera (Unity Editor webcam / intended Android camera)
  -> Vuforia Engine init + video background
  -> Model Target dataset load (iPhone16ProMax.xml/.dat)
  -> Closed-source 6DoF pose estimation
  -> Unity ModelTarget Transform (WorldCenterMode = DEVICE)
  -> Child GLB overlay ("手持电话") enabled/disabled by tracking status
  -> URP Game view rendering
  -> Console Debug.Log of status changes only
```

| Stage | Key files / functions | Status | Input | Output |
|---|---|---|---|---|
| Input (RGB) | `VuforiaBehaviour` on `ARCamera`; Play Mode = WEBCAM | implemented | live RGB | video background |
| Preprocessing | Vuforia 内部；无项目脚本 | unknown (SDK-internal) | RGB | UNKNOWN |
| Segmentation | 无 | N/A | N/A | N/A |
| Depth / LiDAR | 无 | N/A | N/A | N/A |
| Model-target recognition | `ModelTargetBehaviour`, dataset XML/DAT | implemented | RGB + CAD dataset | `Status` / `StatusInfo` |
| 6DoF pose | Vuforia native engine | implemented (opaque) | recognized target | Unity `Transform` |
| Coordinate transform | `WorldCenterMode.DEVICE`; `virtualSceneScaleFactor=1` | implemented | Vuforia pose | Unity left-handed pose |
| Object replacement | Prefab instance `手持电话` parented to `ModelTarget` | partially implemented | ModelTarget pose | child Unity transform |
| Network transmission | 无 socket/HTTP/UDP | N/A | N/A | N/A |
| Unity / AR rendering | URP + Vuforia video background | implemented | pose + mesh | Game view overlay |
| Logging / evaluation | `DefaultObserverEventHandler` 只打 status | partially implemented | status enum | Unity Console 文本 |

**证据**

- Scene roots：`Assets/Scenes/SampleScene.unity` L690–697
- `ARCamera` + `VuforiaBehaviour`：L244–407；`mWorldCenterMode: 2` 在 L332
- `ModelTarget`：L541–683；`mDataSetPath: Vuforia/iPhone16ProMax.xml` L637
- Dataset：`Assets/StreamingAssets/Vuforia/iPhone16ProMax.xml` L1–9；同目录存在 `iPhone16ProMax.dat`
- Replacement child：`SampleScene.unity` L457–540（guid 对应 `Assets/Resources/模型/手持电话.glb`）
- Status log：Vuforia `DefaultObserverEventHandler.cs` L86–94
- Device tracker：`Assets/Resources/VuforiaConfiguration.asset` L23, L45–47
- Play Mode webcam：`VuforiaConfiguration.asset` L50–52 `playModeType: 0`

---

## 3. Inputs and Outputs

### Inputs

| Item | Status | Format / unit | Example location |
|---|---|---|---|
| RGB image / video | implemented | live camera；Editor 默认 webcam profile 640×480，实际分辨率 UNKNOWN | Play Mode webcam |
| depth / LiDAR / RGB-D | N/A | — | — |
| mask / segmentation | N/A | — | — |
| camera intrinsics | unknown | Vuforia 内部标定 | 无项目内标定文件 |
| camera extrinsics | unknown | DEVICE world center 下相机近似世界原点 | `ARCamera` identity |
| CAD / mesh | implemented | Vuforia Model Target meshes + `.dat` | `Assets/Resources/VuforiaModels/iPhone16ProMax/` |
| object size / scale | implemented | meters（bbox） | bbox ≈ 0.017 × 0.168 × 0.081 m |
| calibration files | missing | — | — |
| network stream | N/A | — | — |
| Unity scene data | implemented | `.unity` | `Assets/Scenes/SampleScene.unity` |
| config files | implemented | Vuforia YAML/XML | `VuforiaConfiguration.asset`；`iPhone16ProMax.xml` |
| dataset files | implemented | `.xml` + `.dat` | `Assets/StreamingAssets/Vuforia/` |
| original CAD for MTG | unknown | UNKNOWN | 仓库中无 `.step/.iges/.fbx` 源模型 |
| replacement GLBs | implemented as assets；scene 只用 1 个 | glTF/GLB | `Assets/Resources/模型/` |

### Outputs

| Item | Status | Format / unit |
|---|---|---|
| 3D position | implemented (runtime only, 审计时未落盘) | Unity `Vector3`；单位 m（scale factor=1） |
| 6DoF pose | implemented (runtime) | Unity position + quaternion |
| rotation matrix | unknown | 未导出 |
| quaternion | implemented (Unity) | Unity `xyzw` |
| Euler angles | partially implemented | 仅 replacement 子物体 inspector Euler `(90, 90, 0)` |
| 4×4 matrix | unknown | 未导出 |
| object-to-camera / camera-to-object | DEVICE mode：camera 为 world center，trackable 相对相机放置 | Unity world |
| Unity transform | implemented | ModelTarget + child |
| AR overlay result | partially implemented | `Vuforia/image/*.png` 定性截图 |
| logs | implemented after audit | Console + `Vuforia/logs/*.txt` |
| CSV / JSON results | implemented after audit | 逐帧 CSV：`Vuforia/logs/vuforia_frames_*.csv` |
| visualization videos | missing | 仅 PNG 截图 |

---

## 4. Key Files and Code Evidence

项目 **没有自研跟踪算法**。审计时也没有自定义 runtime 评测脚本。之后补了 `VuforiaExperimentLogger`（只记日志，不改 Vuforia 估计）。

| File | Lines | Role | Evidence Summary |
|---|---:|---|---|
| `Assets/Scenes/SampleScene.unity` | 697+ | 唯一运行场景：ARCamera + ModelTarget + 替换物 | L258, L554, L457–540 |
| `Assets/Resources/VuforiaConfiguration.asset` | 58 | Vuforia license、版本 11.4.4、device tracker、webcam Play Mode | L16–27, L45–52 |
| `Assets/StreamingAssets/Vuforia/iPhone16ProMax.xml` | 9 | Model Target 运行配置 | L4: `trackingMode="car"` `motionHint="adaptive"` `optimizeTrackingFor="default"` |
| `Assets/StreamingAssets/Vuforia/iPhone16ProMax.dat` | binary ~8.3MB | 识别/跟踪数据库 | StreamingAssets |
| `Assets/Editor/Vuforia/iPhone16ProMax/authoringinfo.xml` | 10 | MTG 生成元数据 | L3–4: `toolVersion="v7.4.1"` |
| `Assets/Resources/模型/手持电话.glb` | binary ~81MB | 场景中实际挂接的替换物体 | guid 匹配 scene |
| `Assets/Editor/Migration/AddVuforiaEnginePackage.cs` | 478 | 把 Vuforia 11.4.4 tarball 写入 Packages | L15 |
| `Packages/manifest.json` | 47 | `com.ptc.vuforia.engine` file tarball | L3 |
| `Vuforia/vuforia method.md` | 327 | 定性 Methods/Results 草稿 | L1–327 |
| `Vuforia/image/{1-7}.png` | images | 定性实验截图 | 1–6 为 Play Mode；7 为 TrackingOptimization 下拉 |
| `DefaultObserverEventHandler.cs` (SDK) | 321 | status 日志 + 显示/隐藏 overlay | L24–37, L86–94, L142–165 |
| `Assets/Scripts/VuforiaExperimentLogger.cs` | post-audit | 逐帧 CSV + SUMMARY（非跟踪算法） | `CsvHeader` L78+；输出 `Vuforia/logs/` |
| evaluation / benchmark scripts | — | 审计时不存在；现仅有 logger，无独立 eval 脚本 | 指标由 §21 离线从 CSV 汇总 |

---

## 5. Dependencies and Versions

| Item | Value | Evidence |
|---|---|---|
| Python / CUDA / PyTorch / OpenCV | N/A | 无 |
| Unity | **2022.3.62f3** | `ProjectSettings/ProjectVersion.txt` L1–2 |
| URP | 14.0.12 | `Packages/manifest.json` L8 |
| Vuforia Engine | **11.4.4** | `manifest.json` L3；`VuforiaConfiguration.asset` L26 |
| Vuforia MTG | **v7.4.1** | `authoringinfo.xml` L3–4 |
| FoundationPose / YOLO / SAM | N/A | 未使用 |
| glTFast | 6.14.1 | `manifest.json` L4 |
| Dockerfile / README installation | missing | 根目录无 README |
| Vuforia license | **Premium Trial**；license key **已提交进仓库** | `VuforiaConfiguration.asset` L16–17；截图水印 |

Vuforia 专有软件，论文必须引用，且不能把算法写成自研。`vuforia method.md` L276–281 这一点是对的。

---

## 6. Hardware and Runtime Assumptions

| Assumption | Status | Evidence |
|---|---|---|
| GPU | Unity 渲染需要 GPU；无 CUDA 推理 | Editor 截图 Graphics API = Metal |
| CUDA / VRAM 下限 | N/A / missing | 无 |
| LiDAR / RGB-D / depth camera | N/A | 无 |
| iPhone 16 Pro Max as **target object** | implemented | dataset 名 `iPhone16ProMax` |
| iPhone as **tracking device** | unknown | 截图是 macOS Unity Editor |
| webcam | implemented (Editor) | `playModeType: 0` = WEBCAM |
| Unity runtime | implemented | Editor Play Mode 已演示 |
| Android device runtime | planned / unknown | Min SDK 29；无 APK、无 on-device log |
| iOS runtime | unknown | `cameraUsageDescription` 为空 |
| local network / client-server | N/A | 无 |
| HMD / VR | N/A | XR disabled |

**重要运行事实：** 定性实验是在 **macOS Unity Editor + 摄像头** 下做的，窗口标题含 `Android` 与 `<Metal>`。不能把这些结果直接写成 Android 设备端 AR 或 VR HMD 结果。

---

## 7. Algorithms, Models, and Third-party Sources

| Algorithm / model | In code? | In README/md? |
|---|---|---|
| Vuforia Model Target | **actually used** | used |
| Vuforia Device Tracking / Extended Tracking | **actually used** | md 写成 future，但配置已开 |
| TrackingOptimization.DEFAULT | **actually used** | md 称 Default Mode |
| Model Target `trackingMode=car` | **actually used in dataset** | **md 未提** |
| LOW_FEATURE_OBJECTS / AR_CONTROLLER | mentioned only | md §4.2–4.3 “considered” |
| Image Target / AprilTag / ArUco / OpenCV PnP / YOLO / SAM / FoundationPose | not used | FoundationPose 仅 future |

**关键矛盾（更信任 XML/scene/SDK）：**

- `vuforia method.md` L189–191：Default mode was selected
- `iPhone16ProMax.xml` L4：`trackingMode="car"` **and** `optimizeTrackingFor="default"`
- `SampleScene.unity` L658–660：`mMotionHint: 1`, `mTrackingMode: 1`, `mTrackingOptimization: 0`
- SDK：`CAR + ADAPTIVE → TrackingOptimization.DEFAULT`

运行时 Tracking Optimization = DEFAULT；MTG dataset Tracking Mode = CAR。

---

## 8. Data Formats and Dataset Structure

| Type | Path | Units / notes |
|---|---|---|
| Model Target XML | `Assets/StreamingAssets/Vuforia/iPhone16ProMax.xml` | trackingMode/motionHint/upVector |
| Model Target DAT | `.../iPhone16ProMax.dat` | 二进制识别数据 |
| Authoring XML | `Assets/Editor/Vuforia/.../authoringinfo.xml` | bbox **meters** |
| CAD meshes | `Assets/Resources/VuforiaModels/iPhone16ProMax/` | Unity units ≈ m |
| Replacement GLBs | `Assets/Resources/模型/*.glb` | scene 只用 `手持电话.glb`，再乘 0.47 |
| Pose files | **missing** | — |
| Qualitative screenshots | `Vuforia/image/1.png`–`7.png` | Editor Play Mode |
| Timestamp convention | Unity Console `[HH:MM:SS]` | 秒级，无 epoch、无帧号 |

bbox：X≈0.01695 m，Y≈0.16838 m，Z≈0.08084 m，与 iPhone 16 Pro Max 同量级但不完全相等。

---

## 9. Ground Truth / Reference Pose Mechanism

| Mechanism | Status |
|---|---|
| GT pose files / reference pose / marker GT / synthetic GT | missing |
| SLAM pose export | missing（有 EXTENDED_TRACKED 状态，无 pose 文件） |
| Camera calibration file | missing |
| Object coordinate frame | implemented（Vuforia model frame + upVector `0 1 0`） |
| DEVICE world center | implemented：camera ≈ world origin |
| Timestamp alignment | missing |

**Audit-time：** Translation Error / Rotation Error 均不能计算（missing GT）。当时上限：qualitative overlay + status 观察。

**Post-audit (2026-08-21)：** Plan B 卷尺 GT 已接入。`GT_Static` 为 `ARCamera` 子物体，40 cm 条件 local `(0,0,0.4)`。Translation Error = 相机系中估计原点与 GT 点的欧氏距离（cm），仅统计 `TRACKED` 帧。Rotation Error 仍为 **N/A**（方案 B 不能独立约束 CAD 朝向）。证据：`Assets/Scripts/VuforiaExperimentLogger.cs`；场景 `GT_Static`；§21。

---

## 10. Evaluation Scripts, Metrics, and Logs

审计当时：无 evaluation scripts、无 notebooks、无 result CSV/JSON。`Logs/` 仅为 Unity 导入日志。

| Metric | Status at audit | Status after 2026-08-21 runs |
|---|---|---|
| Translation Error | missing | **already computed**（Plan B；主条件 40 cm 背面 n=2） |
| Rotation Error | missing | **N/A**（方案 B 无朝向 GT；SUMMARY 里的 0 忽略） |
| Latency / FPS | missing | **computable, Editor proxy only**（不可当 AR FPS / capture-to-photon） |
| Success Rate / Pose Jitter | missing | **already computed**（锁定后帧成功率；TRACKED 帧 jitter） |
| Robustness | qualitative only | **already computed**（20/40/80 cm、正面、半遮挡；n=1） |
| Registration / tracking time | missing | **already computed**（registration_ms；state_update_ms 代理） |
| Network latency | N/A | **N/A** |
| End-to-end latency | missing | **UNKNOWN** |
| Dropped frames / GPU / VRAM | missing | **already computed**（Editor Profiler 字段） |

不要把 `Vuforia/vuforia method.md` 中的 “accurate / stable / fast” 当结果。填表用 §17 / §21。

---

## 11. Timing, FPS, and Latency Instrumentation

审计当时：项目无自定义 `Stopwatch` / `Time.realtimeSinceStartup` 埋点。仅 SDK `Debug.Log` 状态变化带 Console 时分秒。`UsePoseSmoothing: 0`。`enableInternalProfiler: 0`。

截图中 start→TRACKED 间隔是人手对准+识别的混合物，**不能**当 model inference latency。

---

## 12. Pose Representation, Coordinate Systems, and Units

| Topic | Finding | Paper risk |
|---|---|---|
| translation unit | meters when `virtualSceneScaleFactor=1` | 误写成 cm 会差 100× |
| rotation | Unity quaternion xyzw | 与 wxyz 互换会错 |
| Euler | Unity inspector ZXY | 不宜直接报 Euler 误差 |
| pose direction | DEVICE：trackable 相对相机放置 | 与 OpenCV `T_CO` 比较前必须转换 |
| handedness | Unity left-handed, Y-up | 跨 route 必须加转换 |
| replacement offset | scale 0.47, y=-0.1, Euler (90,90,0) | overlay 对齐 ≠ CAD 坐标系已标定 |

---

## 13. Network / Unity / AR Pipeline

Unity / AR：适用。Network：**N/A**。

- Replacement：`手持电话` 作为 ModelTarget 子物体
- `StatusFilter: 0` = Tracked only → `EXTENDED_TRACKED` 时会隐藏 overlay
- `RuntimeOcclusion: 0`
- `OnTargetFound` / `OnTargetLost` 事件列表为空

---

## 14. Error Handling, Failure Modes, and Known Limitations

- Init errors：`DefaultInitializationErrorHandler` 挂在 ARCamera
- Tracking failure：`NO_POSE` / `LIMITED`
- Hard-coded replacement pose
- Trial license 水印
- Dataset `trackingMode=car` 用于手机
- md Figure 2（`image/4.png`）写成 failure，但 Console 显示 TRACKED/EXTENDED_TRACKED
- md §3.4 正面屏幕图像实验：仓库只有一个 dataset，无法从本 repo 复现
- 三种 tracking mode 比较未做成可复现配置矩阵

---

## 15. Existing Experiment Configurations and Reproducible Commands

仓库没有 CLI demo/eval 命令。用 Unity **2022.3.62f3** 打开项目，Play Mode + webcam，对准真机 iPhone 16 Pro Max。

| Purpose | Command / action | Evidence |
|---|---|---|
| Open | Unity 2022.3.62f3 打开本项目 | `ProjectVersion.txt` |
| Demo | Editor Play | `playModeType: 0` |
| Evaluation | Editor Play + `VuforiaExperimentLogger` | `Vuforia/logs/*.csv` + `*.txt` |
| Android APK | 未出现在仓库 | — |

---

## 16. Existing Result Files

| Path | Usable for paper? |
|---|---|
| `Vuforia/vuforia method.md` | Methods 定性草稿；**不要**当定量 Results |
| `Vuforia/image/1.png`–`6.png` | 定性 figure 候选 |
| `Vuforia/image/4.png` | caption 与部分 Console 冲突，使用前核对 |
| `Vuforia/image/7.png` | Methods 配置证据 |
| `Vuforia/logs/vuforia_frames_*.csv` | **可用**：逐帧 pose/status/误差 |
| `Vuforia/logs/vuforia_summary_*.txt` | **可用**：单次 SUMMARY（须按 §21 解释，勿直接抄 `fps_mean` / `rotation_error_deg_mean=0`） |

这些 PNG 是过程截图。CSV 才是 2026-08-21 定量记录。完整清单见 §21。

---

## 17. Paper-ready Metrics Feasibility Table

原始 prompt 要求的列如下。**Status / Existing Evidence 为 2026-08-21 补全后状态**（替代基线，Unity Editor + Mac 摄像头，Plan B 卷尺 GT）。主列条件 = **40 cm 完整背面**。逐次记录见 §21。

| Metric | Status | Existing Evidence | How to Compute | Unit | Missing Pieces |
|---|---|---|---|---|---|
| Translation Error | **already computed** | `..._124755.csv` TRACKED mean **11.66 cm**（std 0.14）；`..._125352.csv` TRACKED mean **20.71 cm**（std 0.08）。Vuforia `cam_dist_m` 约 51.2 / 60.3 cm vs 卷尺 40 cm | 相机系 `‖est − GT‖`，GT=`(0,0,0.4)`，仅 `TRACKED` 帧 | cm | 方案 B 卷尺；n=2 且两次差 ~9 cm，**不要合成 mean±std 假装 n=5**；非真机 |
| Rotation Error | **N/A** | logger `computeRotationError=false`；SUMMARY 的 `rotation_error_deg_mean=0` 是未计算，不是 0° | 需要独立 marker GT（Plan A） | degree | Plan B 不能约束 CAD 朝向 |
| Latency | **computable（proxy only）** | trial 1 TRACKED `frame_dt_ms` mean **2.95 ms**；SUMMARY `latency_frame_dt_ms_mean` ≈ 2.45 ms | Unity 帧间隔 | ms | **不是**曝光→显示；capture-to-photon 仍 UNKNOWN |
| FPS | **computable, not usable as AR FPS** | trial 1 TRACKED mean **505**、median **390**；SUMMARY `fps_mean` 753 | `1 / dt` | FPS | Editor 未锁摄像头；**禁止当论文 AR FPS** |
| Success Rate | **already computed** | 锁定后 `TRACKED`：**85.4%** / **83.9%**（trial 1/2）；试验成功 **2/2**。全程含启动 NO_POSE 的 73.5% / 68.0% **不要用** | 试验成功 = 至少一帧 TRACKED；帧成功率 = 第一次 TRACKED 之后 TRACKED 帧 / 之后总帧 | % | 定义必须写进 Methods |
| Pose Jitter | **already computed** | trial 1 TRACKED：mean **0.006 cm / 0.07°**（median 0） | 相邻 TRACKED 帧相机系位移 / 角增量 | cm / degree | 跟丢时会跳；只报 TRACKED |
| Robustness | **already computed** | 20 cm 背面成功（误差约 **4.5 cm**，相对 0.2 m 离线重算）；80 cm **失败**；40 cm 正面 **失败**；40 cm 半遮挡背面 **失败** | 每条件 trial success + 若锁定则 translation error | condition-based | 每条件 n=1；无系统 viewpoint/motion 扫描 |
| Registration Time | **already computed** | **1710 ms** / **1991 ms**（trial 1/2）；20 cm 背面 1696 ms | `Vuforia Started` → 第一次 `TRACKED` | ms | 含对准时间 |
| Tracking Time | **computable（proxy）** | SUMMARY `tracking_time_ms_mean` ≈ **2.3 ms** | `World.OnStateUpdated` 间隔 | ms | 不是 Vuforia 内部推理耗时 |
| Network Latency | **N/A** | 无 socket/HTTP/UDP | — | ms | 本 route 无网络 |
| End-to-end Latency | **UNKNOWN** | SUMMARY：`UNKNOWN_capture_to_photon__use_frame_dt_ms_as_proxy` | 需高速相机或硬件时钟 | ms | capture-to-photon 未测 |
| Dropped Frames | **already computed** | trial 1：3 帧，**0.016%**（相对 60 FPS 阈值） | `dt > 1/60` 计数 | count / % | Editor 阈值，解释力弱 |
| GPU / VRAM | **already computed（Editor）** | unity alloc **≈ 450 MB**；gfx driver **≈ 630–640 MB**；`graphics_memory_mb=38338`；gpu=`Apple M5 Pro` | Unity Profiler 分配字段 | MB / % | 显存容量不是占用%；非移动端 |

---

## 18. Missing Paper-level Experiments

### P0

1. Accuracy experiment with GT — **部分完成（Plan B 卷尺，40 cm n=2）**
2. Per-frame pose + status logging — **完成**（`VuforiaExperimentLogger` + `Vuforia/logs/`）
3. 写清并固定 runtime：Editor+webcam vs Android 真机 — **已固定为 OSXEditor**；若论文声称 mobile AR 仍缺真机
4. 纠正 `trackingMode=car` 描述与 failure caption — Methods 必须写 XML 的 `car` + DEFAULT；图 caption 仍需人工核对

### P1

5. Latency / FPS on claimed device — **未做**（仅有 Editor 代理）
6. Robustness protocol：距离、遮挡、正面 — **部分完成（n=1）**；视角 / 运动未做
7. Occlusion 定量化 — **半遮挡一次失败**；无像素级遮挡率
8. Object replacement 对齐实验 — **未做**（子物体仍手工 offset）
9. Failure case 分列 — CSV 已有 `NO_POSE` / `TRACKED` / `EXTENDED_TRACKED`

### P2

10. Viewpoint / distance / motion 系统扫描
11. DEFAULT vs LOW_FEATURE vs AR_CONTROLLER ablation
12. Car vs Default trackingMode 重导出
13. User-facing AR quality
14. Cross-route comparison

---

## 19. Minimal Additional Instrumentation

建议最小 logger（审计后已实现，见 `Assets/Scripts/VuforiaExperimentLogger.cs`）：

- 订阅 `OnTargetStatusChanged` 与 `World.OnStateUpdated`
- 每帧写 CSV：pose、status、dt、FPS、jitter、GT error（若有）、GPU
- 停止 Play 时打印 SUMMARY
- GT：把 marker/夹具物体拖到 `groundTruth`

CSV schema 见该脚本中的 `CsvHeader`。

---

## 20. Summary for IEEE VR 2027 Paper

**Methods 可写：** Vuforia Engine 11.4.4 Model Target；MTG v7.4.1；iPhone 16 Pro Max；DEVICE world center；1 unit = 1 m；`trackingMode=car` + Optimization DEFAULT；算法非自研。

**Results 现在能支撑（替代基线，须标注 Editor + 卷尺 GT + 小 n）：**

- Translation Error（40 cm 背面）：**11.7 cm / 20.7 cm**
- Success Rate（锁定后）：**85.4% / 83.9%**；试验 2/2 成功
- Registration：**1710 / 1991 ms**
- Pose jitter（TRACKED）：**0.006 cm / 0.07°**
- Robustness：20 cm 背面成功（≈4.5 cm）；80 cm / 正面 / 半遮挡 **失败**
- Rotation Error：**N/A**；Network：**N/A**；E2E latency：**UNKNOWN**；FPS：**N/A（Editor）**

**Strengths：** 商业 SDK 集成短；无需训练；近距离背面可锁 pose；已有可引用 CSV。

**Limitations：** 闭源；需 CAD；Editor+webcam；卷尺 GT；40 cm 两次尺度不一致；replacement 手工 offset；trial license。

**跨 route 最重要指标：** 6DoF accuracy、on-device latency/FPS、occlusion success、setup cost（需 CAD）。

**最大风险：** 把 Editor `fps_mean` 或 SUMMARY `rotation_error_deg_mean=0` 写成论文结果。

**下一步 3 件事（若还要加强本 route）：** 1) 真机重跑 2) Plan A 旋转 GT 3) capture-to-photon。替代对比列可以停在 §17。

---

## Appendix A. Instrumentation added after audit

| File | Role |
|---|---|
| `Assets/Scripts/VuforiaExperimentLogger.cs` | 每帧 CSV + Console SUMMARY |
| `Assets/Scenes/SampleScene.unity` | `ModelTarget` 上挂接该组件 |

输出目录（Editor）：`Vuforia/logs/`  
真机：`Application.persistentDataPath/vuforia_logs/`

场景 GT：`GT_Static`（`ARCamera` 子物体），40 cm 条件 local position `(0, 0, 0.4)`。

---

## 21. Instrumented Experiment Records (2026-08-21)

**Protocol：** iPhone 16 Pro Max，无壳，竖放，充电口朝下；Mac 自带摄像头；Unity 2022.3.62f3 Play Mode；`platform=OSXEditor`；`gpu=Apple M5 Pro`。Logger `conditionLabel` 在文件名里仍是 `rear_visible_static`，下表 **Condition 以操作者当场说明为准**。

**填表规则：** Translation / jitter 只用 `TRACKED` 帧。帧成功率 = 第一次 `TRACKED` 之后的 TRACKED 比例。20 cm 的 CSV `trans_err_cm` 仍相对 40 cm GT，**必须离线按 `(0,0,0.2)` 重算**。SUMMARY 的 `rotation_error_deg_mean=0` 当作 N/A。`fps_mean` 不当 AR FPS。

### 21.1 用于论文主列 / robustness 的试验

| Time | Frames CSV | Summary TXT | Operator condition | Trial success | Translation Error | Registration | Notes |
|---|---|---|---|---|---|---|---|
| 12:47:55 | `Vuforia/logs/vuforia_frames_rear_visible_static_20260821_124755.csv` | `..._124755.txt` | **40 cm 完整背面** | Yes | **11.66 cm**（std 0.14）；`cam_dist` 51.2 cm | 1710 ms | 主条件 trial 1。锁定后 TRACKED **85.4%**。jitter 0.006 cm / 0.07°。duration 46.6 s |
| 12:53:52 | `..._125352.csv` | `..._125352.txt` | **40 cm 完整背面** | Yes | **20.71 cm**（std 0.08）；`cam_dist` 60.3 cm | 1991 ms | 主条件 trial 2。锁定后 TRACKED **83.9%**。duration 38.3 s |
| 12:56:42 | `..._125642.csv` | `..._125642.txt` | **80 cm 背面**（操作者确认失败；文件 condition 未改名） | **No** | N/A | N/A | 10.1 s，`frames_tracked=0`，仅 INITIALIZING |
| 12:57:02 | `..._125702.csv` | `..._125702.txt` | **40 cm 正面屏幕** | **No** | N/A | N/A | 8.5 s，`TRACKED=0` |
| 12:57:28 | `..._125728.csv` | `..._125728.txt` | **40 cm 背面遮约一半** | **No** | N/A | N/A | 11.3 s，`TRACKED=0` |
| 12:58:15 | `..._125815.csv` | `..._125815.txt` | **20 cm 完整背面** | Yes | **≈ 4.54 cm**（离线 vs 0.2 m；**不要用** CSV/SUMMARY 的 16.61 cm） | 1696 ms | `cam_dist` ≈ 23.8 cm。锁定后 TRACKED **91.1%**。duration 27.2 s |

### 21.2 不用于填表的调试 / 无 GT 运行

| Time | Files | Why excluded |
|---|---|---|
| 12:39:14 | `..._123914.csv` / `.txt` | GT 未接（`MISSING_GT`）；registration 22.9 s；TRACKED 极少 |
| 12:41:01 | `..._124101.csv` / `.txt` | 已 TRACKED 但 **无 GT**，`trans_err=NA` |
| 12:53:18 | `..._125318.csv` / `.txt` | 操作者未标注条件；大量 `EXTENDED_TRACKED`；SUMMARY trans_err 22.2 cm。不纳入 §17 |

### 21.3 证据链

- Logger：`Assets/Scripts/VuforiaExperimentLogger.cs`
- 场景挂接：`Assets/Scenes/SampleScene.unity`（`ModelTarget` + `GT_Static`）
- Dataset：`Assets/StreamingAssets/Vuforia/iPhone16ProMax.xml`（`trackingMode="car"`）
- 硬件 / 平台：各 SUMMARY 的 `platform=OSXEditor`、`device=Mac17,9`、`unity=2022.3.62f3`

