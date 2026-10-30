using UnityEngine;

namespace CounterTopRearrange
{
    /// <summary>
    /// Builds the visible model for a spawnable prefab at runtime: a tinted
    /// primitive whose base rests on the prefab origin (so the placement
    /// controller can sit the origin on the detected surface). Cone has no
    /// Unity primitive, so its mesh is generated procedurally.
    /// </summary>
    [ExecuteInEditMode]
    public class SpawnablePrimitive : MonoBehaviour
    {
        public enum Shape
        {
            Cube,
            Sphere,
            Cylinder,
            Capsule,
            Cone
        }

        [Tooltip("Primitive shape to build.")]
        public Shape shape = Shape.Cube;

        [Tooltip("Tint applied to the model material.")]
        public Color tint = Color.white;

        [Tooltip("Model size in meters. For cone/cylinder, x/z are the base diameter and y the height.")]
        public Vector3 size = new Vector3(0.12f, 0.12f, 0.12f);

        const string ModelChildName = "Model";

        void Awake()
        {
            EnsureModel();
        }

#if UNITY_EDITOR
        bool _validateQueued;

        void OnValidate()
        {
            // Keep the prefab preview in sync while editing; the runtime path
            // is Awake -> EnsureModel.
            if (!Application.isPlaying && !_validateQueued)
            {
                _validateQueued = true;
                UnityEditor.EditorApplication.delayCall += DelayedValidate;
            }
        }

        void DelayedValidate()
        {
            _validateQueued = false;
            if (this != null && !Application.isPlaying)
                EnsureModel();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = tint;
            Gizmos.DrawWireCube(transform.position + Vector3.up * size.y * 0.5f, size);
        }
#endif

        /// <summary>Rebuilds the Model child when missing or stale.</summary>
        public void EnsureModel()
        {
            Transform existing = transform.Find(ModelChildName);
            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.DestroyObjectImmediate(existing.gameObject);
                else
#endif
                    Destroy(existing.gameObject);
            }

            GameObject model;
            if (shape == Shape.Cone)
                model = BuildCone();
            else
                model = GameObject.CreatePrimitive(ToPrimitiveType(shape));

            model.name = ModelChildName;
            model.transform.SetParent(transform, false);
            model.transform.localScale = size;
            // Base of the model rests on the prefab origin.
            model.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

            Renderer renderer = model.GetComponent<Renderer>();
            if (renderer != null)
                Tint(renderer, tint);
        }

        static PrimitiveType ToPrimitiveType(Shape value)
        {
            switch (value)
            {
                case Shape.Sphere: return PrimitiveType.Sphere;
                case Shape.Cylinder: return PrimitiveType.Cylinder;
                case Shape.Capsule: return PrimitiveType.Capsule;
                default: return PrimitiveType.Cube;
            }
        }

        static void Tint(Renderer renderer, Color color)
        {
            Material material = renderer.material;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        static GameObject BuildCone()
        {
            const int sides = 24;
            GameObject cone = new GameObject("Cone");
            MeshFilter filter = cone.AddComponent<MeshFilter>();
            cone.AddComponent<MeshRenderer>();
            cone.AddComponent<MeshCollider>();

            // Unit cone: base circle radius 0.5 at y = -0.5, apex at y = +0.5.
            Vector3[] vertices = new Vector3[sides * 2 + 2];
            Vector3 apex = new Vector3(0f, 0.5f, 0f);
            Vector3 baseCenter = new Vector3(0f, -0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float angle = (float)i / sides * Mathf.PI * 2f;
                Vector3 ring = new Vector3(Mathf.Cos(angle) * 0.5f, -0.5f, Mathf.Sin(angle) * 0.5f);
                vertices[i] = ring;              // side ring (shares apex fan below)
                vertices[sides + i] = ring;      // base ring (flat base normal)
            }
            vertices[sides * 2] = apex;
            vertices[sides * 2 + 1] = baseCenter;

            int[] triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                // Side triangle: apex, ring[i], ring[next].
                triangles[i * 3] = sides * 2;
                triangles[i * 3 + 1] = i;
                triangles[i * 3 + 2] = next;
                // Base triangle: center, base[next], base[i] (facing down).
                int b = sides * 3 + i * 3;
                triangles[b] = sides * 2 + 1;
                triangles[b + 1] = sides + next;
                triangles[b + 2] = sides + i;
            }

            Mesh mesh = new Mesh();
            mesh.name = "Cone";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            ((MeshCollider)cone.GetComponent<Collider>()).sharedMesh = mesh;
            return cone;
        }
    }
}
