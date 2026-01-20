using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    public static class GraphicsHelper
    {
        public delegate void DrawFunction(CommandBuffer cmd);

        public static void QuickDraw(RenderTexture rt, Matrix4x4 viewMatrix, Matrix4x4 projMatrix, DrawFunction function, bool clearColor = true, Color clrColor = default(Color), bool clearDepth = true, float depth = 1.0f)
        {
            var cmd = CommandBufferPool.Get("VoxelizeSlicing_Slice");
            cmd.SetRenderTarget(rt);
            cmd.SetViewport(new Rect(0, 0, rt.width, rt.height));
            cmd.ClearRenderTarget(clearDepth, clearColor, clrColor, depth);

            var gpuProjMatrix = GL.GetGPUProjectionMatrix(projMatrix, true);

            cmd.SetViewProjectionMatrices(viewMatrix, gpuProjMatrix);

            function(cmd);

            // important: reset matrices so we don't leak state
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
