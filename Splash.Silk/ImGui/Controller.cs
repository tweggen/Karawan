using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.inputs;
using engine.news;
using ImGuiNET;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
#if GLES
using Silk.NET.OpenGLES;
#elif GL
using Silk.NET.OpenGL;
#elif LEGACY
using Silk.NET.OpenGL.Legacy;
#endif
using Point = System.Drawing.Point;
using static engine.Logger;

namespace Splash.Silk.ImGui;

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public class Controller : IDisposable
{
    private GL _gl;
    /*
     * WP-5.3 / KI-11: was a Silk IView. The controller only ever needed three things from
     * it - resize notification, and the framebuffer size - and IWindowBackend has all of
     * them. That was the whole "ImGui entanglement": not the renderer, just the window
     * handle and an input context.
     */
    private IWindowBackend _backend;
    private bool _frameBegun;
    private readonly List<char> _pressedChars = new();
    private engine.inputs.IKeyboard _keyboard;

    private int _attribLocationTex;
    private int _attribLocationProjMtx;
    private int _attribLocationVtxPos;
    private int _attribLocationVtxUV;
    private int _attribLocationVtxColor;
    private uint _vboHandle;
    private uint _elementsHandle;
    private uint _vertexArrayObject;

    public uint Program;

    private Texture _fontTexture;
    private Shader _shader;
    private bool _initialized = false;

    private int _windowWidth;
    private int _windowHeight;

    public IntPtr Context;

    [Conditional("DEBUG")]
    public static void CheckGlError(GL gl, string title)
    {
        var error = gl.GetError();
        if (!string.IsNullOrWhiteSpace(title))
        {
            if (error != GLEnum.NoError)
            {
                Warning($"{title}: {error}");
            }
        }
    }

    private readonly Dictionary<string, int> _uniformToLocation = new Dictionary<string, int>();

    private int GetUniformLocation(string uniform)
    {
        if (_uniformToLocation.TryGetValue(uniform, out int location) == false)
        {
            location = _gl.GetUniformLocation(Program, uniform);
            _uniformToLocation.Add(uniform, location);

            if (location == -1)
            {
                Warning($"The uniform '{uniform}' does not exist in the shader!");
            }
        }

        return location;
    }

    private readonly Dictionary<string, int> _attribLocation = new Dictionary<string, int>();
    public int GetAttribLocation(string attrib)
    {
        if (_attribLocation.TryGetValue(attrib, out int location) == false)
        {
            location = _gl.GetAttribLocation(Program, attrib);
            _attribLocation.Add(attrib, location);

            if (location == -1)
            {
                Warning($"The attrib '{attrib}' does not exist in the shader!");
            }
        }

        return location;
    }

    
    public void UseShader()
    {
        _gl.UseProgram(Program);
    }
    
    
    public void MakeCurrent()
    {
        ImGuiNET.ImGui.SetCurrentContext(Context);
    }

    private void Init(GL gl, IWindowBackend backend)
    {
        _gl = gl;
        _backend = backend;
        _windowWidth = (int)backend.Size.X;
        _windowHeight = (int)backend.Size.Y;

        _backend.OnResize += WindowResized;

        Context = ImGuiNET.ImGui.CreateContext();
        ImGuiNET.ImGui.SetCurrentContext(Context);
        ImGuiNET.ImGui.StyleColorsDark();
    }

    private void BeginFrame()
    {
        ImGuiNET.ImGui.NewFrame();
        _frameBegun = true;
        /*
         * The resize subscription used to live HERE, which meant it was added once per
         * FRAME and never removed - an unbounded delegate chain, and WindowResized running
         * once per frame elapsed. It is now done once, in Init.
         *
         * The keyboard scan that also lived here is gone with engine.inputs.IContext: the
         * controller is FED input by Platform now (FeedKey/FeedChar/FeedMouse*) rather
         * than reaching out for a device. See the class remarks.
         */
    }

    /// <summary>
    /// Delegate to receive keyboard key events.
    /// </summary>
    /// <param name="keyboard">The keyboard context generating the event.</param>
    /// <param name="scancode">The native scancode of the key generating the event.</param>
    /// <param name="keycode">The native keycode of the key generating the event.</param>
    /// <param name="scancode">The native scancode of the key generating the event.</param>
    /// <param name="down">True if the event is a key down event, otherwise False</param>
    private static void OnKeyEvent(char keyCode, engine.inputs.ScanCode scanCode, bool down)
    {
        var io = ImGuiNET.ImGui.GetIO();
        var imGuiKey = TranslateInputKeyToImGuiKey(keyCode, scanCode);
        io.AddKeyEvent(imGuiKey, down);
        io.SetKeyEventNativeData(imGuiKey, (int)keyCode, (int)scanCode);
    }



    private void WindowResized(Vector2 size)
    {
        _windowWidth = (int)size.X;
        _windowHeight = (int)size.Y;
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
    /// or index data has increased beyond the capacity of the existing buffers.
    /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
    /// </summary>
    public void Render()
    {
        if (_frameBegun)
        {
            var oldCtx = ImGuiNET.ImGui.GetCurrentContext();

            if (oldCtx != Context)
            {
                ImGuiNET.ImGui.SetCurrentContext(Context);
            }

            _frameBegun = false;
            ImGuiNET.ImGui.Render();
            RenderImDrawData(ImGuiNET.ImGui.GetDrawData());

            if (oldCtx != Context)
            {
                ImGuiNET.ImGui.SetCurrentContext(oldCtx);
            }
        }
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        var oldCtx = ImGuiNET.ImGui.GetCurrentContext();

        if (oldCtx != Context)
        {
            ImGuiNET.ImGui.SetCurrentContext(Context);
        }

        if (_frameBegun)
        {
            ImGuiNET.ImGui.Render();
        }

        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput();

        _frameBegun = true;
        ImGuiNET.ImGui.NewFrame();

        if (oldCtx != Context)
        {
            ImGuiNET.ImGui.SetCurrentContext(oldCtx);
        }
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        var io = ImGuiNET.ImGui.GetIO();
        io.DisplaySize = new Vector2(_windowWidth, _windowHeight);

        if (_windowWidth > 0 && _windowHeight > 0)
        {
            io.DisplayFramebufferScale = new Vector2(_backend.FramebufferSize.X / _windowWidth,
                _backend.FramebufferSize.Y / _windowHeight);
        }

        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    /*
     * Input state, fed by Platform from the SAME backend callbacks that drive the engine's
     * event queue (WP-5.3 / KI-11).
     *
     * Why fed rather than pulled: this controller used to reach into a Silk IInputContext
     * and poll it. No surviving backend provides one, and the engine's own input travels
     * as queue events drained on the LOGICAL thread while ImGui renders on the platform
     * thread - so pulling from the queue here would mean cross-thread state and a second
     * ordering. Platform already receives every raw callback on the platform thread, which
     * is the same thread Render runs on, so handing them straight over needs no lock and
     * introduces no second source of truth.
     */
    private Vector2 _mousePosition;
    private readonly bool[] _mouseDown = new bool[3];
    private Vector2 _mouseWheel;

    internal void FeedMouseMoved(Vector2 position) => _mousePosition = position;

    internal void FeedMouseButton(int button, bool down)
    {
        if (button >= 0 && button < _mouseDown.Length) _mouseDown[button] = down;
    }

    internal void FeedMouseWheel(Vector2 delta) => _mouseWheel += delta;

    internal void FeedKey(char keyCode, engine.inputs.ScanCode scanCode, bool down)
        => OnKeyEvent(keyCode, scanCode, down);

    /**
     * True while ImGui wants the pointer, i.e. the cursor is over a panel. Platform uses
     * this to decide whether a click belongs to the UI or to the game.
     */
    public bool WantCaptureMouse => ImGuiNET.ImGui.GetIO().WantCaptureMouse;

    /**
     * True while ImGui wants typed input, i.e. a text field has focus.
     */
    public bool WantCaptureKeyboard => ImGuiNET.ImGui.GetIO().WantCaptureKeyboard;

    private void UpdateImGuiInput()
    {
        var io = ImGuiNET.ImGui.GetIO();

        /*
         * Mouse position is in LOGICAL units, matching io.DisplaySize, which is set from
         * IWindowBackend.Size rather than FramebufferSize. On a HiDPI display those differ
         * by the scale factor, and feeding pixels here would put every hit-test off by
         * exactly that factor while rendering looked perfect - which is the failure GATE-C
         * cannot currently check for, and the reason this work unblocks it.
         */
        io.MousePos = _mousePosition;

        io.MouseDown[0] = _mouseDown[0];
        io.MouseDown[1] = _mouseDown[1];
        io.MouseDown[2] = _mouseDown[2];

        io.MouseWheel = _mouseWheel.Y;
        io.MouseWheelH = _mouseWheel.X;
        // Wheel is a DELTA, so it has to be consumed. Left set, it scrolls forever.
        _mouseWheel = Vector2.Zero;

        foreach (var c in _pressedChars)
        {
            io.AddInputCharacter(c);
        }

        _pressedChars.Clear();
    }

    internal void PressChar(char keyChar)
    {
        _pressedChars.Add(keyChar);
    }

    /// <summary>
    /// Translates a Silk.NET.Input.Key to an ImGuiKey.
    /// </summary>
    /// <param name="key">The Silk.NET.Input.Key to translate.</param>
    /// <returns>The corresponding ImGuiKey.</returns>
    /// <exception cref="NotImplementedException">When the key has not been implemented yet.</exception>
    private static ImGuiKey TranslateInputKeyToImGuiKey(char keycode, ScanCode scancode)
    {
        /*
         * TXWTODO: I have no clue which semantics to use.
         */
        return (ImGuiKey)keycode;
    }

    private unsafe void SetupRenderState(ImDrawDataPtr drawDataPtr, int framebufferWidth, int framebufferHeight)
    {
        // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
        _gl.Enable(GLEnum.Blend);
        _gl.BlendEquation(GLEnum.FuncAdd);
        _gl.BlendFuncSeparate(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha, GLEnum.One, GLEnum.OneMinusSrcAlpha);
        _gl.Disable(GLEnum.CullFace);
        _gl.Disable(GLEnum.DepthTest);
        _gl.Disable(GLEnum.StencilTest);
        _gl.Enable(GLEnum.ScissorTest);
#if !GLES && !LEGACY
        _gl.Disable(GLEnum.PrimitiveRestart);
        _gl.PolygonMode(GLEnum.FrontAndBack, GLEnum.Fill);
#endif

        float L = drawDataPtr.DisplayPos.X;
        float R = drawDataPtr.DisplayPos.X + drawDataPtr.DisplaySize.X;
        float T = drawDataPtr.DisplayPos.Y;
        float B = drawDataPtr.DisplayPos.Y + drawDataPtr.DisplaySize.Y;

        Span<float> orthoProjection = stackalloc float[]
        {
            2.0f / (R - L), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (T - B), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T), 0.0f, 1.0f,
        };

        UseShader();
        CheckGlError(_gl, "");
        _gl.Uniform1(_attribLocationTex, 0);
        CheckGlError(_gl, "");
        _gl.UniformMatrix4(_attribLocationProjMtx, 1, false, orthoProjection);
        CheckGlError(_gl, "Projection");

        _gl.BindSampler(0, 0);

        // Setup desired GL state
        // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
        // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
        _vertexArrayObject = _gl.GenVertexArray();
        _gl.BindVertexArray(_vertexArrayObject);
        CheckGlError(_gl, "VAO");

        // Bind vertex/index buffers and setup attributes for ImDrawVert
        _gl.BindBuffer(GLEnum.ArrayBuffer, _vboHandle);
        _gl.BindBuffer(GLEnum.ElementArrayBuffer, _elementsHandle);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxPos);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxUV);
        _gl.EnableVertexAttribArray((uint)_attribLocationVtxColor);
        _gl.VertexAttribPointer((uint)_attribLocationVtxPos, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert),
            (void*)0);
        _gl.VertexAttribPointer((uint)_attribLocationVtxUV, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)8);
        _gl.VertexAttribPointer((uint)_attribLocationVtxColor, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert),
            (void*)16);
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr drawDataPtr)
    {
        int framebufferWidth = (int)(drawDataPtr.DisplaySize.X * drawDataPtr.FramebufferScale.X);
        int framebufferHeight = (int)(drawDataPtr.DisplaySize.Y * drawDataPtr.FramebufferScale.Y);
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
            return;

        // Backup GL state
        _gl.GetInteger(GLEnum.ActiveTexture, out int lastActiveTexture);
        _gl.ActiveTexture(GLEnum.Texture0);

        _gl.GetInteger(GLEnum.CurrentProgram, out int lastProgram);
        _gl.GetInteger(GLEnum.TextureBinding2D, out int lastTexture);

        _gl.GetInteger(GLEnum.SamplerBinding, out int lastSampler);

        _gl.GetInteger(GLEnum.ArrayBufferBinding, out int lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out int lastVertexArrayObject);

#if !GLES
        Span<int> lastPolygonMode = stackalloc int[2];
        _gl.GetInteger(GLEnum.PolygonMode, lastPolygonMode);
#endif

        Span<int> lastScissorBox = stackalloc int[4];
        _gl.GetInteger(GLEnum.ScissorBox, lastScissorBox);

        _gl.GetInteger(GLEnum.BlendSrcRgb, out int lastBlendSrcRgb);
        _gl.GetInteger(GLEnum.BlendDstRgb, out int lastBlendDstRgb);

        _gl.GetInteger(GLEnum.BlendSrcAlpha, out int lastBlendSrcAlpha);
        _gl.GetInteger(GLEnum.BlendDstAlpha, out int lastBlendDstAlpha);

        _gl.GetInteger(GLEnum.BlendEquationRgb, out int lastBlendEquationRgb);
        _gl.GetInteger(GLEnum.BlendEquationAlpha, out int lastBlendEquationAlpha);

        bool lastEnableBlend = _gl.IsEnabled(GLEnum.Blend);
        bool lastEnableCullFace = _gl.IsEnabled(GLEnum.CullFace);
        bool lastEnableDepthTest = _gl.IsEnabled(GLEnum.DepthTest);
        bool lastEnableStencilTest = _gl.IsEnabled(GLEnum.StencilTest);
        bool lastEnableScissorTest = _gl.IsEnabled(GLEnum.ScissorTest);

#if !GLES && !LEGACY
        bool lastEnablePrimitiveRestart = _gl.IsEnabled(GLEnum.PrimitiveRestart);
#endif

        SetupRenderState(drawDataPtr, framebufferWidth, framebufferHeight);

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOff = drawDataPtr.DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawDataPtr.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        for (int n = 0; n < drawDataPtr.CmdListsCount; n++)
        {
            ImDrawListPtr cmdListPtr = drawDataPtr.CmdLists[n];

            // Upload vertex/index buffers

            _gl.BufferData(GLEnum.ArrayBuffer, (nuint)(cmdListPtr.VtxBuffer.Size * sizeof(ImDrawVert)),
                (void*)cmdListPtr.VtxBuffer.Data, GLEnum.StreamDraw);
            CheckGlError(_gl, $"Data Vert {n}");
            _gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(cmdListPtr.IdxBuffer.Size * sizeof(ushort)),
                (void*)cmdListPtr.IdxBuffer.Data, GLEnum.StreamDraw);
            CheckGlError(_gl, $"Data Idx {n}");

            for (int cmd_i = 0; cmd_i < cmdListPtr.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr cmdPtr = cmdListPtr.CmdBuffer[cmd_i];

                if (cmdPtr.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    Vector4 clipRect;
                    clipRect.X = (cmdPtr.ClipRect.X - clipOff.X) * clipScale.X;
                    clipRect.Y = (cmdPtr.ClipRect.Y - clipOff.Y) * clipScale.Y;
                    clipRect.Z = (cmdPtr.ClipRect.Z - clipOff.X) * clipScale.X;
                    clipRect.W = (cmdPtr.ClipRect.W - clipOff.Y) * clipScale.Y;

                    if (clipRect.X < framebufferWidth && clipRect.Y < framebufferHeight && clipRect.Z >= 0.0f &&
                        clipRect.W >= 0.0f)
                    {
                        // Apply scissor/clipping rectangle
                        _gl.Scissor((int)clipRect.X, (int)(framebufferHeight - clipRect.W),
                            (uint)(clipRect.Z - clipRect.X), (uint)(clipRect.W - clipRect.Y));
                        CheckGlError(_gl, "Scissor");

                        // Bind texture, Draw
                        _gl.BindTexture(GLEnum.Texture2D, (uint)cmdPtr.TextureId);
                        CheckGlError(_gl, "Texture");

                        _gl.DrawElementsBaseVertex(GLEnum.Triangles, cmdPtr.ElemCount, GLEnum.UnsignedShort,
                            (void*)(cmdPtr.IdxOffset * sizeof(ushort)), (int)cmdPtr.VtxOffset);
                        CheckGlError(_gl, "Draw");
                    }
                }
            }
        }

        // Destroy the temporary VAO
        _gl.DeleteVertexArray(_vertexArrayObject);
        _vertexArrayObject = 0;

        // Restore modified GL state
        _gl.UseProgram((uint)lastProgram);
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);

        _gl.BindSampler(0, (uint)lastSampler);

        _gl.ActiveTexture((GLEnum)lastActiveTexture);

        _gl.BindVertexArray((uint)lastVertexArrayObject);

        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);
        _gl.BlendEquationSeparate((GLEnum)lastBlendEquationRgb, (GLEnum)lastBlendEquationAlpha);
        _gl.BlendFuncSeparate((GLEnum)lastBlendSrcRgb, (GLEnum)lastBlendDstRgb, (GLEnum)lastBlendSrcAlpha,
            (GLEnum)lastBlendDstAlpha);

        if (lastEnableBlend)
        {
            _gl.Enable(GLEnum.Blend);
        }
        else
        {
            _gl.Disable(GLEnum.Blend);
        }

        if (lastEnableCullFace)
        {
            _gl.Enable(GLEnum.CullFace);
        }
        else
        {
            _gl.Disable(GLEnum.CullFace);
        }

        if (lastEnableDepthTest)
        {
            _gl.Enable(GLEnum.DepthTest);
        }
        else
        {
            _gl.Disable(GLEnum.DepthTest);
        }

        if (lastEnableStencilTest)
        {
            _gl.Enable(GLEnum.StencilTest);
        }
        else
        {
            _gl.Disable(GLEnum.StencilTest);
        }

        if (lastEnableScissorTest)
        {
            _gl.Enable(GLEnum.ScissorTest);
        }
        else
        {
            _gl.Disable(GLEnum.ScissorTest);
        }

