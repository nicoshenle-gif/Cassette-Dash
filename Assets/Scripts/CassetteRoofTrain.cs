using UnityEngine;

/// <summary>
/// Controls the roof railway and train.
/// When the player climbs onto the cassette roof and presses F near the train,
/// the player boards the train, which accelerates along the tracks and drives away into the distance!
/// </summary>
public class CassetteRoofTrain : MonoBehaviour
{
    [Header("Prefabs & Models")]
    public GameObject railwayModel;
    public GameObject trainModel;

    [Header("Positions & Parameters")]
    [Tooltip("Railway starts immediately to the right of the cassette roof hole (hole ends at X = 9.97)")]
    public float trackStartX = 10.30f;
    public float trainStartX = 12.00f;
    public float roofDeckY = 8.16f;
    public float trackZ = 0f;
    public float uniformScale = 2.0f;
    public int trackSegmentCount = 15;
    public float maxSpeed = 16f;
    public float acceleration = 6f;

    [Header("Runtime State")]
    public bool isPlayerNear = false;
    public bool isDeparting = false;
    public float currentSpeed = 0f;
    public float distanceTraveled = 0f;
    public CassettePlayer boardedPlayer;
    public CassettePlayer player => boardedPlayer;

    GameObject _trainGo;
    GameObject _railwayGo;
    BoxCollider _interactCol;
    Camera _cam;
    Vector3 _camStartOffset;
    float _smokeTimer;

    void Awake()
    {
        SetupComponents();
    }

    void SetupComponents()
    {
        _cam = Camera.main;

        // Interaction trigger around the train cabin
        _interactCol = GetComponent<BoxCollider>();
        if (!_interactCol) _interactCol = gameObject.AddComponent<BoxCollider>();
        _interactCol.isTrigger = true;
        _interactCol.center = new Vector3(0f, 0.5f, 0f);
        _interactCol.size = new Vector3(2.5f, 1.6f, 1.6f);

        // Ending cinematic + GAME OVER / STAGE CLEAR screen. Added to the runtime-built train root so
        // the scene and the level generator need no extra wiring.
        var ending = GetComponent<CassetteEndingSequence>();
        if (ending == null) ending = gameObject.AddComponent<CassetteEndingSequence>();
        ending.train = this;
    }

    public void Setup(Transform root, GameObject railPrefab, GameObject trainPrefab)
    {
        railwayModel = railPrefab;
        trainModel = trainPrefab;
        transform.SetParent(root, false);
        transform.localPosition = new Vector3(trainStartX, roofDeckY, trackZ);

        // Build or instantiate Railway
        BuildRailway(root);

        // Build or instantiate Train
        BuildTrain();
    }

    void BuildRailway(Transform root)
    {
        if (_railwayGo != null) SafeDestroy(_railwayGo);

        _railwayGo = new GameObject("Roof_Railway");
        _railwayGo.transform.SetParent(root, false);
        _railwayGo.transform.localPosition = new Vector3(trackStartX, roofDeckY, trackZ);

        float segLength = 1.177f * uniformScale; // ~4.12 units per segment

        if (railwayModel != null)
        {
            Material railMat = null;
#if UNITY_EDITOR
            railMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Railway_Material.mat");
#endif
            var urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            for (int i = 0; i < trackSegmentCount; i++)
            {
                var inst = Instantiate(railwayModel, _railwayGo.transform);
                inst.name = $"Railway_Seg_{i}";
                // Rotate 90 deg around Y so Z track runs along +X
                inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                inst.transform.localScale = Vector3.one * uniformScale;
                inst.transform.localPosition = new Vector3((i + 0.5f) * segLength, 0f, 0f);

                // Ensure materials
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (railMat != null)
                    {
                        r.sharedMaterial = railMat;
                    }
                    else if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                    {
                        var m = new Material(urpShader) { color = new Color(0.72f, 0.74f, 0.78f) };
                        m.SetFloat("_Smoothness", 0.6f);
                        r.sharedMaterial = m;
                    }
                }
            }
        }
        else
        {
            float totalLen = segLength * trackSegmentCount;
            BuildProceduralTrack(_railwayGo.transform, totalLen);
        }

