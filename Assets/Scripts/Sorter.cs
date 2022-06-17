using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BufferSorter;
using MergeSort;

public class Sorter : MonoBehaviour
{
    #region variables

    public ComputeShader ParticleCalculation;
    public ComputeShader SortShader;
    private int m_buildGridIndicesKernel;
    private int m_mainKernel;
    private int m_sortKernel;
    

    #endregion


    #region Buffers

    private ComputeBuffer m_particlesBuffer;
    //private Particle[] temp_particles;
    private DisposableBuffer<uint> m_values;
    private DisposableBuffer<uint> m_keys;

    #endregion


    #region setup

    void Start()
    {
        BitonicMergeSort sorter;
        var count = 1 << 10;

        sorter = new BitonicMergeSort(SortShader);
        m_values = new DisposableBuffer<uint>(count);
        m_keys = new DisposableBuffer<uint>(count);

        ParticleCalculation.SetBuffer(m_mainKernel, "cellIDs", m_values);

        sorter.Init(m_keys);
        sorter.Sort(m_keys, m_values);

        m_keys.Download();
        
        for (int i = 0; i < 10; i++)
        {
            Debug.Log(m_keys.Data[i], m_values.Data[m_keys.Data[i]]);
        }
    }

    #endregion


    #region ComputeUpdate

    void Update()
    {
    }

    #endregion
   
    void OnDestroy() 
    {
        if (_sort != null)
            _sort.Dispose();

        if (_keys != null)
            _keys.Dispose();

        if (_values != null)
            _values.Dispose();
    }

}
