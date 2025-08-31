using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Silk.NET.GLFW;
using System.Drawing;
using Silk.NET.OpenGL;
using SiegeEngine.Events;
using SiegeEngine.Rendering.Definitions;
using SiegeEngine.Networking;
using SiegeEngine.PlayerSystem;
using SiegeEngine.Interfaces;
using SiegeEngine.Rendering;
using SiegeEngine.Scenes;

namespace SiegeEngine.Systems
{
    public unsafe class HtmlUiSystem : GameSystem
    {
        private readonly IRenderContext _renderContext;
        private readonly Glfw _glfw;
        private readonly WindowHandle* _window;
        private readonly ShaderProgram _shader;
        private readonly VertexBuffer _uiBuffer;
        private readonly EditorTextRenderer _textRenderer;
        private readonly Dictionary<string, Action> _actions = new Dictionary<string, Action>();
        private readonly List<object> _elements = new List<object>();
        private string _htmlPath;
        private string _cssPath;
        private readonly ActionConfig _actionConfig;
        private string _selectedBrush = "Wall";
        public bool _gridSnapState = false;
        private readonly InputHandler _inputHandler;
        private readonly EventBus _eventBus;
        private object _hoveredElementOnMouseDown;
        private readonly EditorScene _editorScene;
        private readonly SteamEngine _steamEngine;
        private readonly string _callbackId;

        public HtmlUiSystem(IRenderContext renderContext, Glfw glfw, WindowHandle* window, IGameServer server, string htmlPath, string cssPath, ActionConfig actionConfig, InputHandler inputHandler, EditorScene editorScene, EventBus eventBus, SteamEngine steamEngine = null)
            : base(server)
        {
            _renderContext = renderContext ?? throw new ArgumentNullException(nameof(renderContext));
            _glfw = glfw;
            _window = window;
            _htmlPath = htmlPath;
            _cssPath = cssPath;
            _actionConfig = actionConfig;
            _inputHandler = inputHandler;
            _editorScene = editorScene;
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _steamEngine = steamEngine;
            _callbackId = $"HtmlUiSystem_{Guid.NewGuid()}";

            string vertexShader = @"
                #version 330 core
                layout(location = 0) in vec2 aPosition;
                layout(location = 1) in vec4 aColor;
                layout(location = 2) in vec2 aTexCoord;
                out vec4 vColor;
                out vec2 vTexCoord;
                uniform mat4 uProjection;
                uniform mat4 uTransform;
                void main() {
                    gl_Position = uProjection * uTransform * vec4(aPosition, 0.0, 1.0);
                    vColor = aColor;
                    vTexCoord = aTexCoord;
                }";
            string fragmentShader = @"
                #version 330 core
                in vec4 vColor;
                in vec2 vTexCoord;
                out vec4 FragColor;
                uniform float uUseTexture;
                uniform vec4 uColor;
                uniform sampler2D uTexture;
                void main() {
                    if (uUseTexture > 0.5) {
                        FragColor = texture(uTexture, vTexCoord) * uColor;
                    } else {
                        FragColor = vColor;
                    }
                }";
            _shader = new ShaderProgram(_renderContext, vertexShader, fragmentShader);
            _uiBuffer = new VertexBuffer(_renderContext);
            _textRenderer = new EditorTextRenderer(_glfw, _renderContext, _window);
            _textRenderer.Initialize(_shader);

            LoadUi();
            _inputHandler.SetMouseCallback(_callbackId, OnMouseButton);
            _eventBus.Subscribe<MouseInputEvent>(OnNetworkMouseButton);
            _eventBus.Subscribe<ToggleGridSnapEvent>(OnToggleGridSnap);
            Console.WriteLine($"HtmlUiSystem: Registered mouse callback with InputHandler, ID: {_callbackId}");
        }

        private void OnMouseButton(MouseButton button, InputAction action)
        {
            _glfw.GetCursorPos(_window, out double mx, out double my);
            Vector2 mousePos = new Vector2((float)mx, (float)my);
            Console.WriteLine($"HtmlUiSystem: Local mouse event - Button: {button}, Action: {action}, Pos: {mousePos}, Callback ID: {_callbackId}");
            HandleMouseInput(new MouseInputEvent(mousePos, button, action, _steamEngine?.GetSteamId() ?? 0));
        }

