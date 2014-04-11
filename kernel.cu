
#pragma OPENCL EXTENSION cl_amd_printf : enable

typedef struct _CAMERA
{
	float3 position;
	float3 direction;
	float3 right;
	float3 top;
	float focalLength;
	float aspect;
} CAMERA;

typedef struct _RAY
{
	float3 position;
	float3 direction;
} RAY;

typedef struct _BOX
{
	float3 minimum;
	float3 maximum;
} BOX;

RAY getRay(global CAMERA *camera, float x, float y)
{
	RAY ray;
	ray.position = camera->position.xyz;
	ray.direction = 
		camera->direction * camera->focalLength + 
		camera->right * 400 * x * camera->aspect + 
		camera->top * 400 * y;
	ray.direction = normalize(ray.direction);
	return ray;
}

bool intersect_ray_box(RAY *ray, BOX *box)
{
	float dist = 0.0f;
	float maxValue = FLT_MAX;
	if (fabs(ray->direction.x) < 1E-06f)
	{
		if ((ray->position.x < box->minimum.x) || (ray->position.x > box->maximum.x))
		{
			return false;
		}
	}
	else
	{
		float num11 = 1.0f / ray->direction.x;
		float num8 = (box->minimum.x - ray->position.x) * num11;
		float num7 = (box->maximum.x - ray->position.x) * num11;
		if (num8 > num7)
		{
			float temp = num8;
			num8 = num7;
			num7 = temp;
		}
		dist = max(num8, dist);
		maxValue = min(num7, maxValue);
		if (dist > maxValue)
		{
			return false;
		}
	}
	if (fabs(ray->direction.y) < 1E-06f)
	{
		if ((ray->position.y < box->minimum.y) || (ray->position.y > box->maximum.y))
		{
			return false;
		}
	}
	else
	{
		float num10 = 1.0f / ray->direction.y;
		float num6 = (box->minimum.y - ray->position.y) * num10;
		float num5 = (box->maximum.y - ray->position.y) * num10;
		if (num6 > num5)
		{
			float temp = num6;
			num6 = num5;
			num5 = temp;
		}
		dist = max(num6, dist);
		maxValue = min(num5, maxValue);
		if (dist > maxValue)
		{
			return false;
		}
	}
	if (fabs(ray->direction.z) < 1E-06f)
	{
		if ((ray->position.z < box->minimum.z) || (ray->position.z > box->maximum.z))
		{
			return false;
		}
	}
	else
	{
		float num9 = 1.0f / ray->direction.z;
		float num4 = (box->minimum.z - ray->position.z) * num9;
		float num3 = (box->maximum.z - ray->position.z) * num9;
		if (num4 > num3)
		{
			float temp = num4;
			num4 = num3;
			num3 = temp;
		}
		dist = max(num4, dist);
		maxValue = min(num3, maxValue);
		if (dist > maxValue)
		{
			return false;
		}
	}
	return true;
}

__kernel void render(	global uchar4 *renderTarget, 
						int width, 
						int height, 
						global CAMERA *camera,
						global uchar *world,
						int sizeX,
						int sizeY,
						int sizeZ,
						uchar threshold)
{
	const int x = get_global_id(0);
	const int y = get_global_id(1);
	if(x >= width || y >= height)
		return;
	int id = x + width * y;
	
	const float pixx = 2.0f * ((float)x / width) - 1.0f;
	const float pixy = 2.0f * ((float)y / height) - 1.0f;
	
	BOX volume;
	RAY ray = getRay(camera, pixx, pixy);

	volume.minimum = (float3)(0);
	volume.maximum = (float3)(sizeX, sizeY, sizeZ);
	
	renderTarget[id] = (uchar4)(255, 128, 64, 255);
	if(!intersect_ray_box(&ray, &volume))
	{
		return;
	}
	
	float clip_far = 1600;
	float stepWidth = clip_far / 16000.0f;

	float3 start = ray.position;
	float3 step = sign(ray.direction);
	float3 boundary = start;
	boundary.x += step.x > 0;
	boundary.y += step.y > 0;
	boundary.z += step.z > 0;

	float3 tmax = (boundary - ray.position) / ray.direction;
	if(isnan(tmax.x)) tmax.x = FLT_MAX;
	if(isnan(tmax.y)) tmax.y = FLT_MAX;
	if(isnan(tmax.z)) tmax.z = FLT_MAX;

	float3 tdelta = step / ray.direction;
	if(isnan(tdelta.x)) tdelta.x = FLT_MAX;
	if(isnan(tdelta.y)) tdelta.y = FLT_MAX;
	if(isnan(tdelta.z)) tdelta.z = FLT_MAX;

	int px = (int)(start.x + 0.5f);
	int py = (int)(start.y + 0.5f);
	int pz = (int)(start.z + 0.5f);

	for(int i = 0; i < 750; i++)
	{
		if(px >= 0 && py >= 0 && pz >= 0 && px < sizeX && py < sizeY && pz < sizeZ)
		{
			int offset = sizeX * sizeY * pz + sizeX * py + px;
			if(world[offset] >= threshold)
			{
				renderTarget[id].x = world[offset];
				renderTarget[id].y =  world[offset];
				renderTarget[id].z =  world[offset];
				renderTarget[id].w = 255;
				return;
			}
		}
		if(tmax.x < tmax.y && tmax.x < tmax.z)
		{
			px += step.x;
			tmax.x += tdelta.x;
		}
		else if(tmax.y < tmax.z)
		{
			py += step.y;
			tmax.y += tdelta.y;
		}
		else
		{
			pz += step.z;
			tmax.z += tdelta.z;
		}
	}
}