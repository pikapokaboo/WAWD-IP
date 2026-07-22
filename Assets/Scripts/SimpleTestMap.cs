using UnityEngine;

[ExecuteAlways]
public sealed class SimpleTestMap : MonoBehaviour
{
    private const int CurrentLayoutVersion = 2;
    private const string GeneratedRootName = "Generated Geometry";

    [SerializeField] private Color groundColor = new(0.28f, 0.31f, 0.34f);
    [SerializeField] private Color obstacleColor = new(0.18f, 0.48f, 0.68f);
    [SerializeField] private Color accentColor = new(0.9f, 0.55f, 0.16f);
    [SerializeField, HideInInspector] private int generatedLayoutVersion;

    private Transform generatedRoot;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        // The component replaced a plane that was scaled to 10 in the original scene.
        transform.localScale = Vector3.one;
        generatedRoot = transform.Find(GeneratedRootName);
        if (generatedRoot == null || generatedLayoutVersion != CurrentLayoutVersion)
            Rebuild();
    }

    [ContextMenu("Rebuild Test Map")]
    public void Rebuild()
    {
        Clear();

        GameObject root = new(GeneratedRootName);
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
        generatedLayoutVersion = CurrentLayoutVersion;

        // Arena shell and a clear spawn area around the origin.
        AddBlock("Ground", new Vector3(0f, -0.5f, 6f), new Vector3(40f, 1f, 40f), groundColor);
        AddBlock("North Wall", new Vector3(0f, 1f, 26f), new Vector3(40f, 2f, 0.5f), groundColor);
        AddBlock("South Wall", new Vector3(0f, 1f, -14f), new Vector3(40f, 2f, 0.5f), groundColor);
        AddBlock("East Wall", new Vector3(20f, 1f, 6f), new Vector3(0.5f, 2f, 40f), groundColor);
        AddBlock("West Wall", new Vector3(-20f, 1f, 6f), new Vector3(0.5f, 2f, 40f), groundColor);

        // Sparse cover leaves broad lanes from the player spawn.
        AddBlock("Low Cover", new Vector3(0f, 0.6f, 8f), new Vector3(5f, 1.2f, 0.8f), obstacleColor);
        AddBlock("Cover Left", new Vector3(-7f, 1f, 4f), new Vector3(2f, 2f, 2f), obstacleColor);
        AddBlock("Cover Right", new Vector3(7f, 1f, 5f), new Vector3(2f, 2f, 2f), obstacleColor);

        // Staircase leading to a raised platform.
        for (int i = 0; i < 5; i++)
        {
            float height = 0.35f * (i + 1);
            AddBlock($"Step {i + 1}", new Vector3(-11f, height * 0.5f, 9f + i),
                new Vector3(4f, height, 1f), accentColor);
        }

        AddBlock("Stair Platform", new Vector3(-11f, 1.75f, 16f), new Vector3(6f, 0.5f, 5f), obstacleColor);

        // Ramp and landing opposite the stairs.
        AddBlock("Ramp", new Vector3(11f, 1.1f, 11f), new Vector3(4f, 0.4f, 8f),
            accentColor, Quaternion.Euler(-14f, 0f, 0f));
        AddBlock("Ramp Landing", new Vector3(11f, 2f, 17f), new Vector3(6f, 0.5f, 4f), obstacleColor);

        // Small jump course along the right side.
        AddBlock("Jump Block 1", new Vector3(14f, 0.4f, -5f), new Vector3(2.5f, 0.8f, 2.5f), accentColor);
        AddBlock("Jump Block 2", new Vector3(14f, 0.7f, 0f), new Vector3(2.5f, 1.4f, 2.5f), accentColor);
        AddBlock("Jump Block 3", new Vector3(14f, 1f, 5f), new Vector3(2.5f, 2f, 2.5f), accentColor);

        // A narrow corridor for collision and cornering tests.
        AddBlock("Corridor Left", new Vector3(-5f, 1f, -7f), new Vector3(0.5f, 2f, 8f), obstacleColor);
        AddBlock("Corridor Right", new Vector3(-1.5f, 1f, -7f), new Vector3(0.5f, 2f, 8f), obstacleColor);
    }

    private void AddBlock(string blockName, Vector3 position, Vector3 scale, Color color,
        Quaternion rotation = default)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = blockName;
        block.transform.SetParent(generatedRoot, false);
        block.transform.SetLocalPositionAndRotation(position,
            rotation == default ? Quaternion.identity : rotation);
        block.transform.localScale = scale;

        propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        block.GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }

    private void Clear()
    {
        if (generatedRoot == null)
            generatedRoot = transform.Find(GeneratedRootName);

        if (generatedRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedRoot.gameObject);
        else
            DestroyImmediate(generatedRoot.gameObject);

        generatedRoot = null;
    }
}
