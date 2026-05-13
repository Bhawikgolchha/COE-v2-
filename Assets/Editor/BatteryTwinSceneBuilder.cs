#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using BatteryTwin.Bindings;
using BatteryTwin.Data;
using BatteryTwin.Twin;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BatteryTwin.EditorTools
{
    /// <summary>
    /// One-click scene builder for the battery digital twin. Run via menu:
    ///   BatteryTwin → Build Scene
    ///
    /// Creates: a fresh scene at Assets/Scenes/BatteryTwin.unity with table,
    /// monitor + RenderTexture dashboard, pouch cell (auto-imported from
    /// Downloads if present), decorative 18650 cells, BMS, floating UI panels,
    /// camera framing, post-process bloom, and a fully-wired TwinController.
    ///
    /// Idempotent-ish: re-running overwrites the scene file. Materials and
    /// RenderTexture assets are reused if they already exist.
    /// </summary>
    public static class BatteryTwinSceneBuilder
    {
        const string SceneAssetPath = "Assets/Scenes/BatteryTwin.unity";
        const string FbxSourcePath = @"C:\Users\Lenovo\Downloads\Pouch_Cell_73Ah.fbx";
        const string FbxAssetPath = "Assets/Models/Pouch_Cell_73Ah.fbx";
        const string MatDir = "Assets/Materials";
        const string RTDir = "Assets/RenderTextures";
        const string DashboardRTPath = RTDir + "/RT_Dashboard.renderTexture";

        [MenuItem("BatteryTwin/Build Scene", priority = 0)]
        public static void Build()
        {
            try
            {
                EnsureDirectories();
                CopyFbxIfNeeded();

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // --- materials & render texture -------------------------------
                Material mTableWood   = GetOrCreateMaterial("M_TableWood",   new Color(0.42f, 0.27f, 0.16f), smoothness: 0.20f);
                Material mPouchCell   = GetOrCreateMaterial("M_PouchCell",   new Color(0.85f, 0.85f, 0.90f), smoothness: 0.55f, emission: true);
                Material mMonitorBz   = GetOrCreateMaterial("M_MonitorBezel",new Color(0.08f, 0.08f, 0.10f), smoothness: 0.30f);
                Material mMonitorStnd = GetOrCreateMaterial("M_MonitorStand",new Color(0.15f, 0.15f, 0.18f), smoothness: 0.40f);
                Material mBMS         = GetOrCreateMaterial("M_BMS",         new Color(0.10f, 0.45f, 0.18f), smoothness: 0.30f);
                Material mCell_R      = GetOrCreateMaterial("M_Cell18650_Red",   new Color(0.80f, 0.18f, 0.18f), smoothness: 0.55f);
                Material mCell_G      = GetOrCreateMaterial("M_Cell18650_Green", new Color(0.25f, 0.75f, 0.30f), smoothness: 0.55f);
                Material mCell_O      = GetOrCreateMaterial("M_Cell18650_Orange",new Color(0.95f, 0.55f, 0.10f), smoothness: 0.55f);
                Material mPanelBg     = GetOrCreateMaterial("M_PanelBg",     new Color(0.08f, 0.09f, 0.11f), smoothness: 0.10f);

                RenderTexture rt = GetOrCreateRenderTexture(DashboardRTPath, 1280, 720);
                Material mMonitorScreen = GetOrCreateUnlitTexturedMaterial("M_MonitorScreen", rt);

                // --- root buckets --------------------------------------------
                GameObject envRoot       = new GameObject("Environment");
                GameObject deviceRoot    = new GameObject("Devices");
                GameObject panelsRoot    = new GameObject("FloatingPanels");
                GameObject dashboardRoot = new GameObject("Dashboard");
                GameObject twinRoot      = new GameObject("_TwinController");

                // --- main camera ---------------------------------------------
                var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGO.tag = "MainCamera";
                var cam = camGO.GetComponent<Camera>();
                cam.fieldOfView = 45f;
                cam.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                camGO.transform.position = new Vector3(0f, 1.4f, -1.2f);
                camGO.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

                // --- directional light ---------------------------------------
                var lightGO = new GameObject("Directional Light", typeof(Light));
                var lt = lightGO.GetComponent<Light>();
                lt.type = LightType.Directional;
                lt.intensity = 1.1f;
                lt.color = new Color(1f, 0.96f, 0.90f);
                lightGO.transform.SetParent(envRoot.transform);
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // --- table ---------------------------------------------------
                GameObject table = CreatePrimitive(PrimitiveType.Cube, "Table", envRoot.transform,
                    new Vector3(0f, 0.75f, 0f), new Vector3(2.0f, 0.05f, 1.2f), mTableWood);

                // --- monitor (parent + bezel + screen + stand) ---------------
                GameObject monitorRoot = new GameObject("Monitor");
                monitorRoot.transform.SetParent(deviceRoot.transform);
                monitorRoot.transform.position = new Vector3(-0.5f, 0.78f, -0.2f);
                monitorRoot.transform.rotation = Quaternion.Euler(-10f, 0f, 0f);

                CreatePrimitive(PrimitiveType.Cube, "Monitor_Bezel", monitorRoot.transform,
                    new Vector3(0f, 0.16f, 0f), new Vector3(0.55f, 0.32f, 0.04f), mMonitorBz);

                GameObject screen = CreatePrimitive(PrimitiveType.Quad, "Monitor_Screen", monitorRoot.transform,
                    new Vector3(0f, 0.16f, -0.021f), new Vector3(0.50f, 0.28f, 1f), mMonitorScreen);

                CreatePrimitive(PrimitiveType.Cylinder, "Monitor_Stand", monitorRoot.transform,
                    new Vector3(0f, 0.025f, 0f), new Vector3(0.06f, 0.05f, 0.06f), mMonitorStnd);

                // --- pouch cell ----------------------------------------------
                GameObject pouch = null;
                if (File.Exists(Path.Combine(Application.dataPath, "../" + FbxAssetPath)))
                {
                    var fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath);
                    if (fbxPrefab != null)
                    {
                        pouch = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab);
                        pouch.name = "PouchCell";
                    }
                }
                if (pouch == null)
                {
                    // Fallback: a stylized pouch as a thin box. Bindings still work.
                    pouch = CreatePrimitive(PrimitiveType.Cube, "PouchCell", null,
                        Vector3.zero, new Vector3(0.20f, 0.30f, 0.02f), null);
                    Debug.LogWarning("PouchCell FBX not found at " + FbxAssetPath + ". Using a placeholder cube.");
                }
                pouch.transform.SetParent(deviceRoot.transform);
                pouch.transform.position = new Vector3(0.20f, 0.79f, 0.00f);
                pouch.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                // Try to find the renderer that the bindings should drive.
                Renderer pouchRenderer = pouch.GetComponentInChildren<Renderer>();
                if (pouchRenderer != null)
                {
                    pouchRenderer.sharedMaterial = mPouchCell;
                }

                // --- indicator cells (V / I / T) -----------------------------
                // The three small cylinders are data-driven: each one's height
                // and colour reflects one battery metric. IndicatorCellBinding
                // owns the runtime mutation; we only seed the initial pose.
                GameObject cellVoltage = CreatePrimitive(PrimitiveType.Cylinder, "Cell_Voltage", deviceRoot.transform,
                    new Vector3(-0.22f, 0.83f, 0.05f),  new Vector3(0.030f, 0.04f, 0.030f), mCell_G);
                GameObject cellCurrent = CreatePrimitive(PrimitiveType.Cylinder, "Cell_Current", deviceRoot.transform,
                    new Vector3(-0.14f, 0.83f, 0.05f),  new Vector3(0.032f, 0.04f, 0.032f), mCell_R);
                GameObject cellTemperature = CreatePrimitive(PrimitiveType.Cylinder, "Cell_Temperature", deviceRoot.transform,
                    new Vector3(-0.06f, 0.83f, 0.05f),  new Vector3(0.026f, 0.04f, 0.026f), mCell_O);

                // --- BMS board -----------------------------------------------
                CreatePrimitive(PrimitiveType.Cube, "BMS_Board", deviceRoot.transform,
                    new Vector3(0.30f, 0.785f, 0.08f), new Vector3(0.10f, 0.005f, 0.06f), mBMS);

                // (Current-flow particle system intentionally removed — the
                //  monitoring view is cleaner without it. CurrentParticleBinding
                //  is left in place for users who want to re-add it manually.)

                // --- floating panels -----------------------------------------
                // Arc them around the desk at ~eye level.
                TMP_Text panelCellId, panelVoltage, panelTemp, panelSoC, panelSoH, panelCurrent, panelPower, panelStatus;
                TMP_Text connLabel, dataSourceLabel, updateRateLabel, timeLabel, dateLabel;
                GameObject connConnectedIcon, connDisconnectedIcon;

                var cellPanel = MakeMultiLinePanel(panelsRoot.transform, "Panel_CellParameters",
                    new Vector3(0.85f, 1.35f, -0.10f), new Vector3(0.40f, 0.34f, 0.01f),
                    "CELL PARAMETERS",
                    new[] { "Cell ID", "Voltage", "Temperature", "SOC", "SOH", "Current", "Power", "Status" },
                    mPanelBg, out var cellPanelValues);
                panelCellId   = cellPanelValues[0];
                panelVoltage  = cellPanelValues[1];
                panelTemp     = cellPanelValues[2];
                panelSoC      = cellPanelValues[3];
                panelSoH      = cellPanelValues[4];
                panelCurrent  = cellPanelValues[5];
                panelPower    = cellPanelValues[6];
                panelStatus   = cellPanelValues[7];

                MakeBadgePanel(panelsRoot.transform, "Panel_Connection",
                    new Vector3(1.05f, 1.65f, -0.05f), new Vector3(0.30f, 0.10f, 0.01f),
                    "CONNECTION", "Connected", mPanelBg,
                    out connLabel, out connConnectedIcon, out connDisconnectedIcon);

                MakeBadgePanel(panelsRoot.transform, "Panel_DataSource",
                    new Vector3(1.05f, 1.52f, -0.05f), new Vector3(0.30f, 0.10f, 0.01f),
                    "DATA SOURCE", "Mock", mPanelBg,
                    out dataSourceLabel, out _, out _);

                MakeBadgePanel(panelsRoot.transform, "Panel_UpdateRate",
                    new Vector3(1.05f, 1.39f, -0.05f), new Vector3(0.30f, 0.10f, 0.01f),
                    "UPDATE RATE", "1.0 s", mPanelBg,
                    out updateRateLabel, out _, out _);

                MakeTimePanel(panelsRoot.transform, "Panel_Time",
                    new Vector3(1.05f, 1.25f, -0.05f), new Vector3(0.30f, 0.12f, 0.01f),
                    mPanelBg, out timeLabel, out dateLabel);

                // (System Log panel intentionally removed per project spec —
                //  user wants a clean monitoring view without log spam.)

                // --- dashboard (canvas → rendertexture → monitor screen) ----
                BuildDashboard(dashboardRoot.transform, rt, mPouchCell,
                    out var dashFields, out var dashGauges, out var dashCharts);

                // --- post-process volume (URP Bloom) -------------------------
                TryAddURPBloomVolume(envRoot.transform);

                // --- twin controller GameObject + components -----------------
                var mock = twinRoot.AddComponent<MockBatteryDataSource>();
                var twin = twinRoot.AddComponent<TwinController>();
                var connMon = twinRoot.AddComponent<ConnectionMonitor>();
                var timePanel = twinRoot.AddComponent<TimePanel>();

                twin.activeSourceBehaviour = mock;

                // Temperature + emission bindings on the pouch renderer.
                if (pouchRenderer != null)
                {
                    var tempBind = pouch.AddComponent<TemperatureColorBinding>();
                    tempBind.target = pouchRenderer;
                    twin.temperatureBindings.Add(tempBind);

                    var emiBind = pouch.AddComponent<SocEmissionBinding>();
                    emiBind.target = pouchRenderer;
                    twin.socEmissionBindings.Add(emiBind);
                }

                // Indicator-cell bindings — three small cylinders encode V/I/T.
                AddIndicatorBinding(twin, cellVoltage,     BatteryField.Voltage,
                    minV: 2.5f, maxV: 4.2f, absolute: false,
                    minH: 0.04f, maxH: 0.14f,
                    ramp: BuildVoltageGradient());
                AddIndicatorBinding(twin, cellCurrent,     BatteryField.Current,
                    minV: 0f, maxV: 2.0f, absolute: true,
                    minH: 0.04f, maxH: 0.14f,
                    ramp: BuildCurrentGradient());
                AddIndicatorBinding(twin, cellTemperature, BatteryField.Temperature,
                    minV: 20f, maxV: 55f, absolute: false,
                    minH: 0.04f, maxH: 0.14f,
                    ramp: BuildTempGradient());

                // Floating-panel text bindings.
                AddTextBinding(twin, panelCellId,   BatteryField.CellId,      "",   "");
                AddTextBinding(twin, panelVoltage,  BatteryField.Voltage,     "F2", " V");
                AddTextBinding(twin, panelTemp,     BatteryField.Temperature, "F1", " °C");
                AddTextBinding(twin, panelSoC,      BatteryField.SOC,         "F0", " %");
                AddTextBinding(twin, panelSoH,      BatteryField.SOH,         "F0", " %");
                AddTextBinding(twin, panelCurrent,  BatteryField.Current,     "F2", " A");
                AddTextBinding(twin, panelPower,    BatteryField.Power,       "F2", " W");
                AddTextBinding(twin, panelStatus,   BatteryField.Status,      "",   "");

                // Dashboard text bindings.
                AddTextBinding(twin, dashFields["CellId"],      BatteryField.CellId,      "",   "");
                AddTextBinding(twin, dashFields["Voltage"],     BatteryField.Voltage,     "F2", " V");
                AddTextBinding(twin, dashFields["Temperature"], BatteryField.Temperature, "F1", " °C");
                AddTextBinding(twin, dashFields["SOC"],         BatteryField.SOC,         "F0", " %");
                AddTextBinding(twin, dashFields["SOH"],         BatteryField.SOH,         "F0", " %");
                AddTextBinding(twin, dashFields["Current"],     BatteryField.Current,     "F2", " A");
                AddTextBinding(twin, dashFields["Power"],       BatteryField.Power,       "F2", " W");
                AddTextBinding(twin, dashFields["Status"],      BatteryField.Status,      "",   "");

                // Gauges.
                AddGaugeBinding(twin, dashGauges["SOC"],  BatteryField.SOC,  0, 100, BuildSocGradient());
                AddGaugeBinding(twin, dashGauges["SOH"],  BatteryField.SOH,  0, 100, BuildSohGradient());
                AddGaugeBinding(twin, dashGauges["Temp"], BatteryField.Temperature, 20, 55, BuildTempGradient());

                // Charts.
                ConfigureChart(dashCharts["V"], BatteryField.Voltage,     2.0f, 4.5f,  new Color(0.30f, 0.95f, 0.40f));
                ConfigureChart(dashCharts["I"], BatteryField.Current,    -35f,  35f,   new Color(0.40f, 0.75f, 1.00f));
                ConfigureChart(dashCharts["T"], BatteryField.Temperature, 15f,  60f,   new Color(1.00f, 0.55f, 0.20f));
                twin.charts.Add(dashCharts["V"]);
                twin.charts.Add(dashCharts["I"]);
                twin.charts.Add(dashCharts["T"]);

                // Connection monitor wiring.
                connMon.twin = twin;
                connMon.connectionLabel = connLabel;
                connMon.connectedIcon = connConnectedIcon;
                connMon.disconnectedIcon = connDisconnectedIcon;
                connMon.dataSourceLabel = dataSourceLabel;
                connMon.updateRateLabel = updateRateLabel;
                connMon.displayedUpdateRate = mock.updateInterval;

                timePanel.timeLabel = timeLabel;
                timePanel.dateLabel = dateLabel;

                // --- save the scene ------------------------------------------
                if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
                EditorSceneManager.SaveScene(scene, SceneAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[BatteryTwin] Scene built and saved → " + SceneAssetPath + ". Press Play.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("BatteryTwin build failed", e.Message + "\n\nSee Console for details.", "OK");
            }
        }

        // ============================================================================
        // helpers
        // ============================================================================

        static void EnsureDirectories()
        {
            foreach (var d in new[] { "Assets/Scenes", "Assets/Models", "Assets/Materials",
                                       "Assets/Prefabs", "Assets/RenderTextures", "Assets/Settings" })
            {
                if (!AssetDatabase.IsValidFolder(d))
                {
                    string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                    string leaf = Path.GetFileName(d);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }

        static void CopyFbxIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FbxAssetPath) != null) return;
            if (!File.Exists(FbxSourcePath))
            {
                Debug.LogWarning("[BatteryTwin] FBX not at " + FbxSourcePath + " — placeholder will be used.");
                return;
            }
            string dest = Path.Combine(Application.dataPath, "Models", "Pouch_Cell_73Ah.fbx");
            File.Copy(FbxSourcePath, dest, overwrite: true);
            AssetDatabase.ImportAsset(FbxAssetPath, ImportAssetOptions.ForceUpdate);
        }

        static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.3f, bool emission = false)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GetOrCreateUnlitTexturedMaterial(string name, Texture tex)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static RenderTexture GetOrCreateRenderTexture(string path, int w, int h)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (rt == null)
            {
                rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    antiAliasing = 2,
                    wrapMode = TextureWrapMode.Clamp,
                };
                AssetDatabase.CreateAsset(rt, path);
            }
            return rt;
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (mat != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
            // Drop collider — purely visual scene.
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            return go;
        }

        static void AddTextBinding(TwinController twin, TMP_Text label, BatteryField field, string format, string unit)
        {
            if (label == null) return;
            var b = label.gameObject.AddComponent<DashboardTextBinding>();
            b.label = label;
            b.field = field;
            b.format = format;
            b.unit = unit;
            twin.textBindings.Add(b);
        }

        static void AddIndicatorBinding(TwinController twin, GameObject cell, BatteryField field,
            float minV, float maxV, bool absolute, float minH, float maxH, Gradient ramp)
        {
            if (cell == null) return;
            var b = cell.AddComponent<IndicatorCellBinding>();
            b.target = cell.transform;
            b.targetRenderer = cell.GetComponent<Renderer>();
            b.field = field;
            b.minValue = minV;
            b.maxValue = maxV;
            b.useAbsoluteValue = absolute;
            b.minHeight = minH;
            b.maxHeight = maxH;
            b.baseY = 0.79f; // desk surface (matches Table's top in this scene)
            b.colorRamp = ramp;
            twin.indicatorBindings.Add(b);
        }

        static Gradient BuildVoltageGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.30f, 0.25f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.85f, 0.30f), 0.4f),
                        new GradientColorKey(new Color(0.30f, 0.90f, 0.40f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient BuildCurrentGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.55f, 0.60f, 0.95f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.70f, 0.30f), 0.6f),
                        new GradientColorKey(new Color(0.95f, 0.25f, 0.20f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static void AddGaugeBinding(TwinController twin, Image img, BatteryField field, float min, float max, Gradient grad)
        {
            if (img == null) return;
            var b = img.gameObject.AddComponent<GaugeBinding>();
            b.fillImage = img;
            b.field = field;
            b.min = min;
            b.max = max;
            b.fillGradient = grad;
            twin.gaugeBindings.Add(b);
        }

        static void ConfigureChart(RollingLineChart chart, BatteryField field, float yMin, float yMax, Color color)
        {
            chart.field = field;
            chart.yMin = yMin;
            chart.yMax = yMax;
            // Graphic.color multiplies vertex colour. Keep it white so lineColor passes through.
            chart.color = Color.white;
            chart.lineColor = color;
            chart.capacity = 120;
            chart.lineThickness = 2f;
        }

        static Gradient BuildSocGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.25f, 0.20f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.80f, 0.20f), 0.4f),
                        new GradientColorKey(new Color(0.30f, 0.90f, 0.40f), 0.8f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient BuildSohGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.95f, 0.25f, 0.20f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.85f, 0.30f), 0.6f),
                        new GradientColorKey(new Color(0.30f, 0.90f, 0.40f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient BuildTempGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.30f, 0.60f, 1.00f), 0f),
                        new GradientColorKey(new Color(0.95f, 0.80f, 0.30f), 0.6f),
                        new GradientColorKey(new Color(0.95f, 0.30f, 0.25f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static void TryAddURPBloomVolume(Transform parent)
        {
            // Best-effort — only adds the GameObject. The Bloom override is
            // added at runtime by Unity's URP defaults; if you want to tune it
            // open the Global Volume and add Bloom + ColorAdjustments manually.
            var v = new GameObject("Global Volume");
            v.transform.SetParent(parent);
            var vol = v.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0;
        }

        // ----- panel builders -------------------------------------------------

        static GameObject MakeWorldCanvas(Transform parent, string name, Vector3 pos, Vector3 size, Material bg)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, -25f, 0f); // angle toward camera

            // Background quad (so the panel is visible from any angle).
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Bg";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = bg;

            // Canvas.
            var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(root.transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 400f * (size.y / Mathf.Max(0.0001f, size.x)));
            rect.localScale = Vector3.one * (size.x / 400f);
            rect.localPosition = Vector3.zero;
            return canvasGO;
        }

        static TMP_Text MakeTMP(Transform parent, string name, string text, float fontSize, Color color,
            Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return tmp;
        }

        static GameObject MakeMultiLinePanel(Transform parent, string name, Vector3 pos, Vector3 size,
            string title, string[] labels, Material bg, out TMP_Text[] valueLabels)
        {
            var canvasGO = MakeWorldCanvas(parent, name, pos, size, bg);
            Transform t = canvasGO.transform;

            float W = ((RectTransform)t).sizeDelta.x;
            float H = ((RectTransform)t).sizeDelta.y;
            MakeTMP(t, "Title", title, 22f, new Color(0.85f, 0.86f, 0.90f),
                new Vector2(20f, -10f), new Vector2(W - 40f, 30f));

            valueLabels = new TMP_Text[labels.Length];
            float rowH = (H - 50f) / Mathf.Max(1, labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                float y = -50f - i * rowH;
                MakeTMP(t, "Lbl_" + labels[i], labels[i], 18f, new Color(0.70f, 0.72f, 0.78f),
                    new Vector2(20f, y), new Vector2(W * 0.55f, rowH));
                var v = MakeTMP(t, "Val_" + labels[i], "—", 18f, ValueColor(labels[i]),
                    new Vector2(W * 0.55f, y), new Vector2(W * 0.40f, rowH), TextAlignmentOptions.Right);
                valueLabels[i] = v;
            }
            return canvasGO;
        }

        static Color ValueColor(string field)
        {
            switch (field)
            {
                case "Voltage":     return new Color(0.30f, 0.95f, 0.40f);
                case "Temperature": return new Color(1.00f, 0.55f, 0.20f);
                case "SOC":         return new Color(0.95f, 0.30f, 0.25f);
                case "SOH":         return new Color(0.95f, 0.80f, 0.30f);
                case "Current":     return new Color(0.75f, 0.55f, 1.00f);
                case "Power":       return new Color(0.40f, 0.85f, 0.95f);
                case "Status":      return new Color(0.95f, 0.55f, 0.25f);
                case "Cell ID":     return new Color(0.40f, 0.75f, 1.00f);
                default:            return Color.white;
            }
        }

        static void MakeBadgePanel(Transform parent, string name, Vector3 pos, Vector3 size,
            string title, string initialValue, Material bg,
            out TMP_Text valueLabel, out GameObject connectedIcon, out GameObject disconnectedIcon)
        {
            var canvasGO = MakeWorldCanvas(parent, name, pos, size, bg);
            Transform t = canvasGO.transform;
            float W = ((RectTransform)t).sizeDelta.x;
            float H = ((RectTransform)t).sizeDelta.y;

            MakeTMP(t, "Title", title, 18f, new Color(0.70f, 0.72f, 0.78f),
                new Vector2(15f, -10f), new Vector2(W - 30f, 22f));
            valueLabel = MakeTMP(t, "Value", initialValue, 24f, new Color(0.40f, 0.92f, 0.45f),
                new Vector2(15f, -36f), new Vector2(W - 30f, H - 40f));

            // Two square icons; we just toggle GameObjects active.
            connectedIcon = new GameObject("Icon_Connected", typeof(RectTransform), typeof(Image));
            connectedIcon.transform.SetParent(t, false);
            var ci = connectedIcon.GetComponent<Image>();
            ci.color = new Color(0.30f, 0.90f, 0.40f);
            var crt = (RectTransform)connectedIcon.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-15f, -15f);
            crt.sizeDelta = new Vector2(20f, 20f);

            disconnectedIcon = new GameObject("Icon_Disconnected", typeof(RectTransform), typeof(Image));
            disconnectedIcon.transform.SetParent(t, false);
            var di = disconnectedIcon.GetComponent<Image>();
            di.color = new Color(0.95f, 0.30f, 0.25f);
            var drt = (RectTransform)disconnectedIcon.transform;
            drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(1f, 1f);
            drt.anchoredPosition = new Vector2(-15f, -15f);
            drt.sizeDelta = new Vector2(20f, 20f);
            disconnectedIcon.SetActive(false);
        }

        static void MakeTimePanel(Transform parent, string name, Vector3 pos, Vector3 size, Material bg,
            out TMP_Text timeLabel, out TMP_Text dateLabel)
        {
            var canvasGO = MakeWorldCanvas(parent, name, pos, size, bg);
            Transform t = canvasGO.transform;
            float W = ((RectTransform)t).sizeDelta.x;

            MakeTMP(t, "Title", "TIME", 16f, new Color(0.70f, 0.72f, 0.78f),
                new Vector2(15f, -10f), new Vector2(W - 30f, 20f));
            timeLabel = MakeTMP(t, "TimeValue", "00:00:00", 30f, new Color(0.40f, 0.75f, 1.00f),
                new Vector2(15f, -34f), new Vector2(W - 30f, 38f));
            dateLabel = MakeTMP(t, "DateValue", "—", 16f, new Color(0.40f, 0.75f, 1.00f),
                new Vector2(15f, -78f), new Vector2(W - 30f, 22f));
        }

        // ----- dashboard (rendertexture canvas) -------------------------------

        static void BuildDashboard(Transform parent, RenderTexture rt, Material pouchMat,
            out Dictionary<string, TMP_Text> fields,
            out Dictionary<string, Image> gauges,
            out Dictionary<string, RollingLineChart> charts)
        {
            // Dedicated camera that ONLY renders the dashboard layer.
            int dashLayer = EnsureDashboardLayer();

            var dashCamGO = new GameObject("DashboardCamera", typeof(Camera));
            dashCamGO.transform.SetParent(parent);
            dashCamGO.transform.position = new Vector3(0f, -1000f, 0f); // park far away
            var dashCam = dashCamGO.GetComponent<Camera>();
            dashCam.orthographic = true;
            dashCam.orthographicSize = 360f;
            dashCam.cullingMask = 1 << dashLayer;
            dashCam.clearFlags = CameraClearFlags.SolidColor;
            dashCam.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
            dashCam.targetTexture = rt;
            dashCam.nearClipPlane = -10f;
            dashCam.farClipPlane = 10f;

            var canvasGO = new GameObject("DashboardCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(dashCamGO.transform, false);
            canvasGO.layer = dashLayer;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = dashCam;
            canvas.planeDistance = 1f;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(1280f, 720f);

            // Apply the dash layer to children recursively.
            void Layer(GameObject g) { g.layer = dashLayer; foreach (Transform c in g.transform) Layer(c.gameObject); }

            // Title.
            var title = MakeTMP(crt, "Title", "BATTERY MONITORING SYSTEM", 36f,
                new Color(0.90f, 0.90f, 0.95f), new Vector2(40f, -30f), new Vector2(1200f, 50f),
                TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            ((RectTransform)title.transform).anchorMin = new Vector2(0f, 1f);
            ((RectTransform)title.transform).anchorMax = new Vector2(1f, 1f);
            ((RectTransform)title.transform).pivot = new Vector2(0.5f, 1f);
            ((RectTransform)title.transform).anchoredPosition = new Vector2(0f, -30f);
            ((RectTransform)title.transform).sizeDelta = new Vector2(0f, 50f);

            // Left column: labels + values.
            fields = new Dictionary<string, TMP_Text>();
            string[] rowLabels = { "Cell ID", "Voltage", "Temperature", "State of Charge (SOC)", "State of Health (SOH)", "Current", "Power" };
            string[] rowKeys   = { "CellId", "Voltage", "Temperature", "SOC", "SOH", "Current", "Power" };
            for (int i = 0; i < rowLabels.Length; i++)
            {
                float y = -110f - i * 50f;
                MakeTMP(crt, "Lbl_" + rowKeys[i], rowLabels[i], 22f, new Color(0.72f, 0.74f, 0.80f),
                    new Vector2(60f, y), new Vector2(420f, 40f));
                var v = MakeTMP(crt, "Val_" + rowKeys[i], "—", 22f, ValueColor(rowKeys[i] switch
                {
                    "CellId" => "Cell ID",
                    "SOC" => "SOC",
                    "SOH" => "SOH",
                    _ => rowKeys[i],
                }), new Vector2(420f, y), new Vector2(220f, 40f), TextAlignmentOptions.Right);
                fields[rowKeys[i]] = v;
            }

            // Status row.
            MakeTMP(crt, "Lbl_Status", "Status:", 22f, new Color(0.85f, 0.85f, 0.90f),
                new Vector2(60f, -480f), new Vector2(120f, 40f));
            fields["Status"] = MakeTMP(crt, "Val_Status", "—", 26f, new Color(0.95f, 0.55f, 0.25f),
                new Vector2(180f, -480f), new Vector2(280f, 40f));

            // Right column: charts.
            MakeTMP(crt, "ChartsTitle", "Real-Time Graphs", 22f, new Color(0.85f, 0.86f, 0.90f),
                new Vector2(700f, -110f), new Vector2(540f, 32f));

            charts = new Dictionary<string, RollingLineChart>();
            charts["V"] = MakeChart(crt, "Chart_V", new Vector2(700f, -160f), new Vector2(540f, 220f));
            charts["I"] = MakeChart(crt, "Chart_I", new Vector2(700f, -160f), new Vector2(540f, 220f)); // overlaid by default
            charts["T"] = MakeChart(crt, "Chart_T", new Vector2(700f, -160f), new Vector2(540f, 220f));

            // Gauges at the bottom right.
            gauges = new Dictionary<string, Image>();
            gauges["SOC"]  = MakeGauge(crt, "Gauge_SOC",  "SOC",  new Vector2(700f, -430f));
            gauges["SOH"]  = MakeGauge(crt, "Gauge_SOH",  "SOH",  new Vector2(900f, -430f));
            gauges["Temp"] = MakeGauge(crt, "Gauge_Temp", "Temp", new Vector2(1100f, -430f));

            // Apply layer to everything under the canvas.
            Layer(canvasGO);
        }

        static RollingLineChart MakeChart(Transform parent, string name, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return go.AddComponent<RollingLineChart>();
        }

        static Image MakeGauge(Transform parent, string name, string title, Vector2 anchoredPos)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            var hrt = (RectTransform)holder.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(0f, 1f);
            hrt.pivot = new Vector2(0f, 1f);
            hrt.anchoredPosition = anchoredPos;
            hrt.sizeDelta = new Vector2(160f, 160f);

            // Background ring.
            var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(holder.transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.18f, 0.18f, 0.22f);
            bgImg.type = Image.Type.Filled;
            bgImg.fillMethod = Image.FillMethod.Radial360;
            bgImg.fillAmount = 1f;
            ((RectTransform)bg.transform).anchorMin = Vector2.zero;
            ((RectTransform)bg.transform).anchorMax = Vector2.one;
            ((RectTransform)bg.transform).offsetMin = Vector2.zero;
            ((RectTransform)bg.transform).offsetMax = Vector2.zero;

            // Fill ring.
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(holder.transform, false);
            var img = fill.GetComponent<Image>();
            img.color = new Color(0.95f, 0.30f, 0.25f);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0.2f;
            ((RectTransform)fill.transform).anchorMin = Vector2.zero;
            ((RectTransform)fill.transform).anchorMax = Vector2.one;
            ((RectTransform)fill.transform).offsetMin = Vector2.zero;
            ((RectTransform)fill.transform).offsetMax = Vector2.zero;

            // Title.
            MakeTMP(holder.transform, "Title", title, 16f, new Color(0.75f, 0.77f, 0.83f),
                new Vector2(0f, -10f), new Vector2(160f, 22f), TextAlignmentOptions.Center);
            return img;
        }

        static int EnsureDashboardLayer()
        {
            const string layerName = "UI_Dashboard";
            int idx = LayerMask.NameToLayer(layerName);
            if (idx >= 0) return idx;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerSP.stringValue))
                {
                    layerSP.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }
            Debug.LogWarning("[BatteryTwin] No free user layer slot to register UI_Dashboard. Falling back to Default layer.");
            return 0;
        }
    }
}
#endif
