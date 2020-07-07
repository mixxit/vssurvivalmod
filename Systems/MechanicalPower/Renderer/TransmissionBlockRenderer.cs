﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class TransmissionBlockRenderer : MechBlockRenderer
    {
        CustomMeshDataPartFloat matrixAndLightFloats1;
        CustomMeshDataPartFloat matrixAndLightFloats2;
        MeshRef blockMeshRef1;
        MeshRef blockMeshRef2;

        public TransmissionBlockRenderer(ICoreClientAPI capi, MechanicalPowerMod mechanicalPowerMod, Block textureSoureBlock, CompositeShape shapeLoc) : base(capi, mechanicalPowerMod)
        {
            MeshData blockMesh1;
            MeshData blockMesh2 = null;

            AssetLocation loc = shapeLoc.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");

            Shape shape = capi.Assets.TryGet(loc).ToObject<Shape>();
            Vec3f rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);

            capi.Tesselator.TesselateShape(textureSoureBlock, shape, out blockMesh1, rot);

            CompositeShape ovShapeCmp = new CompositeShape() { Base = new AssetLocation("shapes/block/wood/mechanics/transmission-rightgear.json") };
            //rot = new Vec3f(ovShapeCmp.rotateX, ovShapeCmp.rotateY, ovShapeCmp.rotateZ);
            rot = new Vec3f(shapeLoc.rotateX, shapeLoc.rotateY, shapeLoc.rotateZ);
            Shape ovshape = capi.Assets.TryGet(ovShapeCmp.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json")).ToObject<Shape>();
            capi.Tesselator.TesselateShape(textureSoureBlock, ovshape, out blockMesh2, rot);

            //blockMesh1.Rgba2 = null;
            //blockMesh2.Rgba2 = null;

            // 16 floats matrix, 4 floats light rgbs
            blockMesh1.CustomFloats = matrixAndLightFloats1 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh1.CustomFloats.SetAllocationSize((16 + 4) * 10100);
            blockMesh2.CustomFloats = matrixAndLightFloats2 = new CustomMeshDataPartFloat((16 + 4) * 10100)
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 16, 32, 48, 64 },
                InterleaveSizes = new int[] { 4, 4, 4, 4, 4 },
                InterleaveStride = 16 + 4 * 16,
                StaticDraw = false,
            };
            blockMesh2.CustomFloats.SetAllocationSize((16 + 4) * 10100);

            this.blockMeshRef1 = capi.Render.UploadMesh(blockMesh1);
            this.blockMeshRef2 = capi.Render.UploadMesh(blockMesh2);
        }


        protected override void UpdateLightAndTransformMatrix(int index, Vec3f distToCamera, float rotation, IMechanicalPowerRenderable dev)
        {
            BEBehaviorMPTransmission trans = dev as BEBehaviorMPTransmission;
            if (trans == null) return;
            float rot1 = trans.RotationNeighbour(1, true);
            float rot2 = trans.RotationNeighbour(0, true);

            float rotX = rot1 * dev.AxisSign[0];
            float rotY = rot1 * dev.AxisSign[1];
            float rotZ = rot1 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats1.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);

            rotX = rot2 * dev.AxisSign[0];
            rotY = rot2 * dev.AxisSign[1];
            rotZ = rot2 * dev.AxisSign[2];
            UpdateLightAndTransformMatrix(matrixAndLightFloats2.Values, index, distToCamera, dev.LightRgba, rotX, rotY, rotZ);
        }

        public override void OnRenderFrame(float deltaTime, IShaderProgram prog)
        {
            UpdateCustomFloatBuffer();

            if (quantityBlocks > 0)
            {
                matrixAndLightFloats1.Count = quantityBlocks * 20;
                updateMesh.CustomFloats = matrixAndLightFloats1;
                capi.Render.UpdateMesh(blockMeshRef1, updateMesh);
                capi.Render.RenderMeshInstanced(blockMeshRef1, quantityBlocks);
                matrixAndLightFloats2.Count = quantityBlocks * 20;
                updateMesh.CustomFloats = matrixAndLightFloats2;
                capi.Render.UpdateMesh(blockMeshRef2, updateMesh);
                capi.Render.RenderMeshInstanced(blockMeshRef2, quantityBlocks);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            blockMeshRef1?.Dispose();
            blockMeshRef2?.Dispose();
        }
    }
}
