using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleController : MonoBehaviour
{
    #region variables

    public ComputeShader ParticleCalculation;
    public Material ParticleMaterial;
    public int numParticles = 500000;
    public float speed = 4.0f;
    public float radius = 5.0f;
    public Vector3 box = new Vector3(1, 1, 1);

    private float cellDim;
    private Vector3Int gridDim;

    private const int c_groupSize = 128;
    private int m_updateParticlesKernel;
    private int m_buildGridIndicesKernel;

    #endregion


    #region Particle Struct

    struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 color;
    }    
        
    #endregion


    #region Buffers

    private ComputeBuffer m_particlesBuffer;
    private Particle[] temp_particles;
    private const int c_particleStride = 36;
    private ComputeBuffer m_quadPoints;
    private const int c_quadStride = 12;

    private ComputeBuffer m_indicesBuffer;
    private Vector2Int[] temp_indices;

    #endregion


    void InitParticles(Particle[] particles, ComputeBuffer pBuffer)
    {
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].position = new Vector3(Random.Range(0.0f, box.x), Random.Range(0.0f, box.y), Random.Range(0.0f, box.z));
            particles[i].velocity = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized * speed;
        }
            // Pack this data into buffer on CPU
        pBuffer.SetData(particles);
    }

    void RecalcBox()
    {
        // Recalculate box dimensions
        box.x = Mathf.Round(box.x / cellDim) * cellDim;
        box.y = Mathf.Round(box.y / cellDim) * cellDim;
        box.z = Mathf.Round(box.z / cellDim) * cellDim;
    }

    void CalcGrid()
    {
        RecalcBox();
        // How many cells along x, y, z axes
        gridDim = new Vector3Int((int) (box.x / cellDim) , (int) (box.y / cellDim), (int) (box.z / cellDim));
    }

    void InitQuads(ComputeBuffer quadBuffer)
    {
        // Initialise quadpoints buffer (individual particles)
        quadBuffer.SetData(new[] {
            new Vector3(-0.25f, 0.25f),
            new Vector3(0.25f, 0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(-0.25f, -0.25f),
            new Vector3(-0.25f, 0.25f),
        });
    }


    #region setup

    void Start()
    {
        // Find kernel and set kernel indices
        m_updateParticlesKernel = ParticleCalculation.FindKernel("UpdateParticles");
        m_buildGridIndicesKernel = ParticleCalculation.FindKernel("UpdateGrid");

        
        // Create buffers
            // Create particle buffer
        m_particlesBuffer = new ComputeBuffer(numParticles, c_particleStride);
            // Create quad buffer
        m_quadPoints = new ComputeBuffer(6, c_quadStride);
            // Create index buffer
        m_indicesBuffer = new ComputeBuffer(numParticles, 32);


        // Initialise particles in memory and set initial positions and velocities
        Particle[] particles = new Particle[numParticles];
        InitParticles(particles, m_particlesBuffer);


        // Initialise grid
        cellDim = radius;
        CalcGrid();


        // Initialise quad points
        InitQuads(m_quadPoints);


        // Set radius and cellDim variables in computer shader
        ParticleCalculation.SetFloat("radius", radius);
        ParticleCalculation.SetFloat("cellDim", Mathf.CeilToInt(radius));
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});
        
    }

    #endregion


    #region Compute Update

    void Update()
    {
        // Updated compute shader variables
        ParticleCalculation.SetFloat("deltaTime", Time.deltaTime);
        ParticleCalculation.SetFloat("speed", speed);
        
        // Recalculate grid on box rescale
        CalcGrid();
            // Send new box dimensions to GPU
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});

        // Prepare to run GPU code
        int numGroups = Mathf.CeilToInt((float)numParticles / c_groupSize);

        // Dispatch particle update code on GPU
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "particles", m_particlesBuffer);
            // Run code
        ParticleCalculation.Dispatch(m_updateParticlesKernel, numGroups, 1, 1);

        // Dispatch grid update code on GPU
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "cellparticleindices", m_indicesBuffer);
            // Update GPU buffer with CPU buffer ?? Other way round?
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "particles", m_particlesBuffer);
            // Run code
        ParticleCalculation.Dispatch(m_buildGridIndicesKernel, numGroups, 1, 1);


        #region debugging
        
        temp_indices = new Vector2Int[numParticles];
        m_indicesBuffer.GetData(temp_indices);

        int max = temp_indices[0].y;

        for (int i = 0; i < numParticles; i++)
        {
            //max = max < temp_indices[i].y ? temp_indices[i].y : max;
            if (max < temp_indices[i].y)
            {
                max = temp_indices[i].y;
            }
            
        }
        
        #endregion

            

        print("max: " + max);
    }

    #endregion


    #region Rendering
        void OnRenderObject()
        {
            ParticleMaterial.SetBuffer("particles", m_particlesBuffer);
            ParticleMaterial.SetBuffer("quadPoints", m_quadPoints);

            ParticleMaterial.SetPass(0);

            Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, numParticles);
        }
    #endregion


    #region Cleanup
    void OnDestroy()
    {
        m_particlesBuffer.Dispose();
        m_quadPoints.Dispose();
        m_indicesBuffer.Dispose();
    }
    #endregion
}
