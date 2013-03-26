VoxelizedRenderer
=================

Simple uniform grid voxel renderer with OpenCL, written in C#.

The project is badly commented atm, but i will add more comments
in the next view commits, also a better code structure and object orientation.

You can try this without the need of recompiling the project with starting
the exe file in bin\Release. You will see a gray screen (because you are in a volume),
so just press S for a while to see something and fly around.

Planned features for this voxel renderer are sparse voxel octree rendering
and support for DirectX or OpenGL Depth-/Backbuffers, so you can implement the
voxel renderer into your own game projects and benefit from both sides.

Current features are just a slow rendering process of 1000 voxels depth and a moveable
camera.

Controls:
WASD:		move
Arrow Keys: look around

Please report any bugs or error messages, so i can fix them.