        private void OnNetworkMouseButton(MouseInputEvent e)
        {
            Console.WriteLine($"HtmlUiSystem: Networked mouse event - SteamID: {e.SteamId}, Pos: {e.Position}, Button: {e.Button}, Action: {e.Action}");
            if (e.SteamId == (_steamEngine?.GetSteamId() ?? 0))
            {
                HandleMouseInput(e);
            }
        }

        private void OnToggleGridSnap(ToggleGridSnapEvent e)
        {
            if (e.PlayerId != (_steamEngine?.GetSteamId() ?? 0))
            {
                _gridSnapState = e.State;
                _editorScene?.ToggleGridSnap(e.State);
                foreach (var element in _elements)
                {
                    if (element is Toggle toggle && toggle.Name == "grid-snap")
                    {
                        if (toggle.State != e.State)
                            toggle.ToggleState();
                        break;
                    }
                }
                Console.WriteLine($"HtmlUiSystem: Received networked ToggleGridSnapEvent, State: {e.State}, PlayerId: {e.PlayerId}");
            }
        }

        private void HandleMouseInput(MouseInputEvent e)
        {
            Vector2 mousePos = e.Position;
            MouseButton button = e.Button;
            InputAction action = e.Action;
            bool isMouseCaptured = _editorScene?.IsMouseCaptured ?? false;

            bool isOverUi = false;
            foreach (var element in _elements)
            {
                if (element is UiElement uiElement)
                {
                    if (uiElement.Bounds.Contains((int)mousePos.X, (int)mousePos.Y))
                    {
                        isOverUi = true;
                        Console.WriteLine($"HtmlUiSystem: Mouse over UiElement {uiElement.Id} at Bounds: {uiElement.Bounds}");
                        break;
                    }
                }
                else if (element is Toggle toggle)
                {
                    if (mousePos.X >= toggle.Position.X && mousePos.X <= toggle.Position.X + toggle.Size.X &&
                        mousePos.Y >= toggle.Position.Y && mousePos.Y <= toggle.Position.Y + toggle.Size.Y)
                    {
                        isOverUi = true;
                        Console.WriteLine($"HtmlUiSystem: Mouse over Toggle {toggle.Name} at Pos: ({toggle.Position.X}, {toggle.Position.Y}), Size: ({toggle.Size.X}, {toggle.Size.Y})");
                        break;
                    }
                }
                else if (element is Dropdown dropdown)
                {
                    if (mousePos.X >= dropdown.Position.X && mousePos.X <= dropdown.Position.X + dropdown.Size.X &&
                        mousePos.Y >= dropdown.Position.Y && mousePos.Y <= dropdown.Position.Y + dropdown.Size.Y)
                    {
                        isOverUi = true;
                        Console.WriteLine($"HtmlUiSystem: Mouse over Dropdown {dropdown.Name} at Pos: ({dropdown.Position.X}, {dropdown.Position.Y}), Size: ({dropdown.Size.X}, {dropdown.Size.Y}), IsExpanded: {dropdown.IsExpanded}");
                        break;
                    }
                }
            }

            if (isMouseCaptured && !isOverUi && mousePos.X > 650)
            {
                Console.WriteLine($"HtmlUiSystem: Input blocked - Mouse captured, not over UI, Pos: {mousePos}");
                return;
            }

            if (button != MouseButton.Left) return;

            foreach (var element in _elements)
            {
                bool inBounds = false;
                string id = null;
                if (element is UiElement uiElement)
                {
                    inBounds = uiElement.Bounds.Contains((int)mousePos.X, (int)mousePos.Y);
                    id = uiElement.Id;
                }
                else if (element is Toggle toggle)
                {
                    inBounds = mousePos.X >= toggle.Position.X && mousePos.X <= toggle.Position.X + toggle.Size.X &&
                               mousePos.Y >= toggle.Position.Y && mousePos.Y <= toggle.Position.Y + toggle.Size.Y;
                    id = toggle.Name;
                }
                else if (element is Dropdown dropdown)
                {
                    var pos = dropdown.Position;
                    var size = dropdown.Size;
                    inBounds = mousePos.X >= pos.X && mousePos.X <= pos.X + size.X &&
                               (action == InputAction.Press && mousePos.Y >= pos.Y && mousePos.Y <= pos.Y + size.Y ||
                                action == InputAction.Release && mousePos.Y >= pos.Y && mousePos.Y <= pos.Y + size.Y + dropdown.Options.Count * 30);
                    id = _elements.IndexOf(element).ToString();
                    if (inBounds && action == InputAction.Press)
                    {
                        dropdown.ToggleExpanded();
                        _hoveredElementOnMouseDown = element;
                        Console.WriteLine($"HtmlUiSystem: Dropdown {dropdown.Name} toggled, IsExpanded: {dropdown.IsExpanded}");
                    }
                    else if (inBounds && action == InputAction.Release && _hoveredElementOnMouseDown == element)
                    {
                        int optionIndex = dropdown.GetOptionIndexAt(mousePos, pos);
                        Console.WriteLine($"HtmlUiSystem: Dropdown {dropdown.Name} option check, OptionIndex: {optionIndex}, MousePos: {mousePos}, DropdownPos: {pos}");
                        if (optionIndex >= 0)
                        {
                            dropdown.SelectOption(optionIndex);
                            string brush = dropdown.Options[optionIndex];
                            _actionConfig.Trigger(brush);
                            Console.WriteLine($"HtmlUiSystem: Selected brush '{brush}' for Dropdown {dropdown.Name}");
                        }
                        else
                        {
                            Console.WriteLine($"HtmlUiSystem: No option selected for Dropdown {dropdown.Name}");
                        }
                    }
                }

                if (inBounds && !(element is Dropdown))
                {
                    if (action == InputAction.Press)
                    {
                        _hoveredElementOnMouseDown = element;
                        Console.WriteLine($"HtmlUiSystem: Mouse down on {id}");
                    }
                    else if (action == InputAction.Release && _hoveredElementOnMouseDown == element)
                    {
                        Console.WriteLine($"HtmlUiSystem: Mouse released on {id}");
                        if (element is Toggle toggle)
                        {
                            toggle.ToggleState();
                            _gridSnapState = toggle.State;
                            _actionConfig.Trigger(id);
                            _eventBus.Publish(new ToggleGridSnapEvent(_steamEngine?.GetSteamId() ?? 0, _gridSnapState), true);
                            Console.WriteLine($"HtmlUiSystem: Toggled {toggle.Name} to state: {toggle.State}, Published ToggleGridSnapEvent");
                        }
                        else if (_actions.TryGetValue(id, out var act))
                        {
                            act.Invoke();
                            if (id == "back")
                            {
                                ulong steamId = _steamEngine != null ? _steamEngine.GetSteamId() : 0;
                                _server.Publish(new ExitEditorEvent(steamId), true);
                                Console.WriteLine($"HtmlUiSystem: Triggered back action, publishing ExitEditorEvent for SteamID: {steamId}");
                            }
                            Console.WriteLine($"HtmlUiSystem: Invoked action for {id}");
                        }
                    }
                }
            }
        }

