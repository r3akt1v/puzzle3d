using UnityEngine;

namespace Puzzle3D
{
    /// <summary>
    /// Mouse controls: pick up / drag pieces, 3D-tumble rotation, camera zoom & orbit.
    /// LMB drag = move, RMB drag = rotate, wheel = height (held) / zoom (free), R = reset tilt.
    /// </summary>
    public class DragController : MonoBehaviour
    {
        public Camera Cam;
        public Board Board;
        public bool InputEnabled = true;

        PieceController held;
        bool heldByRight; // picked up with RMB = rotate-in-hand mode
        float holdY = 1.1f;
        float minHoldY = 0.35f, maxHoldY = 4f;

        // camera orbit state
        float camYaw = 0f, camPitch = 0f;
        Vector3 camBasePos;
        Quaternion camBaseRot;

        public event System.Action<PieceController> OnPickedUp;
        public event System.Action OnReleased;

        void Start()
        {
            if (Cam == null) Cam = Camera.main;
        }

        public void SetCameraBase(Vector3 pos, Quaternion rot)
        {
            camBasePos = pos; camBaseRot = rot;
            camYaw = 0; camPitch = 0;
            ApplyCamera();
        }

        void Update()
        {
            if (Cam == null || Board == null) return;
            if (!InputEnabled)
            {
                if (held != null) ForceRelease();
                return;
            }

            Vector2 mouse = Input.mousePosition;
            Ray ray = Cam.ScreenPointToRay(mouse);

            // UI click guard
            bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
                          UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            if (held == null)
            {
                // ---- free camera: RMB orbit, wheel zoom ----
                if (Input.GetMouseButtonDown(1) && !overUI) { /* orbit starts */ }
                if (Input.GetMouseButton(1) && !overUI)
                {
                    camYaw += Input.GetAxis("Mouse X") * 4f;
                    camPitch = Mathf.Clamp(camPitch - Input.GetAxis("Mouse Y") * 3f, -15f, 25f);
                    ApplyCamera();
                }
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f && !overUI) ZoomCamera(scroll);

                // ---- pick up with LMB (move mode) ----
                if (Input.GetMouseButtonDown(0) && !overUI)
                {
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        var pc = hit.collider.GetComponentInParent<PieceController>();
                        if (pc != null && pc.State == PieceState.OnTable)
                        {
                            held = pc;
                            heldByRight = false;
                            pc.State = PieceState.Held;
                            holdY = Mathf.Max(minHoldY, pc.transform.position.y + 0.9f);
                            OnPickedUp?.Invoke(pc);
                        }
                    }
                }
                // ---- pick up with RMB (rotate-in-hand mode) ----
                if (Input.GetMouseButtonDown(1) && !overUI && held == null)
                {
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        var pc = hit.collider.GetComponentInParent<PieceController>();
                        if (pc != null && pc.State == PieceState.OnTable)
                        {
                            held = pc;
                            heldByRight = true;
                            pc.State = PieceState.Held;
                            holdY = Mathf.Max(minHoldY, pc.transform.position.y + 0.5f);
                            OnPickedUp?.Invoke(pc);
                        }
                    }
                }
            }
            else if (heldByRight)
            {
                // ---- rotate-in-hand: mouse delta rotates, no translation ----
                float dx = Input.GetAxis("Mouse X");
                float dy = Input.GetAxis("Mouse Y");
                held.transform.Rotate(Vector3.up, dx * 0.5f, Space.World);
                held.transform.Rotate(Cam.transform.right, -dy * 0.5f, Space.World);

                // scroll also rotates (trackpad two-finger)
                Vector2 scroll = Input.mouseScrollDelta;
                if (Mathf.Abs(scroll.y) > 0.01f)
                    held.transform.Rotate(Vector3.up, scroll.y * 3f, Space.World);
                if (Mathf.Abs(scroll.x) > 0.01f)
                    held.transform.Rotate(Cam.transform.right, scroll.x * 3f, Space.World);

                if (Input.GetKeyDown(KeyCode.R))
                {
                    Vector3 fwd = held.transform.forward; fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.001f)
                        held.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                    else
                        held.transform.rotation = Quaternion.identity;
                }

                // snap assist still applies (position unchanged, but rotation aligns)
                if (held.IsCloseToTarget(Board.SnapRadius * 0.9f) && held.transform.up.y > 0.4f)
                {
                    Board.ShowAssistGhost(held);
                    held.transform.rotation = Quaternion.Slerp(
                        held.transform.rotation, Quaternion.identity,
                        1f - Mathf.Exp(-8f * Time.deltaTime));
                }
                else Board.HideAssistGhost();

                if (Input.GetMouseButtonUp(1))
                {
                    Board.HideAssistGhost();
                    if (!held.TryPlace()) held.Settle();
                    held = null; heldByRight = false;
                    OnReleased?.Invoke();
                }
            }
            else
            {
                // ---- move held piece on horizontal plane ----
                Plane dragPlane = new Plane(Vector3.up, new Vector3(0, holdY, 0));
                if (dragPlane.Raycast(ray, out float dist))
                {
                    Vector3 target = ray.GetPoint(dist);
                    target.y = holdY;
                    held.transform.position = Vector3.Lerp(held.transform.position, target, 1f - Mathf.Exp(-18f * Time.deltaTime));
                }

                // ---- rotate: scroll (trackpad) or RMB drag ----
                Vector2 scrollMove = Input.mouseScrollDelta;
                if (Mathf.Abs(scrollMove.y) > 0.01f)
                    held.transform.Rotate(Vector3.up, scrollMove.y * 3f, Space.World);
                if (Mathf.Abs(scrollMove.x) > 0.01f)
                    held.transform.Rotate(Cam.transform.right, scrollMove.x * 3f, Space.World);
                if (Input.GetMouseButton(1))
                {
                    float dx = Input.GetAxis("Mouse X");
                    float dy = Input.GetAxis("Mouse Y");
                    held.transform.Rotate(Vector3.up, dx * 4f, Space.World);
                    held.transform.Rotate(Cam.transform.right, -dy * 4f, Space.World);
                }
                // R = reset tilt (keep yaw)
                if (Input.GetKeyDown(KeyCode.R))
                {
                    Vector3 fwd = held.transform.forward; fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.001f)
                        held.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
                    else
                        held.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                }

                // ---- snap assist ghost ----
                if (held.IsCloseToTarget(Board.SnapRadius * 0.9f))
                {
                    Board.ShowAssistGhost(held);
                    // magnet: pull XZ toward target, ease rotation upright
                    Vector3 t = held.TargetPos;
                    Vector3 cur = held.transform.position;
                    float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
                    cur.x = Mathf.Lerp(cur.x, t.x, k);
                    cur.z = Mathf.Lerp(cur.z, t.z, k);
                    held.transform.position = cur;
                    if (held.transform.up.y > 0.4f)
                    {
                        held.transform.rotation = Quaternion.Slerp(
                            held.transform.rotation, Quaternion.identity,
                            1f - Mathf.Exp(-8f * Time.deltaTime));
                    }
                }
                else
                {
                    Board.HideAssistGhost();
                }

                // ---- release ----
                if (Input.GetMouseButtonUp(0))
                {
                    Board.HideAssistGhost();
                    if (!held.TryPlace()) held.Settle();
                    held = null;
                    OnReleased?.Invoke();
                }
            }
        }

        void ForceRelease()
        {
            if (held == null) return;
            Board.HideAssistGhost();
            held.Settle();
            held = null;
        }

        void ZoomCamera(float scroll)
        {
            Vector3 dir = camBasePos - Vector3.zero;
            // zoom toward board center (origin area)
            Vector3 focus = new Vector3(0, 0, Board != null ? Board.Origin.z * 0.3f : 0);
            Vector3 toCam = camBasePos - focus;
            float len = Mathf.Clamp(toCam.magnitude - scroll * 1.2f, 6f, 45f);
            camBasePos = focus + toCam.normalized * len;
            float sy = camYaw, sp = camPitch;
            SetCameraBase(camBasePos, camBaseRot);
            camYaw = sy; camPitch = sp;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (Cam == null) return;
            Cam.transform.SetPositionAndRotation(camBasePos, camBaseRot);
            // apply orbit offsets around focus
            Vector3 focus = new Vector3(0, 0, Board != null ? Board.Origin.z * 0.3f : 0);
            Cam.transform.RotateAround(focus, Vector3.up, camYaw);
            Cam.transform.RotateAround(focus, Cam.transform.right, camPitch);
        }
    }
}
