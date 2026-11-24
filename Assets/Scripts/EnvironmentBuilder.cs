using UnityEngine;

namespace Puzzle3D
{
    /// <summary>Creates the play environment: table, frame, lights, camera, EventSystem.</summary>
    public static class EnvironmentBuilder
    {
        public static Material TableMaterial;
        public static Material FrameMaterial;

        public static void Ensure()
        {
            EnsureCamera();
            EnsureLight();
            EnsureTableAndFrame();
            EnsureEventSystem();
        }

        static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
        }

        static void EnsureLight()
        {
            var existing = Object.FindFirstObjectByType<Light>();
            if (existing != null) return;
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            go.transform.rotation = Quaternion.Euler(48f, -30f, 0f);

            // soft ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
        }

        static void EnsureTableAndFrame()
        {
            if (TableMaterial == null)
            {
                TableMaterial = new Material(Shader.Find("Standard"));
                TableMaterial.color = new Color(0.23f, 0.28f, 0.32f); // dark slate felt
                TableMaterial.SetFloat("_Glossiness", 0.08f);
            }
            if (FrameMaterial == null)
            {
                FrameMaterial = new Material(Shader.Find("Standard"));
                FrameMaterial.color = new Color(0.55f, 0.42f, 0.3f); // warm wood
                FrameMaterial.SetFloat("_Glossiness", 0.35f);
            }

            // Table: big slab, top surface at y=0
            var table = GameObject.Find("Table");
            if (table == null)
            {
                table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "Table";
                table.transform.position = new Vector3(0, -0.5f, 0);
                table.transform.localScale = new Vector3(30f, 1f, 24f);
                table.GetComponent<MeshRenderer>().sharedMaterial = TableMaterial;
                table.GetComponent<BoxCollider>().enabled = false; // pieces shouldn't hit table collider
            }
        }

        /// <summary>Resize the table slab to fit the scatter area of the current level.</summary>
        public static void ResizeTable(float width, float depth)
        {
            var table = GameObject.Find("Table");
            if (table == null) return;
            table.transform.localScale = new Vector3(
                Mathf.Max(30f, width), 1f, Mathf.Max(24f, depth));
        }

        /// <summary>Creates the wooden frame outline around the board. Call per level.</summary>
        public static void BuildFrame(Board board, Transform parent)
        {
            var old = GameObject.Find("BoardFrame");
            if (old != null) Object.Destroy(old);

            var frame = new GameObject("BoardFrame");
            frame.transform.SetParent(parent, false);
            float w = board.BoardW + 0.5f;
            float h = board.BoardH + 0.5f;
            float bar = 0.16f, height = 0.09f, y = 0.045f;
            Vector3 c = board.Origin;

            MakeBar(frame.transform, "F_N", new Vector3(c.x, y, c.z + h * 0.5f), new Vector3(w, height, bar));
            MakeBar(frame.transform, "F_S", new Vector3(c.x, y, c.z - h * 0.5f), new Vector3(w, height, bar));
            MakeBar(frame.transform, "F_E", new Vector3(c.x + w * 0.5f, y, c.z), new Vector3(bar, height, h));
            MakeBar(frame.transform, "F_W", new Vector3(c.x - w * 0.5f, y, c.z), new Vector3(bar, height, h));
        }

        static void MakeBar(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.position = pos;
            b.transform.localScale = scale;
            b.GetComponent<MeshRenderer>().sharedMaterial = FrameMaterial;
            Object.Destroy(b.GetComponent<BoxCollider>());
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }
}
