using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    public static class GraphicsHelper
    {
        public delegate void DrawFunction(CommandBuffer cmd);

        public static void QuickDraw(RenderTexture rt, Matrix4x4 viewMatrix, Matrix4x4 projMatrix, 
                                     DrawFunction function, 
                                     bool clearColor = true, Color clrColor = default(Color), bool clearDepth = true, float depth = 1.0f,
                                     bool useGpuProjectionMatrix = false)
        {
            var cmd = CommandBufferPool.Get("VoxelizeSlicing_Slice");
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(new Rect(0, 0, rt.width, rt.height));
            cmd.ClearRenderTarget(clearDepth, clearColor, clrColor, depth);

            /* In normal circumstances, we would need to do something like this:  GL.GetGPUProjectionMatrix(projMatrix, false); and use that matrix
             * But for some reason on these command buffers, I can either use the original projMatrix or flip the Z of the GPU adjusted one. 
             * Not sure what would be more stable, but I'm leaving this code as reference.
             * My guess is that this is needed for render feature programming, etc, but when I have this kind of raw access, I don't need to do it because there's no overal structure to play nice with.*/
            Matrix4x4 gpuProjMatrix = projMatrix;

            if (useGpuProjectionMatrix)
            {
                // This is the “pipeline-friendly” path.
                gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, false);

                // If you ever need this again in a URP pass context, this might be needed because of reverse-Z stuff,
                //gpuProjMatrix.m20 *= -1; gpuProjMatrix.m21 *= -1; gpuProjMatrix.m22 *= -1; gpuProjMatrix.m23 *= -1;
            }

            cmd.SetViewProjectionMatrices(viewMatrix, gpuProjMatrix);

            function(cmd);

            // important: reset matrices so we don't leak state
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public static Matrix4x4 GetUnityCameraMatrix(Transform camTransform)
        {
            // Unity cameras use a Z flip for some ungodly reason, this forces it when computing camera matrices manually
            Matrix4x4 worldMatrix = camTransform.localToWorldMatrix;
            Matrix4x4 viewMatrix = worldMatrix.inverse;
            // Flip Z axis
            viewMatrix.m20 *= -1.0f;
            viewMatrix.m21 *= -1.0f;
            viewMatrix.m22 *= -1.0f;
            viewMatrix.m23 *= -1.0f;

            return viewMatrix;
        }
    }
}
