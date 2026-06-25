using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drop-in replacement for TrailRibbon (the geometry-shader version).
/// Identical CPU-side ring buffer; only the draw call changes:
///   GS version:  DrawProcedural(LineStrip,  count vertices)
///   This version: DrawProcedural(Triangles, (count-1)*6 vertices)
/// The vertex shader unpacks segment index and corner from SV_VertexID
/// and looks up positions in the StructuredBuffer itself.
/// </summary>
public class Trail : MonoBehaviour
{
    [Header("Trail settings")]
    public int   capacity        = 256;
    public float pixelWidth      = 3f;
    public float fadeTime        = 8f;
    public Material ribbonMaterial; // assign TrailRibbonVertex.shader material

    protected GraphicsBuffer   pointBuffer;
    protected Vector4[]        cpuPoints;
    protected Vector3Double[]  trailPoints;

    public Vector4[] CpuPoints => cpuPoints;
    public Vector3Double[] TrailPoints => trailPoints;

    protected int     head      = 0;
    protected int     count     = 0;

    public int Head => head;
    public int Tail => (head - count + capacity) % capacity;
    public int Count => count;

    protected Camera cam;
    protected MainCamera mainCamera;

    private MaterialPropertyBlock propBlock;

    public Body Body
    {
        get; set;
    }

    void Awake()
    {
        cpuPoints   = new Vector4[capacity];
        trailPoints = new Vector3Double[capacity];
        pointBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(float) * 4);
        cam         = Camera.main;
        mainCamera  = FindObjectOfType<MainCamera>();
        propBlock   = new MaterialPropertyBlock();
    }

    void OnDestroy() => pointBuffer?.Release();

    protected virtual void Update()
    {
        if (Body != null)
        {
            Sample();
            AgePoints();
            Draw();
        }
    }

    protected virtual void Sample()
    {
        if (Body != null)
        {
            var pos = Body.position;
            trailPoints[head] = pos;

            var centeredPos = mainCamera.centeredBody.position;
            var samplePos = (Vector3)((pos - centeredPos) * mainCamera.scale);
            cpuPoints[head] = new Vector4(samplePos.x, samplePos.y, samplePos.z, 0f);

            head  = (head + 1) % capacity;
            count = Mathf.Min(count + 1, capacity);
        }
    }

    virtual protected void AgePoints()
    {
        var centeredPos = mainCamera.centeredBody.position;
        float dt = Time.deltaTime;
        for (int i = 0; i < count; i++)
        {
            int idx = (head - 1 - i + capacity) % capacity;
            var correctedPos = (Vector3)((trailPoints[idx] - centeredPos) * mainCamera.scale);
            cpuPoints[idx] = new Vector4(correctedPos.x, correctedPos.y, correctedPos.z, cpuPoints[idx].w + dt);
        }
    }

    protected void Draw()
    {
        if (ribbonMaterial == null || count < 2) return;
        pointBuffer.SetData(cpuPoints);

        propBlock.Clear();
        propBlock.SetBuffer("_Points",     pointBuffer);
        propBlock.SetInt   ("_Capacity",   capacity);
        propBlock.SetInt   ("_Head",       head);
        propBlock.SetInt   ("_Count",      count);
        propBlock.SetFloat ("_PixelWidth", pixelWidth);
        propBlock.SetFloat ("_FadeTime",   fadeTime);

        int vertexCount = (count - 1) * 6;

        Graphics.DrawProcedural(
            ribbonMaterial,
            new Bounds(transform.position, Vector3.one * 1e6f),
            MeshTopology.Triangles,
            vertexCount,
            1,
            null,
            propBlock,
            ShadowCastingMode.Off,
            false,
            gameObject.layer
        );
    }

    public override string ToString()
    {
        string s = "";
        for (int i = 0; i < count; i++)
        {
            s += cpuPoints[(head - 1 - i + capacity) % capacity];
            s += ", ";
        }
        return s;
    }
}