#if !GLES && !LEGACY
        if (lastEnablePrimitiveRestart)
        {
            _gl.Enable(GLEnum.PrimitiveRestart);
        }
        else
        {
            _gl.Disable(GLEnum.PrimitiveRestart);
        }

        _gl.PolygonMode(GLEnum.FrontAndBack, (GLEnum)lastPolygonMode[0]);
#endif

        _gl.Scissor(lastScissorBox[0], lastScissorBox[1], (uint)lastScissorBox[2], (uint)lastScissorBox[3]);
    }

    
    private uint CompileShader(ShaderType type, string source)
    {
        var shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var success);
        if (success == 0)
        {
            string info = _gl.GetShaderInfoLog(shader);
            Debug.WriteLine($"GL.CompileShader for shader [{type}] had info log:\n{info}");
        }

        return shader;
    }

    
    private uint CreateProgram(params (ShaderType Type, string source)[] shaderPaths)
    {
        var program = _gl.CreateProgram();

        Span<uint> shaders = stackalloc uint[shaderPaths.Length];
        for (int i = 0; i < shaderPaths.Length; i++)
        {
            shaders[i] = CompileShader(shaderPaths[i].Type, shaderPaths[i].source);
        }

        foreach (var shader in shaders)
            _gl.AttachShader(program, shader);

        _gl.LinkProgram(program);

        _gl.GetProgram(program, GLEnum.LinkStatus, out var success);
        if (success == 0)
        {
            string info = _gl.GetProgramInfoLog(program);
            Debug.WriteLine($"GL.LinkProgram had info log:\n{info}");
        }

        foreach (var shader in shaders)
        {
            _gl.DetachShader(program, shader);
            _gl.DeleteShader(shader);
        }

        _initialized = true;

        return program;
    }
    
    private void CreateDeviceResources()
    {
        // Backup GL state

        _gl.GetInteger(GLEnum.TextureBinding2D, out int lastTexture);
        _gl.GetInteger(GLEnum.ArrayBufferBinding, out int lastArrayBuffer);
        _gl.GetInteger(GLEnum.VertexArrayBinding, out int lastVertexArray);

        string api = engine.GlobalSettings.Get("platform.threeD.API");

        string vertexSource;

        if (api == "OpenGLES")
        {
            vertexSource =
                @"#version 300 es
                precision highp float;
                    
                layout (location = 0) in vec2 Position;
                layout (location = 1) in vec2 UV;
                layout (location = 2) in vec4 Color;
                uniform mat4 ProjMtx;
                out vec2 Frag_UV;
                out vec4 Frag_Color;
                void main()
                {
                    Frag_UV = UV;
                    Frag_Color = Color;
                    gl_Position = ProjMtx * vec4(Position.xy,0.0,1.0);
                }";
        } else if (api == "OpenGL")
        {
            vertexSource =
                @"#version 330
                layout (location = 0) in vec2 Position;
                layout (location = 1) in vec2 UV;
                layout (location = 2) in vec4 Color;
                uniform mat4 ProjMtx;
                out vec2 Frag_UV;
                out vec4 Frag_Color;
                void main()
                {
                    Frag_UV = UV;
                    Frag_Color = Color;
                    gl_Position = ProjMtx * vec4(Position.xy,0,1);
                }";
        }
        else
        {
            vertexSource =
                @"#version 110
                attribute vec2 Position;
                attribute vec2 UV;
                attribute vec4 Color;

                uniform mat4 ProjMtx;

                varying vec2 Frag_UV;
                varying vec4 Frag_Color;

                void main()
                {
                    Frag_UV = UV;
                    Frag_Color = Color;
                    gl_Position = ProjMtx * vec4(Position.xy,0,1);
                }";
        }

        string fragmentSource;
        
        if (api == "OpenGLES")
        {
            fragmentSource =
                @"#version 300 es
                precision highp float;
                
                in vec2 Frag_UV;
                in vec4 Frag_Color;
                uniform sampler2D Texture;
                layout (location = 0) out vec4 Out_Color;
                void main()
                {
                    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                }";
        } else if (api == "OpenGL")
        {
            fragmentSource =
                @"#version 330
                in vec2 Frag_UV;
                in vec4 Frag_Color;
                uniform sampler2D Texture;
                layout (location = 0) out vec4 Out_Color;
                void main()
                {
                    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
                }";
        }
        else
        {
            fragmentSource =
                @"#version 110
                varying vec2 Frag_UV;
                varying vec4 Frag_Color;

                uniform sampler2D Texture;

                void main()
                {
                    gl_FragColor = Frag_Color * texture2D(Texture, Frag_UV.st);
                }";
        }

        var files = new[]{
            (ShaderType.VertexShader, vertexSource),
            (ShaderType.FragmentShader, fragmentSource),
        };
        Program = CreateProgram(files);
        
        _attribLocationTex = GetUniformLocation("Texture");
        _attribLocationProjMtx = GetUniformLocation("ProjMtx");
        _attribLocationVtxPos = GetAttribLocation("Position");
        _attribLocationVtxUV = GetAttribLocation("UV");
        _attribLocationVtxColor = GetAttribLocation("Color");

        _vboHandle = _gl.GenBuffer();
        _elementsHandle = _gl.GenBuffer();

        RecreateFontDeviceTexture();

        // Restore modified GL state
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
        _gl.BindBuffer(GLEnum.ArrayBuffer, (uint)lastArrayBuffer);

        _gl.BindVertexArray((uint)lastVertexArray);

        CheckGlError(_gl, "End of ImGui setup");
    }

    /// <summary>
    /// Creates the texture used to render text.
    /// </summary>
    private unsafe void RecreateFontDeviceTexture()
    {
        // Build texture atlas
        var io = ImGuiNET.ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height,
            out int bytesPerPixel); // Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders. If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory.

        // Upload texture to graphics system
        _gl.GetInteger(GLEnum.TextureBinding2D, out int lastTexture);

        _fontTexture = new Texture(_gl, width, height, pixels);
        _fontTexture.Bind();
        _fontTexture.SetMagFilter(TextureMagFilter.Linear);
        _fontTexture.SetMinFilter(TextureMinFilter.Linear);

        // Store our identifier
        io.Fonts.SetTexID((IntPtr)_fontTexture.GlTexture);

        // Restore state
        _gl.BindTexture(GLEnum.Texture2D, (uint)lastTexture);
    }
    

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        _backend.OnResize -= WindowResized;

        _gl.DeleteBuffer(_vboHandle);
        _gl.DeleteBuffer(_elementsHandle);
        _gl.DeleteVertexArray(_vertexArrayObject);

        _fontTexture.Dispose();

        ImGuiNET.ImGui.DestroyContext(Context);
    }
    

    public void InputPartOnInputEvent(Event ev)
    {
        throw new NotImplementedException();
    }
    
    
    /// <summary>
    /// Constructs a new ImGuiController.
    /// </summary>
    public Controller(GL gl, IWindowBackend backend) : this(gl, backend, null, null)
    {
    }
    

    /// <summary>
    /// Constructs a new ImGuiController with font configuration.
    /// </summary>
    public Controller(GL gl, IWindowBackend backend, ImGuiFontConfig imGuiFontConfig) : this(gl, backend,
        imGuiFontConfig, null)
    {
    }
    

    /// <summary>
    /// Constructs a new ImGuiController with an onConfigureIO Action.
    /// </summary>
    public Controller(GL gl, IWindowBackend backend, Action onConfigureIO) : this(gl, backend, null,
        onConfigureIO)
    {
    }
    

    /// <summary>
    /// Constructs a new ImGuiController with font configuration and onConfigure Action.
    /// </summary>
    public Controller(GL gl, IWindowBackend backend, ImGuiFontConfig? imGuiFontConfig = null,
        Action onConfigureIO = null)
    {
        Init(gl, backend);

        var io = ImGuiNET.ImGui.GetIO();
        if (imGuiFontConfig is not null)
        {
            var glyphRange = imGuiFontConfig.Value.GetGlyphRange?.Invoke(io) ?? default;

            io.Fonts.AddFontFromFileTTF(imGuiFontConfig.Value.FontPath, imGuiFontConfig.Value.FontSize, null,
                glyphRange);
        }

        onConfigureIO?.Invoke();

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDeviceResources();

        SetPerFrameImGuiData(1f / 60f);

        BeginFrame();
    }
}