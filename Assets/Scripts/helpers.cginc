
// ---------------------
// Define Data structure (must be same as your particle data)
// ---------------------
struct Data {
	float3 pos;
	float3 vel;
	float3 color;
};

cbuffer grid {
	float3 _GridDim;
	float _GridH;
};

StructuredBuffer  <uint2>	_GridIndicesBufferRead;
RWStructuredBuffer<uint2>	_GridIndicesBufferWrite;


// Calculate the 3D coordinates of a cell
uint3 CalcCellCoords(float3 pos, float3 box, float cellW)
{
    // max x, min y, min z corresponds to cell {0, 0, 0} of index 0
    uint3 cellPos = {(uint)(box.x - pos.x) / cellW, (box.y + pos.y) / cellW, (uint)(box.z + pos.z) / cellW};
    uint cID = ((cellPos.x * 2*box.x) + (cellPos.y * 4*box.x*box.z / cellW) + (cellPos.z)) / cellW ;

    return cID;
}


// Calculate cell integer id
uint CalcCellID(uint3 cellPos, float3 box, float cellW)
{
    uint cID = ((cellPos.x * 2*box.x) + (cellPos.y * 4*box.x*box.z / cellW) + (cellPos.z)) / cellW ;
    return cID;
}
// �Z����2�����C���f�b�N�X����1�����C���f�b�N�X��Ԃ�
uint GridKey(uint3 xyz) {
	return xyz.x + xyz.y * _GridDim.x + xyz.z * _GridDim.x * _GridDim.y;
}