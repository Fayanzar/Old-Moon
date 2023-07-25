#include "SimplexNoise4D.hlsl"

float noise_musgrave_multi_fractal(float4 p, float H, float lacunarity, float octaves)
{
	float rmd;
	float value = 1.0;
	float pwr = 1.0;
	float pwHL = pow(lacunarity, -H);
	int i;

	for (i = 0; i < (int)octaves; i++) {
		value *= (pwr * snoise(p) + 1.0);
		pwr *= pwHL;
		p *= lacunarity;
	}

	rmd = octaves - floor(octaves);
	if (rmd != 0.0)
		value *= (rmd * pwr * snoise(p) + 1.0); /* correct? */

	return value;
}

void MusgraveFractal4D_float(float4 input, float H, float lacunarity, float octaves, out float Out)
{
    Out = noise_musgrave_multi_fractal(input, H, lacunarity, octaves);
}
