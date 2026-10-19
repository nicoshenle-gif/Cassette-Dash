using UnityEngine;

/// <summary>
/// Crossbow bolt fired by CassetteCrossbowTurret.
/// Flies in 3D towards the predicted player position and deals damage on hit.
/// </summary>
public class CassetteCrossbowBolt : MonoBehaviour
{
    public CassettePlayer player;
    public float damage = 20f;
    public Vector3 velocity;
    public float maxLife = 5f;

    float _life;

    static Material _woodMat;
    static Material _metalMat;
    static Material _fletchMat;

    public static void Spawn(Vector3 position, Vector3 direction, float speed, float damage, CassettePlayer owner)
    {
        EnsureMaterials();
        Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.right;

        var go = new GameObject("CrossbowBolt");
        go.transform.position = position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);
        BuildVisual(go.transform);

        var bolt = go.AddComponent<CassetteCrossbowBolt>();
        bolt.player = owner;
        bolt.damage = Mathf.Max(1f, damage);
        bolt.velocity = dir * Mathf.Max(1f, speed);
    }

    static void EnsureMaterials()
    {
        if (_woodMat != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        _woodMat = new Material(shader) { color = new Color(0.45f, 0.30f, 0.14f) };
        if (_woodMat.HasProperty("_BaseColor")) _woodMat.SetColor("_BaseColor", _woodMat.color);

        _metalMat = new Material(shader) { color = new Color(0.76f, 0.79f, 0.84f) };
        if (_metalMat.HasProperty("_BaseColor")) _metalMat.SetColor("_BaseColor", _metalMat.color);

        _fletchMat = new Material(shader) { color = new Color(0.95f, 0.42f, 0.12f) };
        if (_fletchMat.HasProperty("_BaseColor")) _fletchMat.SetColor("_BaseColor", _fletchMat.color);
    }

    static GameObject Part(Transform parent, PrimitiveType type, string name,
        Vector3 localPos, Vector3 scale, Quaternion localRot, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.transform.localRotation = localRot;
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        return go;
    }

    static void BuildVisual(Transform root)
    {
        // Bolt is modelled along +X (shaft, steel tip, orange fletching at the tail).
        Part(root, PrimitiveType.Cylinder, "Shaft",
            new Vector3(0.02f, 0f, 0f), new Vector3(0.032f, 0.40f, 0.032f), Quaternion.Euler(0f, 0f, 90f), _woodMat);
        Part(root, PrimitiveType.Cube, "Tip",
            new Vector3(0.46f, 0f, 0f), new Vector3(0.16f, 0.055f, 0.055f), Quaternion.identity, _metalMat);
        Part(root, PrimitiveType.Cube, "FletchUp",
            new Vector3(-0.28f, 0.055f, 0f), new Vector3(0.20f, 0.018f, 0.085f), Quaternion.identity, _fletchMat);
        Part(root, PrimitiveType.Cube, "FletchDown",
            new Vector3(-0.28f, -0.055f, 0f), new Vector3(0.20f, 0.018f, 0.085f), Quaternion.identity, _fletchMat);
    }

    void FixedUpdate()
    {
        if (player == null) player = FindAnyObjectByType<CassettePlayer>();

        _life += Time.fixedDeltaTime;
        if (_life >= maxLife)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += velocity * Time.fixedDeltaTime;
        if (velocity.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.FromToRotation(Vector3.right, velocity.normalized);

        // ---- Hit the player? (capsule based, works while standing/crouching) ----
        if (player != null && !player.isDead)
        {
            Vector3 pCenter = player.transform.position;
            float dx = Mathf.Abs(pCenter.x - transform.position.x);
            float dz = Mathf.Abs(pCenter.z - transform.position.z);
            float dy = Mathf.Abs(pCenter.y - transform.position.y);
            if (dx <= player.CapsuleRadius + 0.16f &&
                dz <= player.CapsuleRadius + 0.26f &&
                dy <= player.CapsuleHalfHeight + 0.16f)
            {
                player.TakeDamage(damage);
                SpawnSpark();
                Destroy(gameObject);
                return;
            }
        }

        // ---- Hit the cassette shell / floor? ----
        Vector3 p = transform.position;
        if (p.y < -7.45f || p.y > 8.30f || Mathf.Abs(p.x) > 18.65f)
        {
            SpawnSpark();
            Destroy(gameObject);
        }
    }

    void SpawnSpark()
    {
        EnsureMaterials();
        var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "BoltSpark";
        var col = spark.GetComponent<Collider>();
        if (col != null) Destroy(col);
        spark.transform.position = transform.position;
        spark.transform.localScale = Vector3.one * 0.09f;
        var r = spark.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = _fletchMat;
        Destroy(spark, 0.12f);
    }
}
