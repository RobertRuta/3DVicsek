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

    }


    #region setup

    void Start()
    {
        // Find compute kernel
        m_updateParticlesKernel = ParticleCalculation.FindKernel("UpdateParticles");
        m_buildGridIndicesKernel = ParticleCalculation.FindKernel("UpdateGrid");

        // Create particle buffer
        m_particlesBuffer = new ComputeBuffer(numParticles, c_particleStride);
        // Create quad buffer
        m_quadPoints = new ComputeBuffer(6, c_quadStride);
        // Create index buffer
        m_indicesBuffer = new ComputeBuffer(numParticles, 32);


        // Initialise particles in memory and set initial positions and velocities
        Particle[] particles = new Particle[numParticles];        
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].position = new Vector3(Random.Range(0.0f, box.x), Random.Range(0.0f, box.y), Random.Range(0.0f, box.z));
            particles[i].velocity = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized * speed;
        }
            // Send this data to GPU buffer
        m_particlesBuffer.SetData(particles);

        InitParticles()


        // Initialise grid
        cellDim = radius;
            // Recalculate box dimensions
        box.x = Round(box.x / cellDim) * cellDim;
        box.y = Round(box.y / cellDim) * cellDim;
        box.z = Round(box.z / cellDim) * cellDim;
            // How many cells along x, y, z axes
        gridDim = new Vector3Int(box.x , box.y, box.z) * (1 / cellDim);


        // Initialise quadpoints buffer (individual particles)
        m_quadPoints.SetData(new[] {
            new Vector3(-0.25f, 0.25f),
            new Vector3(0.25f, 0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(0.25f, -0.25f),
            new Vector3(-0.25f, -0.25f),
            new Vector3(-0.25f, 0.25f),
        });


        // Set radius and cellDim variables in computer shader
        ParticleCalculation.SetFloat("radius", radius);
        ParticleCalculation.SetFloat("cellDim", Mathf.CeilToInt(radius));
    }

    #endregion


    #region Compute Update

    void Update()
    {
        ParticleCalculation.SetBuffer(m_updateParticlesKernel, "particles", m_particlesBuffer);
        ParticleCalculation.SetFloat("deltaTime", Time.deltaTime);
        ParticleCalculation.SetFloat("speed", speed);
        ParticleCalculation.SetFloats("box", new[] {box.x, box.y, box.z});

        int numGroups = Mathf.CeilToInt((float)numParticles / c_groupSize);
        ParticleCalculation.Dispatch(m_updateParticlesKernel, numGroups, 1, 1);

        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "cellparticleindices", m_indicesBuffer);
        ParticleCalculation.SetBuffer(m_buildGridIndicesKernel, "particles", m_particlesBuffer);
        ParticleCalculation.Dispatch(m_buildGridIndicesKernel, numGroups, 1, 1);

  
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
