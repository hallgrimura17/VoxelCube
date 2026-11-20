    // Inputs:
    // float3 WorldPos : World Position
    // float3 WorldNormal : World Normal
    // float Tile : Tiling factor
    // float Blend : Blending sharpness
    // Texture2DArray TexArray : Your Texture2DArray
    // SamplerState SamplerTexArray : Sampler for the Texture Array
    // float Index : Index of the texture in the array

void TriplanarTextureArray_float(float3 WorldPos, float3 WorldNormal, float Tile, float Blend, Texture2DArray TexArray, SamplerState SamplerTexArray, float Index, out float4 OutColor)
{
        // Calculate UVs for each projection plane
    float2 uvX = WorldPos.zy * Tile;
    float2 uvY = WorldPos.xz * Tile;
    float2 uvZ = WorldPos.xy * Tile;

        // Sample the texture array for each plane
    float4 sampleX = TexArray.Sample(SamplerTexArray, float3(uvX, Index));
    float4 sampleY = TexArray.Sample(SamplerTexArray, float3(uvY, Index));
    float4 sampleZ = TexArray.Sample(SamplerTexArray, float3(uvZ, Index));

        // Calculate blend weights based on world normal
    float3 blendWeights = abs(WorldNormal);
    blendWeights = pow(blendWeights, Blend);
    blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z); // Normalize weights

        // Blend the samples
    OutColor = sampleX * blendWeights.x + sampleY * blendWeights.y + sampleZ * blendWeights.z;
}