        public override void Update(float deltaTime) { }

        public void Render()
        {
            _glfw.GetWindowSize(_window, out int width, out int height);
            _renderContext.Viewport(0, 0, (uint)width, (uint)height);
            _renderContext.Clear(ClearBufferMask.DepthBufferBit);
            _renderContext.Disable(EnableCap.DepthTest);
            _renderContext.DepthMask(false);
            _renderContext.DepthFunc(DepthFunction.Always);
            _renderContext.ColorMask(true, true, true, true);
            _renderContext.Enable(EnableCap.Blend);
            _renderContext.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _renderContext.ActiveTexture(TextureUnit.Texture0);

            _renderContext.UseProgram(0);
            for (int i = 0; i < 32; i++)
            {
                _renderContext.ActiveTexture((TextureUnit)((int)TextureUnit.Texture0 + i));
                _renderContext.BindTexture(TextureTarget.Texture2D, 0);
            }
            _renderContext.ActiveTexture(TextureUnit.Texture0);
            _renderContext.BindVertexArray(0);

            _renderContext.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _renderContext.DrawBuffer(DrawBufferMode.Back);
            _renderContext.ReadBuffer(ReadBufferMode.Back);
            GLEnum fbStatus = _renderContext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (fbStatus != GLEnum.FramebufferComplete)
            {
                //Console.WriteLine($"HtmlUiSystem: Framebuffer incomplete, status: {fbStatus}");
            }
            else
            {
                //Console.WriteLine("HtmlUiSystem: Framebuffer status: FramebufferComplete");
            }

            _renderContext.UseProgram(0);
            _shader.Use();

            Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
            _shader.SetMatrix4("uProjection", projection);
            Matrix4x4 transform = Matrix4x4.Identity;
            _shader.SetMatrix4("uTransform", transform);

            int renderPassCount = 0;
            foreach (var element in _elements)
            {
                if (element is UiElement uiElement)
                {
                    _shader.SetUniform("uUseTexture", 0.0f);
                    var vertices = new List<Vertex>
                    {
                        new Vertex(uiElement.Bounds.X, uiElement.Bounds.Y, 0, uiElement.Color.X, uiElement.Color.Y, uiElement.Color.Z, uiElement.Color.W),
                        new Vertex(uiElement.Bounds.X + uiElement.Bounds.Width, uiElement.Bounds.Y, 0, uiElement.Color.X, uiElement.Color.Y, uiElement.Color.Z, uiElement.Color.W),
                        new Vertex(uiElement.Bounds.X, uiElement.Bounds.Y + uiElement.Bounds.Height, 0, uiElement.Color.X, uiElement.Color.Y, uiElement.Color.Z, uiElement.Color.W),
                        new Vertex(uiElement.Bounds.X + uiElement.Bounds.Width, uiElement.Bounds.Y + uiElement.Bounds.Height, 0, uiElement.Color.X, uiElement.Color.Y, uiElement.Color.Z, uiElement.Color.W)
                    };
                    var indices = new List<uint> { 0, 1, 2, 1, 2, 3 };
                    _uiBuffer.UpdateCustom(vertices, indices);
                    _uiBuffer.Bind();
                    _renderContext.DrawElements(PrimitiveType.Triangles, _uiBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                    renderPassCount++;

                    if (!string.IsNullOrEmpty(uiElement.Text))
                    {
                        _renderContext.BindVertexArray(0);
                        _renderContext.DisableVertexAttribArray(0);
                        _renderContext.DisableVertexAttribArray(1);
                        _renderContext.Disable(EnableCap.DepthTest);
                        _renderContext.DepthMask(false);
                        _renderContext.Clear(ClearBufferMask.DepthBufferBit);
                        _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                        _shader.SetUniform("uUseTexture", 1.0f);
                        _shader.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
                        _shader.SetUniform("uTexture", 0);
                        _renderContext.ActiveTexture(TextureUnit.Texture0);
                        GLEnum textFbStatus = _renderContext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

                        float textX = uiElement.Bounds.X + 10;
                        float textY = uiElement.Bounds.Y + (uiElement.Bounds.Height - 12) / 2 + 10;
                        _textRenderer.RenderText(uiElement.Text, textX, textY, width, height, 12.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                        GLEnum error = _renderContext.GetError();
                        if (error != GLEnum.NoError)
                        {
                            //Console.WriteLine($"HtmlUiSystem: OpenGL error after text '{uiElement.Text}': {error}");
                        }
                        renderPassCount++;
                    }
                }
                else if (element is Toggle toggle)
                {
                    _shader.SetUniform("uUseTexture", 0.0f);
                    Rendering.Definitions.Color bgColor = toggle.State ? new Rendering.Definitions.Color { R = 0.0f, G = 1.0f, B = 0.0f, A = 0.9f } : new Rendering.Definitions.Color { R = 0.2f, G = 0.2f, B = 0.2f, A = 0.9f };
                    var vertices = new List<Vertex>
                    {
                        new Vertex(toggle.Position.X, toggle.Position.Y, 0, bgColor.R, bgColor.G, bgColor.B, bgColor.A),
                        new Vertex(toggle.Position.X + toggle.Size.X, toggle.Position.Y, 0, bgColor.R, bgColor.G, bgColor.B, bgColor.A),
                        new Vertex(toggle.Position.X, toggle.Position.Y + toggle.Size.Y, 0, bgColor.R, bgColor.G, bgColor.B, bgColor.A),
                        new Vertex(toggle.Position.X + toggle.Size.X, toggle.Position.Y + toggle.Size.Y, 0, bgColor.R, bgColor.G, bgColor.B, bgColor.A)
                    };
                    var indices = new List<uint> { 0, 1, 2, 1, 2, 3 };
                    _uiBuffer.UpdateCustom(vertices, indices);
                    _uiBuffer.Bind();
                    _renderContext.DrawElements(PrimitiveType.Triangles, _uiBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                    renderPassCount++;

                    _renderContext.BindVertexArray(0);
                    _renderContext.DisableVertexAttribArray(0);
                    _renderContext.DisableVertexAttribArray(1);
                    _renderContext.Disable(EnableCap.DepthTest);
                    _renderContext.DepthMask(false);
                    _renderContext.Clear(ClearBufferMask.DepthBufferBit);
                    GLEnum fbStatusPostToggle = _renderContext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

                    if (!string.IsNullOrEmpty(toggle.Name))
                    {
                        _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                        _shader.SetUniform("uUseTexture", 1.0f);
                        _shader.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
                        _shader.SetUniform("uTexture", 0);
                        _renderContext.ActiveTexture(TextureUnit.Texture0);
                        GLEnum textFbStatus = _renderContext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

                        float textX = toggle.Position.X + toggle.Size.X + 5;
                        float textY = toggle.Position.Y + (toggle.Size.Y - 12) / 2 + 10;
                        _textRenderer.RenderText("Grid Snap", textX, textY, width, height, 12.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                        GLEnum error = _renderContext.GetError();
                        if (error != GLEnum.NoError)
                        {
                            //Console.WriteLine($"HtmlUiSystem: OpenGL error after toggle text '{toggle.Name}': {error}");
                        }
                        renderPassCount++;
                    }
                }
                else if (element is Dropdown dropdown)
                {
                    _shader.SetUniform("uUseTexture", 0.0f);
                    var pos = new Vector2(dropdown.Position.X, dropdown.Position.Y);
                    var size = new Vector2(dropdown.Size.X, dropdown.Size.Y);
                    var vertices = new List<Vertex>
                    {
                        new Vertex(pos.X, pos.Y, 0, 0.2f, 0.8f, 0.2f, 0.9f),
                        new Vertex(pos.X + size.X, pos.Y, 0, 0.2f, 0.8f, 0.2f, 0.9f),
                        new Vertex(pos.X, pos.Y + size.Y, 0, 0.2f, 0.8f, 0.2f, 0.9f),
                        new Vertex(pos.X + size.X, pos.Y + size.Y, 0, 0.2f, 0.8f, 0.2f, 0.9f)
                    };
                    var indices = new List<uint> { 0, 1, 2, 1, 2, 3 };
                    _uiBuffer.UpdateCustom(vertices, indices);
                    _uiBuffer.Bind();
                    _renderContext.DrawElements(PrimitiveType.Triangles, _uiBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                    renderPassCount++;

                    _renderContext.BindVertexArray(0);
                    _renderContext.DisableVertexAttribArray(0);
                    _renderContext.DisableVertexAttribArray(1);
                    _renderContext.Disable(EnableCap.DepthTest);
                    _renderContext.DepthMask(false);
                    _renderContext.Clear(ClearBufferMask.DepthBufferBit);

                    string displayText = dropdown.Options[dropdown.SelectedIndex];
                    float textX = pos.X + 10;
                    float textY = pos.Y + (size.Y - 12) / 2 + 10;
                    _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                    _shader.SetUniform("uUseTexture", 1.0f);
                    _shader.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
                    _shader.SetUniform("uTexture", 0);
                    _renderContext.ActiveTexture(TextureUnit.Texture0);
                    GLEnum textFbStatus = _renderContext.CheckFramebufferStatus(FramebufferTarget.Framebuffer);

                    _textRenderer.RenderText(displayText, textX, textY, width, height, 12.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                    GLEnum error = _renderContext.GetError();
                    if (error != GLEnum.NoError)
                    {
                        //Console.WriteLine($"HtmlUiSystem: OpenGL error after text '{displayText}': {error}");
                    }
                    renderPassCount++;

                    if (dropdown.IsExpanded)
                    {
                        float optionY = pos.Y + size.Y;
                        for (int i = 0; i < dropdown.Options.Count; i++)
                        {
                            vertices = new List<Vertex>
                            {
                                new Vertex(pos.X, optionY, 0, 0.1f, 0.6f, 0.1f, 0.9f),
                                new Vertex(pos.X + size.X, optionY, 0, 0.1f, 0.6f, 0.1f, 0.9f),
                                new Vertex(pos.X, optionY + 30, 0, 0.1f, 0.6f, 0.1f, 0.9f),
                                new Vertex(pos.X + size.X, optionY + 30, 0, 0.1f, 0.6f, 0.1f, 0.9f)
                            };
                            indices = new List<uint> { 0, 1, 2, 1, 2, 3 };
                            _uiBuffer.UpdateCustom(vertices, indices);
                            _uiBuffer.Bind();
                            _renderContext.DrawElements(PrimitiveType.Triangles, _uiBuffer.GetIndexCount(), DrawElementsType.UnsignedInt, null);
                            renderPassCount++;

                            _renderContext.BindTexture(TextureTarget.Texture2D, 0);
                            _shader.SetUniform("uUseTexture", 1.0f);
                            _shader.SetUniform("uColor", 1.0f, 1.0f, 1.0f, 1.0f);
                            _shader.SetUniform("uTexture", 0);
                            _renderContext.ActiveTexture(TextureUnit.Texture0);
                            _textRenderer.RenderText(dropdown.Options[i], pos.X + 10, optionY + 10, width, height, 12.0f, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
                            GLEnum errorOption = _renderContext.GetError();
                            if (errorOption != GLEnum.NoError)
                            {
                                //Console.WriteLine($"HtmlUiSystem: OpenGL error after option text '{dropdown.Options[i]}': {errorOption}");
                            }
                            renderPassCount++;
                            optionY += 30;
                        }
                    }
                }
            }

            Console.WriteLine($"HtmlUiSystem: Completed rendering with {renderPassCount} passes");
        }

        private void LoadUi()
        {
            string html = File.ReadAllText(_htmlPath);
            var lines = html.Split('\n');
            float xOffset = 10f;

            foreach (var line in lines)
            {
                if (line.Contains("<div") && line.Contains("class=\"toolbar\""))
                {
                    continue;
                }
                if (line.Contains("<button"))
                {
                    var id = ExtractAttribute(line, "id");
                    var text = line.Split('>')[1].Split('<')[0];
                    _elements.Add(new UiElement
                    {
                        Id = id,
                        Text = text,
                        Bounds = new Rectangle((int)xOffset, 10, 120, 40),
                        Color = new Vector4(0.1f, 0.5f, 0.8f, 0.9f),
                        IsButton = true
                    });
                    _actions[id] = () => _actionConfig.Trigger(id);
                    xOffset += 130f;
                }
                else if (line.Contains("<select"))
                {
                    var id = ExtractAttribute(line, "id");
                    var options = new List<string>();
                    int selectedIndex = 0;
                    int optionIndex = 0;
                    foreach (var optLine in lines.SkipWhile(l => l != line).Skip(1))
                    {
                        if (optLine.Contains("</select>")) break;
                        if (optLine.Contains("<option"))
                        {
                            var value = ExtractAttribute(optLine, "value");
                            options.Add(value);
                            if (value == _selectedBrush) selectedIndex = optionIndex;
                            optionIndex++;
                        }
                    }
                    var dropdown = new Dropdown(new DropdownDefinition
                    {
                        Position = new Position { X = xOffset, Y = 10 },
                        Size = new SiegeEngine.Rendering.Definitions.Size { Width = 150, Height = 40 },
                        Options = options,
                        SelectedIndex = selectedIndex,
                        IsOptionsBelow = true
                    }, index => _actionConfig.Trigger(options[index]));
                    _elements.Add(dropdown);
                    _actions[id] = () => { };
                    xOffset += 160f;
                }
                else if (line.Contains("<input") && line.Contains("type=\"checkbox\""))
                {
                    var id = ExtractAttribute(line, "id");
                    var toggle = new Toggle(new ToggleDefinition
                    {
                        Name = id,
                        Position = new Position { X = xOffset, Y = 10 },
                        Size = new SiegeEngine.Rendering.Definitions.Size { Width = 20, Height = 20 },
                        ButtonStyle = new ButtonStyle(),
                        TextStyle = new TextStyle { FontSize = 12.0f },
                        State = _gridSnapState
                    }, null);
                    _elements.Add(toggle);
                    _actions[id] = () => _actionConfig.Trigger(id);
                    xOffset += 80f;
                }
            }
        }

        private string ExtractAttribute(string line, string attribute)
        {
            var start = line.IndexOf(attribute + "=\"") + attribute.Length + 2;
            var end = line.IndexOf("\"", start);
            return line.Substring(start, end - start);
        }

        public void SetSelectedBrush(string brush)
        {
            _selectedBrush = brush;
            foreach (var element in _elements)
            {
                if (element is Dropdown dropdown)
                {
                    int index = dropdown.Options.IndexOf(brush);
                    if (index >= 0)
                        dropdown.SelectOption(index);
                }
            }
        }

        public void Dispose()
        {
            _shader.Dispose();
            _uiBuffer.Dispose();
            _textRenderer.Dispose();
        }
    }
}