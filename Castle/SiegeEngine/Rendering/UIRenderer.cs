using System;
using Silk.NET.GLFW;
using System.Numerics;
using System.Collections.Generic;
using Silk.NET.OpenGL;
using SiegeEngine.Rendering.Definitions;
using SiegeEngine.Interfaces;
using SiegeEngine.PlayerSystem;

namespace SiegeEngine.Rendering
{
    public unsafe class UIRenderer
    {
        private readonly IRenderContext _renderContext;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private readonly UIRenderingLayer _renderingLayer;
        private bool _firstRender = true;

        public UIRenderer(Glfw glfw, IRenderContext renderContext, WindowHandle* window)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _glfw = glfw;
            _window = window;
            _renderingLayer = new UIRenderingLayer(glfw, _renderContext, window);
        }

        public void Initialize(string backgroundPath, Dictionary<string, int> iconIndices)
        {
            _renderingLayer.Initialize(backgroundPath, iconIndices);
        }

        public void BeginRender()
        {
            _renderingLayer.BeginRender();
        }

        public void Render(List<object> elements, string positioningMode, PositionCalculator positionCalculator)
        {
            //Console.WriteLine("Entering Render");
            if (_firstRender)
            {
                Console.WriteLine("Rendering UI");
                _firstRender = false;
            }

            int windowWidth, windowHeight;
            _glfw.GetWindowSize(_window, out windowWidth, out windowHeight);

            _renderingLayer.BeginRender();

            foreach (var element in elements)
            {
                if (element is Label) continue;

                Vector2 adjustedPos = positionCalculator.CalculateAdjustedPosition(
                    element switch
                    {
                        Button btn => btn.Position,
                        Dropdown dropdown => dropdown.Position,
                        Toggle toggle => toggle.Position,
                        Label label => label.Position,
                        _ => Vector2.Zero
                    },
                    positioningMode, windowWidth, windowHeight);
                _renderingLayer.RenderElement(element, adjustedPos, windowWidth, windowHeight);
            }

            _renderingLayer.EndRender();
            //Console.WriteLine("Exiting Render");
        }

        public void RenderElement(object element, Vector2 adjustedPos, int windowWidth, int windowHeight)
        {
            _renderingLayer.RenderElement(element, adjustedPos, windowWidth, windowHeight);
        }

        public void EndRender()
        {
            _renderingLayer.EndRender();
        }

        public void Dispose()
        {
            _renderingLayer.Dispose();
        }
    }
}