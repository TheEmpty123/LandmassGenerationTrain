using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;

public class MapGenerator : MonoBehaviour
{

    public enum DrawMode { NoiseMap, ColourMap, Mesh}

    public DrawMode drawMode;

    const int mapChunkSize = 241;
    [Range(0, 6)]
    public int levelOfDetail;
    public float noiseScale;

    public int octaves;
    [Range(0f, 1f)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;
    public float meshHeightMultiplier;
    public AnimationCurve animationCurve;

    public bool autoUpdate;

    public TerrainType[] regions;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfosQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfosQueue = new Queue<MapThreadInfo<MeshData>>();
    public void requestMapData(Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            mapDataThread(callback);
        };

        new Thread(threadStart).Start();
    }

    public void requestMeshData(Action<MeshData> callback, MapData mapData)
    {
        ThreadStart threadStart = delegate
        {
            meshDataThread(callback, mapData);
        };

        new Thread(threadStart).Start();
    }

    void mapDataThread(Action<MapData> callback)
    {
        MapData mapData = GenerateMapData();
        lock(mapDataThreadInfosQueue){
            mapDataThreadInfosQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    void meshDataThread(Action<MeshData> callback, MapData mapData)
    {
        MeshData meshData = MeshGenerator.GererateTerrainMap(mapData.heightMap, meshHeightMultiplier, animationCurve, levelOfDetail);
        lock (meshDataThreadInfosQueue)
        {
            meshDataThreadInfosQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    public void drawMapInEditor()
    {
        MapData mapData = GenerateMapData();
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTextureMap(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.ColourMap)
        {
            display.DrawTextureMap(TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GererateTerrainMap(mapData.heightMap, meshHeightMultiplier, animationCurve, levelOfDetail), TextureGenerator.TextureFromColourMap(mapData.colourMap, mapChunkSize, mapChunkSize));
        }
    }

    private void Update()
    {
        if(mapDataThreadInfosQueue.Count > 0)
        {
            for(int i = 0; i < mapDataThreadInfosQueue.Count; i++)
            {
                MapThreadInfo<MapData> mapThreadInfo = mapDataThreadInfosQueue.Dequeue();
                mapThreadInfo.callback(mapThreadInfo.parameter);
            }
        }

        if (meshDataThreadInfosQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfosQueue.Count; i++)
            {
                MapThreadInfo<MeshData> meshThreadInfo = meshDataThreadInfosQueue.Dequeue();
                meshThreadInfo.callback(meshThreadInfo.parameter);
            }
        }
    }

    MapData GenerateMapData()
    {
        float[,] mapNoise = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, offset);

        Color[] colourMap = new Color[mapChunkSize * mapChunkSize];
        for(int y = 0; y < mapChunkSize; y++)
        {
            for(int x = 0; x < mapChunkSize; x++)
            {
                float currentHeight = mapNoise[x, y];
                for(int i = 0; i < regions.Length; i++)
                {
                    if(currentHeight <= regions[i].height)
                    {
                        colourMap[y * mapChunkSize + x] = regions[i].color;
                        break;
                    }
                }
            }
        }
        
        return new MapData(mapNoise, colourMap);
    }

    private void OnValidate()
    {
        if(lacunarity < 1) lacunarity = 1;
        if(octaves < 0) octaves = 0;
    }

    struct MapThreadInfo<T>
    {
        public Action<T> callback;
        public T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

public struct MapData
{
    public float[,] heightMap;
    public Color[] colourMap;

    public MapData(float[,] heightMap, Color[] colourMap)
    {
        this.heightMap = heightMap;
        this.colourMap = colourMap;
    }
}