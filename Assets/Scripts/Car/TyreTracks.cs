using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoidStunts
{
    /// <summary>
    /// Every wheel prints a tread ribbon that fades over ~45 s; slip, braking, drift and wheelspin print darker.
    /// One ring-buffered mesh; each new segment uploads only its own 4 vertices.
    /// </summary>
    public class TyreTracks : MonoBehaviour
    {
        [StructLayout(LayoutKind.Sequential)] struct V { public Vector3 p; public Vector2 uv; public Vector2 info; }
        const int N = 4000;
        readonly V[] verts = new V[N * 4]; readonly V[] seg = new V[4];
        Mesh mesh; int head; CarController car;
        readonly Vector3?[] last = new Vector3?[4]; readonly float[] along = new float[4];

        public void Init(CarController c)
        {
            car = c;
            mesh = new Mesh { name = "Tracks" };
            mesh.SetVertexBufferParams(N * 4, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3), new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2), new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
            mesh.SetVertexBufferData(verts, 0, 0, verts.Length);
            var idx = new int[N * 6]; for (int i = 0; i < N; i++) { int v = i * 4, k = i * 6; idx[k] = v; idx[k + 1] = v + 2; idx[k + 2] = v + 1; idx[k + 3] = v + 1; idx[k + 4] = v + 2; idx[k + 5] = v + 3; }
            mesh.SetIndexBufferParams(idx.Length, IndexFormat.UInt32); mesh.SetIndexBufferData(idx, 0, 0, idx.Length);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, idx.Length), MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20000);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(Lib.Tracks); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        public void ResetAll()
        {
            for (int i = 0; i < verts.Length; i++) verts[i] = default;
            mesh.SetVertexBufferData(verts, 0, 0, verts.Length); for (int i = 0; i < 4; i++) last[i] = null;
        }

        void LateUpdate()
        {
            if (car == null) return;
            float now = Time.time; Shader.SetGlobalFloat("_VS_Now", now);
            var t = car.transform; var vel = car.rb.linearVelocity; float hs = new Vector2(vel.x, vel.z).magnitude;
            for (int i = 0; i < 4; i++)
            {
                var w = car.wheels[i]; if (!w.hit) { last[i] = null; continue; }
                float tw = w.front ? CarVisual.WV.fw : CarVisual.WV.rw;
                // the visual tyre sits at ±0.98/1.0 m; print under it
                var lp = new Vector3(Mathf.Sign(w.local.x) * (w.front ? CarVisual.WV.fx : CarVisual.WV.rx), w.local.y, w.local.z);
                var p = t.TransformPoint(lp) - t.up * (CarController.RAYLEN - w.comp) + w.n * .012f;
                if (last[i] == null) { last[i] = p; continue; }
                var d = p - last[i].Value; float L = d.magnitude;
                if (L > 4) { last[i] = p; continue; }
                if (L < .34f || hs < 1) continue;
                float str = Mathf.Clamp01(.32f + Mathf.Abs(w.vlat) * .09f + (car.drifting && !w.front ? .55f : 0) + (car.inp.brake > 0 && hs > 4 ? .35f : 0) + (car.boosting && !w.front && hs < 16 ? .3f : 0));
                d /= L; var side = Vector3.Cross(w.n, d).normalized * (tw * .41f);
                var a = last[i].Value;
                seg[0] = new V { p = a - side, uv = new Vector2(0, along[i]), info = new Vector2(now, str) };
                seg[1] = new V { p = a + side, uv = new Vector2(1, along[i]), info = new Vector2(now, str) };
                along[i] += L;
                seg[2] = new V { p = p - side, uv = new Vector2(0, along[i]), info = new Vector2(now, str) };
                seg[3] = new V { p = p + side, uv = new Vector2(1, along[i]), info = new Vector2(now, str) };
                for (int k = 0; k < 4; k++) verts[head * 4 + k] = seg[k];
                mesh.SetVertexBufferData(seg, 0, head * 4, 4, 0, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                head = (head + 1) % N; last[i] = p;
            }
        }
    }
}
