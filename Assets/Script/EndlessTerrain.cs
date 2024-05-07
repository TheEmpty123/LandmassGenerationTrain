using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MapGenerator;

public class EndlessTerrain : MonoBehaviour
{

    const float viewerMoveThresholdForChunkUpdate = 25f;
    const float sqrViewerMoveThresholdForChunkUpdate = viewerMoveThresholdForChunkUpdate * viewerMoveThresholdForChunkUpdate;
    const float scale = 2f;

    public Transform viewer;
    public LODInfo[] detailLevel;
    public static float maxViewDst;

    public static Vector2 viewerPosition;
    Vector2 viewerOldPosition;
    int chunkSize;
    int chunkVisibleInViewDst;
    public Material material;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunkVisibleList = new List<TerrainChunk>();

    static MapGenerator mapGenerator;

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDst = detailLevel[detailLevel.Length - 1].visibleDstThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunkVisibleInViewDst = Mathf.RoundToInt(maxViewDst / chunkSize);

        UpdateVisibleChunks();
    }

    private void Update()
    {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        if( (viewerOldPosition - viewerPosition).sqrMagnitude > sqrViewerMoveThresholdForChunkUpdate)
        {
            viewerOldPosition = viewerPosition;
            UpdateVisibleChunks();
        }
    }



    void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunkVisibleList.Count; i++)
        {
            terrainChunkVisibleList[i].setVisible(false);
        }
        terrainChunkVisibleList.Clear();

        int currentViewerChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentViewerChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int offsetY = -chunkVisibleInViewDst; offsetY <= chunkVisibleInViewDst; offsetY++)
        {
            for (int offsetX = -chunkVisibleInViewDst; offsetX <= chunkVisibleInViewDst; offsetX++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentViewerChunkCoordX + offsetX, currentViewerChunkCoordY + offsetY);

                if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                {
                    //if the chunk already created, update it instead of create a new one
                    terrainChunkDictionary[viewedChunkCoord].updateVisiblePlane();
                }
                else
                {
                    terrainChunkDictionary.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevel, transform, material));
                }
            }
        }
    }

    public class TerrainChunk
    {
        GameObject meshObject;
        Vector2 position;
        Bounds bound;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        LODMesh collisionLODMesh;

        MapData mapData;
        bool mapDataReceived;

        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int chunkSize, LODInfo[] detailLevels, Transform parent, Material material)
        {
            this.detailLevels = detailLevels;

            position = coord * chunkSize;
            bound = new Bounds(position, Vector2.one * chunkSize);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshRenderer.material = material;

            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.SetParent(parent);
            meshObject.transform.localScale = Vector3.one * scale;
            setVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, updateVisiblePlane);
                if (detailLevels[i].useForCollider)
                {
                    collisionLODMesh = lodMeshes[i];
                }
            }
            mapGenerator.requestMapData(OnMapDataReceived, position);
        }

        void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture = TextureGenerator.TextureFromColourMap(mapData.colourMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            updateVisiblePlane();
        }

        public void updateVisiblePlane()
        {
            if (mapDataReceived)
            {
                float viewerDstFromNearestEdge = Mathf.Sqrt(bound.SqrDistance(viewerPosition));
                bool visible = viewerDstFromNearestEdge <= maxViewDst;

                if (visible)
                {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if (viewerDstFromNearestEdge > detailLevels[i].visibleDstThreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (lodIndex != previousLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            previousLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.requestMesh(mapData);
                        }
                    }

                    if(lodIndex == 0)
                    {
                        if (collisionLODMesh.hasMesh)
                        {
                            meshCollider.sharedMesh = collisionLODMesh.mesh;
                        }
                        else
                        {
                            if (!collisionLODMesh.hasRequestedMesh)
                            {
                                collisionLODMesh.requestMesh(mapData);
                            }
                        }
                    }

                    terrainChunkVisibleList.Add(this);
                }

                setVisible(visible);
            }
        }

        public void setVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool isVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        public void requestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.requestMeshData(onMeshDataReceived, mapData, lod);
        }

        void onMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }

    }

    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDstThreshold;
        public bool useForCollider;
    }

}