        // Solid walking deck collider along the tracks
        float totalTrackLen = segLength * trackSegmentCount;
        var trackCol = _railwayGo.AddComponent<BoxCollider>();
        trackCol.center = new Vector3(totalTrackLen * 0.5f, 0.05f, 0f);
        trackCol.size = new Vector3(totalTrackLen, 0.16f, 2.0f);
    }

    void BuildProceduralTrack(Transform parent, float len)
    {
        float railGauge = 0.95f;
        float railRadius = 0.045f;
        // Two parallel steel rails
        CreateRailBeam(parent, "Rail_Back", new Vector3(len * 0.5f, 0.12f, railGauge * 0.5f), len, railRadius);
        CreateRailBeam(parent, "Rail_Front", new Vector3(len * 0.5f, 0.12f, -railGauge * 0.5f), len, railRadius);

        // Sleepers (wooden ties)
        int tieCount = Mathf.FloorToInt(len / 0.65f);
        for (int i = 0; i <= tieCount; i++)
        {
            float tieX = i * 0.65f;
            CreateSleeper(parent, $"Sleeper_{i}", new Vector3(tieX, 0.04f, 0f), 1.5f, 0.08f, 0.22f);
        }
    }

    static void SafeDestroy(Object obj)
    {
        if (!obj) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    void CreateRailBeam(Transform parent, string name, Vector3 localPos, float length, float r)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(length, r * 2f, r * 2f);
        var c = go.GetComponent<Collider>();
        if (c) SafeDestroy(c);
        ApplyMat(go, new Color(0.78f, 0.82f, 0.86f), 0.85f); // Steel
    }

    void CreateSleeper(Transform parent, string name, Vector3 localPos, float lenZ, float h, float wX)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(wX, h, lenZ);
        var c = go.GetComponent<Collider>();
        if (c) SafeDestroy(c);
        ApplyMat(go, new Color(0.32f, 0.20f, 0.12f), 0.25f); // Dark timber
    }

    void BuildTrain()
    {
        if (_trainGo != null) SafeDestroy(_trainGo);

        if (trainModel != null)
        {
            _trainGo = Instantiate(trainModel, transform);
            _trainGo.name = "Train_Model";
            _trainGo.transform.localPosition = Vector3.zero;
            // Rotated 180 deg around Y so engine/cowcatcher faces +X (along track to right)
            _trainGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _trainGo.transform.localScale = Vector3.one * uniformScale;

            Material trainMat = null;
#if UNITY_EDITOR
            trainMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Train_Material.mat");
#endif
            var urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            foreach (var r in _trainGo.GetComponentsInChildren<Renderer>())
            {
                if (trainMat != null)
                {
                    r.sharedMaterial = trainMat;
                }
                else if (r.sharedMaterial == null || r.sharedMaterial.shader == null || r.sharedMaterial.shader.name.Contains("Error"))
                {
                    var m = new Material(urpShader) { color = new Color(0.85f, 0.22f, 0.20f) };
                    m.SetFloat("_Smoothness", 0.6f);
                    r.sharedMaterial = m;
                }
            }
        }
        else
        {
            // Procedural stylized steam locomotive
            _trainGo = new GameObject("Procedural_Train");
            _trainGo.transform.SetParent(transform, false);
            _trainGo.transform.localPosition = Vector3.zero;
            BuildProceduralLocomotive(_trainGo.transform);
        }
    }

    void BuildProceduralLocomotive(Transform root)
    {
        // Boiler
        var boiler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        boiler.name = "Boiler";
        boiler.transform.SetParent(root, false);
        boiler.transform.localPosition = new Vector3(0.6f, 0.95f, 0f);
        boiler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        boiler.transform.localScale = new Vector3(1.1f, 1.6f, 1.1f);
        var cb = boiler.GetComponent<Collider>(); if (cb) SafeDestroy(cb);
        ApplyMat(boiler, new Color(0.18f, 0.22f, 0.28f), 0.7f); // Deep navy steel

        // Cab
        var cab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cab.name = "Cab";
        cab.transform.SetParent(root, false);
        cab.transform.localPosition = new Vector3(-1.3f, 1.25f, 0f);
        cab.transform.localScale = new Vector3(1.5f, 1.9f, 1.35f);
        var cc = cab.GetComponent<Collider>(); if (cc) SafeDestroy(cc);
        ApplyMat(cab, new Color(0.82f, 0.22f, 0.18f), 0.6f); // Red engine cab

        // Chimney / Smokestack
        var chimney = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        chimney.name = "Chimney";
        chimney.transform.SetParent(root, false);
        chimney.transform.localPosition = new Vector3(1.7f, 1.75f, 0f);
        chimney.transform.localScale = new Vector3(0.32f, 0.55f, 0.32f);
        var ch = chimney.GetComponent<Collider>(); if (ch) SafeDestroy(ch);
        ApplyMat(chimney, new Color(0.12f, 0.12f, 0.14f), 0.8f);

        // Cowcatcher / Pilot
        var pilot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pilot.name = "Pilot";
        pilot.transform.SetParent(root, false);
        pilot.transform.localPosition = new Vector3(2.35f, 0.32f, 0f);
        pilot.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        pilot.transform.localScale = new Vector3(0.55f, 0.45f, 1.3f);
        var cp = pilot.GetComponent<Collider>(); if (cp) SafeDestroy(cp);
        ApplyMat(pilot, new Color(0.85f, 0.72f, 0.20f), 0.5f);
    }

    static void FitToTargetSize(GameObject go, Vector3 targetSize)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        Vector3 cur = b.size;
        if (cur.x > 0.01f && cur.y > 0.01f && cur.z > 0.01f)
        {
            float sx = targetSize.x / cur.x;
            float sy = targetSize.y / cur.y;
            float sz = targetSize.z / cur.z;
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(sx, sy, sz));
        }
    }

    static void ApplyMat(GameObject go, Color col, float smooth)
    {
        var r = go.GetComponent<Renderer>();
        if (!r) return;
        var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(s) { color = col };
        m.SetFloat("_Smoothness", smooth);
        r.sharedMaterial = m;
    }

    public void StartDeparture(CassettePlayer p)
    {
        if (isDeparting) return;
        isDeparting = true;
        boardedPlayer = p;

        if (p != null)
        {
            p.BoardTrain(transform);
        }

        if (_cam != null)
        {
            _camStartOffset = _cam.transform.position - transform.position;
            var follow = _cam.GetComponent<CassetteCameraFollow>();
            if (follow) follow.target = transform;
        }
    }

    void Update()
    {
        if (!isDeparting) return;

        currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
        float ds = currentSpeed * Time.deltaTime;
        transform.position += Vector3.right * ds;
        distanceTraveled += ds;

        // Visual steam puff effect
        _smokeTimer += Time.deltaTime;
        if (_smokeTimer >= 0.12f)
        {
            _smokeTimer = 0f;
            SpawnSteamPuff();
        }

        // Camera follow assist
        if (_cam != null)
        {
            Vector3 targetCamPos = new Vector3(transform.position.x + _camStartOffset.x, _cam.transform.position.y, _cam.transform.position.z);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetCamPos, Time.deltaTime * 6f);
        }
    }

    void SpawnSteamPuff()
    {
        var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.name = "SteamPuff";
        puff.transform.position = transform.position + new Vector3(0.65f, 0.95f, 0f) + Random.insideUnitSphere * 0.08f;
        puff.transform.localScale = Vector3.one * Random.Range(0.15f, 0.28f);
        var c = puff.GetComponent<Collider>(); if (c) Destroy(c);
        var r = puff.GetComponent<Renderer>();
        if (r)
        {
            var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(s) { color = new Color(0.95f, 0.95f, 0.98f, 0.75f) };
            r.sharedMaterial = m;
        }
        var autoDestruct = puff.AddComponent<AutoPuffFade>();
        autoDestruct.driftVelocity = new Vector3(-currentSpeed * 0.35f, Random.Range(1.2f, 2.2f), Random.Range(-0.3f, 0.3f));
    }

    void OnTriggerEnter(Collider other)
    {
        var p = other.GetComponent<CassettePlayer>() ?? other.GetComponentInParent<CassettePlayer>();
        if (p)
        {
            isPlayerNear = true;
            p.currentNearbyTrain = this;
        }
    }

    void OnTriggerExit(Collider other)
    {
        var p = other.GetComponent<CassettePlayer>() ?? other.GetComponentInParent<CassettePlayer>();
        if (p && p.currentNearbyTrain == this)
        {
            isPlayerNear = false;
            p.currentNearbyTrain = null;
        }
    }
}

public class AutoPuffFade : MonoBehaviour
{
    public Vector3 driftVelocity;
    float _age;

    void Update()
    {
        _age += Time.deltaTime;
        transform.position += driftVelocity * Time.deltaTime;
        transform.localScale += Vector3.one * (0.65f * Time.deltaTime);
        if (_age >= 0.85f) Destroy(gameObject);
    }
}
