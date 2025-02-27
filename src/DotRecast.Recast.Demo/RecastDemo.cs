/*
Copyright (c) 2009-2010 Mikko Mononen memon@inside.org
recast4j copyright (c) 2015-2019 Piotr Piastucki piotr@jtilia.org
DotRecast Copyright (c) 2023 Choi Ikpil ikpil@naver.com

This software is provided 'as-is', without any express or implied
warranty.  In no event will the authors be held liable for any damages
arising from the use of this software.
Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:
1. The origin of this software must not be misrepresented; you must not
 claim that you wrote the original software. If you use this software
 in a product, an acknowledgment in the product documentation would be
 appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
 misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using Serilog;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using ImGuiNET;
using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.Extras.Unity.Astar;
using DotRecast.Detour.Io;
using DotRecast.Recast.DemoTool.Builder;
using DotRecast.Recast.Demo.Draw;
using DotRecast.Recast.Demo.Messages;
using DotRecast.Recast.DemoTool.Geom;
using DotRecast.Recast.Demo.Tools;
using DotRecast.Recast.Demo.UI;
using DotRecast.Recast.DemoTool;
using Silk.NET.GLFW;
using static DotRecast.Core.RcMath;
using MouseButton = Silk.NET.Input.MouseButton;
using Window = Silk.NET.Windowing.Window;

namespace DotRecast.Recast.Demo;

public class RecastDemo : IRecastDemoChannel
{
    private static readonly ILogger Logger = Log.ForContext<RecastDemo>();

    private IWindow window;
    private GL _gl;
    private IInputContext _input;
    private ImGuiController _imgui;
    private RcCanvas _canvas;

    private int width = 1000;
    private int height = 900;

    private readonly string title = "DotRecast Demo";

    //private readonly RecastDebugDraw dd;
    private NavMeshRenderer renderer;
    private float timeAcc = 0;
    private float camr = 1000;

    private readonly SoloNavMeshBuilder soloNavMeshBuilder = new SoloNavMeshBuilder();
    private readonly TileNavMeshBuilder tileNavMeshBuilder = new TileNavMeshBuilder();

    private string _lastGeomFileName;
    private Sample _sample;

    private bool processHitTest = false;
    private bool processHitTestShift;
    private int _modState;

    private readonly float[] mousePos = new float[2];

    private bool _mouseOverMenu;
    private bool pan;
    private bool movedDuringPan;
    private bool rotate;
    private bool movedDuringRotate;
    private float scrollZoom;
    private readonly float[] origMousePos = new float[2];
    private readonly float[] origCameraEulers = new float[2];
    private RcVec3f origCameraPos = new RcVec3f();

    private readonly float[] cameraEulers = { 45, -45 };
    private RcVec3f cameraPos = RcVec3f.Of(0, 0, 0);


    private float[] projectionMatrix = new float[16];
    private float[] modelviewMatrix = new float[16];

    private float _moveFront;
    private float _moveLeft;
    private float _moveBack;
    private float _moveRight;
    private float _moveUp;
    private float _moveDown;
    private float _moveAccel;

    private int[] viewport;
    private bool markerPositionSet;
    private RcVec3f markerPosition = new RcVec3f();

    private RcToolsetView toolset;
    private RcSettingsView settingsView;
    private RcLogView logView;

    private long prevFrameTime;
    private RecastDebugDraw dd;
    private readonly Queue<IRecastDemoMessage> _messages;

    public RecastDemo()
    {
        _messages = new();
    }

    public void Run()
    {
        window = CreateWindow();
        window.Run();
    }

    public void OnMouseScrolled(IMouse mice, ScrollWheel scrollWheel)
    {
        if (scrollWheel.Y < 0)
        {
            // wheel down
            if (!_mouseOverMenu)
            {
                scrollZoom += 1.0f;
            }
        }
        else
        {
            if (!_mouseOverMenu)
            {
                scrollZoom -= 1.0f;
            }
        }

        float[] modelviewMatrix = dd.ViewMatrix(cameraPos, cameraEulers);
        cameraPos.x += scrollZoom * 2.0f * modelviewMatrix[2];
        cameraPos.y += scrollZoom * 2.0f * modelviewMatrix[6];
        cameraPos.z += scrollZoom * 2.0f * modelviewMatrix[10];
        scrollZoom = 0;
    }

    public void OnMouseMoved(IMouse mouse, Vector2 position)
    {
        mousePos[0] = (float)position.X;
        mousePos[1] = (float)position.Y;
        int dx = (int)(mousePos[0] - origMousePos[0]);
        int dy = (int)(mousePos[1] - origMousePos[1]);
        if (rotate)
        {
            cameraEulers[0] = origCameraEulers[0] + dy * 0.25f;
            cameraEulers[1] = origCameraEulers[1] + dx * 0.25f;
            if (dx * dx + dy * dy > 3 * 3)
            {
                movedDuringRotate = true;
            }
        }

        if (pan)
        {
            float[] modelviewMatrix = dd.ViewMatrix(cameraPos, cameraEulers);
            cameraPos = origCameraPos;

            cameraPos.x -= 0.1f * dx * modelviewMatrix[0];
            cameraPos.y -= 0.1f * dx * modelviewMatrix[4];
            cameraPos.z -= 0.1f * dx * modelviewMatrix[8];

            cameraPos.x += 0.1f * dy * modelviewMatrix[1];
            cameraPos.y += 0.1f * dy * modelviewMatrix[5];
            cameraPos.z += 0.1f * dy * modelviewMatrix[9];
            if (dx * dx + dy * dy > 3 * 3)
            {
                movedDuringPan = true;
            }
        }
    }

    public void OnMouseUpAndDown(IMouse mouse, MouseButton button, bool down)
    {
        if (down)
        {
            if (button == MouseButton.Right)
            {
                if (!_mouseOverMenu)
                {
                    // Rotate view
                    rotate = true;
                    movedDuringRotate = false;
                    origMousePos[0] = mousePos[0];
                    origMousePos[1] = mousePos[1];
                    origCameraEulers[0] = cameraEulers[0];
                    origCameraEulers[1] = cameraEulers[1];
                }
            }
            else if (button == MouseButton.Middle)
            {
                if (!_mouseOverMenu)
                {
                    // Pan view
                    pan = true;
                    movedDuringPan = false;
                    origMousePos[0] = mousePos[0];
                    origMousePos[1] = mousePos[1];
                    origCameraPos.x = cameraPos.x;
                    origCameraPos.y = cameraPos.y;
                    origCameraPos.z = cameraPos.z;
                }
            }
        }
        else
        {
            // Handle mouse clicks here.
            if (button == MouseButton.Right)
            {
                rotate = false;
                if (!_mouseOverMenu)
                {
                    if (!movedDuringRotate)
                    {
                        processHitTest = true;
                        processHitTestShift = true;
                    }
                }
            }
            else if (button == MouseButton.Left)
            {
                if (!_mouseOverMenu)
                {
                    processHitTest = true;
                    processHitTestShift = 0 != (_modState & KeyModState.Shift);
                }
            }
            else if (button == MouseButton.Middle)
            {
                pan = false;
            }
        }
    }


    private IWindow CreateWindow()
    {
        var monitor = Window.Platforms.First().GetMainMonitor();
        var resolution = monitor.VideoMode.Resolution.Value;

        float aspect = 16.0f / 9.0f;
        width = Math.Min(resolution.X, (int)(resolution.Y * aspect)) - 100;
        height = resolution.Y - 100;
        viewport = new int[] { 0, 0, width, height };

        var options = WindowOptions.Default;
        options.Title = title;
        options.Size = new Vector2D<int>(width, height);
        options.Position = new Vector2D<int>((resolution.X - width) / 2, (resolution.Y - height) / 2);
        options.VSync = true;
        options.ShouldSwapAutomatically = false;
        options.PreferredDepthBufferBits = 24;
        window = Window.Create(options);

        if (window == null)
        {
            throw new Exception("Failed to create the GLFW window");
        }

        window.Closing += OnWindowClosing;
        window.Load += OnWindowOnLoad;
        window.Resize += OnWindowResize;
        window.FramebufferResize += OnWindowFramebufferSizeChanged;
        window.Update += OnWindowUpdate;
        window.Render += OnWindowRender;


        // // -- move somewhere else:
        // glfw.SetWindowPos(window, (mode->Width - width) / 2, (mode->Height - height) / 2);
        // // GlfwSetWindowMonitor(window.GetWindow(), monitor, 0, 0, mode.Width(), mode.Height(), mode.RefreshRate());
        // glfw.ShowWindow(window);
        // glfw.MakeContextCurrent(window);
        //}

        //glfw.SwapInterval(1);

        return window;
    }

    private DemoInputGeomProvider LoadInputMesh(string filename)
    {
        var bytes = Loader.ToBytes(filename);
        DemoInputGeomProvider geom = DemoObjImporter.Load(bytes);

        _lastGeomFileName = filename;
        return geom;
    }

    private void LoadNavMesh(FileStream file, string filename)
    {
        try
        {
            DtNavMesh mesh = null;
            if (filename.EndsWith(".zip"))
            {
                UnityAStarPathfindingImporter importer = new UnityAStarPathfindingImporter();
                mesh = importer.Load(file)[0];
            }
            else
            {
                using var br = new BinaryReader(file);
                DtMeshSetReader reader = new DtMeshSetReader();
                mesh = reader.Read(br, 6);
            }

            if (null != mesh)
            {
                _sample.Update(_sample.GetInputGeom(), ImmutableArray<RecastBuilderResult>.Empty, mesh);
                toolset.SetEnabled(true);
            }
        }
        catch (Exception e)
        {
            Logger.Error(e, "");
        }
    }

    private void OnWindowClosing()
    {
    }

    private void OnWindowResize(Vector2D<int> size)
    {
        width = size.X;
        height = size.Y;
    }

    private void OnWindowFramebufferSizeChanged(Vector2D<int> size)
    {
        _gl.Viewport(size);
        viewport = new int[] { 0, 0, width, height };
    }


    private void OnWindowOnLoad()
    {
        _input = window.CreateInput();

        // mouse input
        foreach (var mice in _input.Mice)
        {
            mice.Scroll += OnMouseScrolled;
            mice.MouseDown += (m, b) => OnMouseUpAndDown(m, b, true);
            mice.MouseUp += (m, b) => OnMouseUpAndDown(m, b, false);
            mice.MouseMove += OnMouseMoved;
        }

        _gl = window.CreateOpenGL();

        dd = new RecastDebugDraw(_gl);
        renderer = new NavMeshRenderer(dd);

        dd.Init(camr);

        ImGuiFontConfig imGuiFontConfig = new(Path.Combine("resources\\fonts", "DroidSans.ttf"), 24, null);
        _imgui = new ImGuiController(_gl, window, _input, imGuiFontConfig);

        DemoInputGeomProvider geom = LoadInputMesh("nav_test.obj");
        _sample = new Sample(geom, ImmutableArray<RecastBuilderResult>.Empty, null);

        settingsView = new RcSettingsView(this);
        settingsView.SetSample(_sample);

        toolset = new RcToolsetView(
            new TestNavmeshTool(),
            new OffMeshConnectionTool(),
            new ConvexVolumeTool(),
            new CrowdTool(),
            new JumpLinkBuilderTool(),
            new DynamicUpdateTool()
        );
        toolset.SetEnabled(true);
        logView = new RcLogView();

        _canvas = new RcCanvas(window, settingsView, toolset, logView);

        var vendor = _gl.GetStringS(GLEnum.Vendor);
        var version = _gl.GetStringS(GLEnum.Version);
        var renderGl = _gl.GetStringS(GLEnum.Renderer);
        var glslString = _gl.GetStringS(GLEnum.ShadingLanguageVersion);


        var workingDirectory = Directory.GetCurrentDirectory();
        Logger.Information($"working directory - {workingDirectory}");
        Logger.Information($"ImGui.Net version - {ImGui.GetVersion()}");
        Logger.Information(vendor);
        Logger.Information(version);
        Logger.Information(renderGl);
        Logger.Information(glslString);
    }

    private void UpdateKeyboard(float dt)
    {
        _modState = 0;

        // keyboard input
        foreach (var keyboard in _input.Keyboards)
        {
            var tempMoveFront = keyboard.IsKeyPressed(Key.W) || keyboard.IsKeyPressed(Key.Up) ? 1.0f : -1f;
            var tempMoveLeft = keyboard.IsKeyPressed(Key.A) || keyboard.IsKeyPressed(Key.Left) ? 1.0f : -1f;
            var tempMoveBack = keyboard.IsKeyPressed(Key.S) || keyboard.IsKeyPressed(Key.Down) ? 1.0f : -1f;
            var tempMoveRight = keyboard.IsKeyPressed(Key.D) || keyboard.IsKeyPressed(Key.Right) ? 1.0f : -1f;
            var tempMoveUp = keyboard.IsKeyPressed(Key.Q) || keyboard.IsKeyPressed(Key.PageUp) ? 1.0f : -1f;
            var tempMoveDown = keyboard.IsKeyPressed(Key.E) || keyboard.IsKeyPressed(Key.PageDown) ? 1.0f : -1f;
            var tempMoveAccel = keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight) ? 1.0f : -1f;
            var tempControl = keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight);

            _modState |= tempControl ? (int)KeyModState.Control : (int)KeyModState.None;
            _modState |= 0 < tempMoveAccel ? (int)KeyModState.Shift : (int)KeyModState.None;

            //Logger.Information($"{_modState}");
            _moveFront = Clamp(_moveFront + tempMoveFront * dt * 4.0f, 0, 2.0f);
            _moveLeft = Clamp(_moveLeft + tempMoveLeft * dt * 4.0f, 0, 2.0f);
            _moveBack = Clamp(_moveBack + tempMoveBack * dt * 4.0f, 0, 2.0f);
            _moveRight = Clamp(_moveRight + tempMoveRight * dt * 4.0f, 0, 2.0f);
            _moveUp = Clamp(_moveUp + tempMoveUp * dt * 4.0f, 0, 2.0f);
            _moveDown = Clamp(_moveDown + tempMoveDown * dt * 4.0f, 0, 2.0f);
            _moveAccel = Clamp(_moveAccel + tempMoveAccel * dt * 4.0f, 0, 2.0f);
        }
    }

    private void OnWindowUpdate(double dt)
    {
        /*
          * try (MemoryStack stack = StackPush()) { int[] w = stack.MallocInt(1); int[] h =
          * stack.MallocInt(1); GlfwGetWindowSize(win, w, h); width = w.x; height = h.x; }
       */
        if (_sample.GetInputGeom() != null)
        {
            var settings = _sample.GetSettings();
            RcVec3f bmin = _sample.GetInputGeom().GetMeshBoundsMin();
            RcVec3f bmax = _sample.GetInputGeom().GetMeshBoundsMax();
            Recast.CalcGridSize(bmin, bmax, settings.cellSize, out var gw, out var gh);
            settingsView.SetVoxels(gw, gh);
            settingsView.SetTiles(tileNavMeshBuilder.GetTiles(_sample.GetInputGeom(), settings.cellSize, settings.tileSize));
            settingsView.SetMaxTiles(tileNavMeshBuilder.GetMaxTiles(_sample.GetInputGeom(), settings.cellSize, settings.tileSize));
            settingsView.SetMaxPolys(tileNavMeshBuilder.GetMaxPolysPerTile(_sample.GetInputGeom(), settings.cellSize, settings.tileSize));
        }

        UpdateKeyboard((float)dt);

        // camera move
        float keySpeed = 22.0f;
        if (0 < _moveAccel)
        {
            keySpeed *= _moveAccel * 2.0f;
        }

        double movex = (_moveRight - _moveLeft) * keySpeed * dt;
        double movey = (_moveBack - _moveFront) * keySpeed * dt + scrollZoom * 2.0f;
        scrollZoom = 0;

        cameraPos.x += (float)(movex * modelviewMatrix[0]);
        cameraPos.y += (float)(movex * modelviewMatrix[4]);
        cameraPos.z += (float)(movex * modelviewMatrix[8]);

        cameraPos.x += (float)(movey * modelviewMatrix[2]);
        cameraPos.y += (float)(movey * modelviewMatrix[6]);
        cameraPos.z += (float)(movey * modelviewMatrix[10]);

        cameraPos.y += (float)((_moveUp - _moveDown) * keySpeed * dt);

        long time = RcFrequency.Ticks;
        prevFrameTime = time;

        // Update sample simulation.
        float SIM_RATE = 20;
        float DELTA_TIME = 1.0f / SIM_RATE;
        timeAcc = Clamp((float)(timeAcc + dt), -1.0f, 1.0f);
        int simIter = 0;
        while (timeAcc > DELTA_TIME)
        {
            timeAcc -= DELTA_TIME;
            if (simIter < 5 && _sample != null)
            {
                toolset.HandleUpdate(DELTA_TIME);
            }

            simIter++;
        }

        if (processHitTest)
        {
            processHitTest = false;
            
            RcVec3f rayStart = new RcVec3f();
            RcVec3f rayEnd = new RcVec3f();

            GLU.GlhUnProjectf(mousePos[0], viewport[3] - 1 - mousePos[1], 0.0f, modelviewMatrix, projectionMatrix, viewport, ref rayStart);
            GLU.GlhUnProjectf(mousePos[0], viewport[3] - 1 - mousePos[1], 1.0f, modelviewMatrix, projectionMatrix, viewport, ref rayEnd);

            SendMessage(new RaycastEvent()
            {
                Start = rayStart,
                End = rayEnd,
            });
        }

        if (_sample.IsChanged())
        {
            RcVec3f? bminN = null;
            RcVec3f? bmaxN = null;
            if (_sample.GetInputGeom() != null)
            {
                bminN = _sample.GetInputGeom().GetMeshBoundsMin();
                bmaxN = _sample.GetInputGeom().GetMeshBoundsMax();
            }
            else if (_sample.GetNavMesh() != null)
            {
                RcVec3f[] bounds = NavMeshUtils.GetNavMeshBounds(_sample.GetNavMesh());
                bminN = bounds[0];
                bmaxN = bounds[1];
            }
            else if (0 < _sample.GetRecastResults().Count)
            {
                foreach (RecastBuilderResult result in _sample.GetRecastResults())
                {
                    if (result.GetSolidHeightfield() != null)
                    {
                        if (bminN == null)
                        {
                            bminN = RcVec3f.Of(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                            bmaxN = RcVec3f.Of(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                        }

                        bminN = RcVec3f.Of(
                            Math.Min(bminN.Value.x, result.GetSolidHeightfield().bmin.x),
                            Math.Min(bminN.Value.y, result.GetSolidHeightfield().bmin.y),
                            Math.Min(bminN.Value.z, result.GetSolidHeightfield().bmin.z)
                        );

                        bmaxN = RcVec3f.Of(
                            Math.Max(bmaxN.Value.x, result.GetSolidHeightfield().bmax.x),
                            Math.Max(bmaxN.Value.y, result.GetSolidHeightfield().bmax.y),
                            Math.Max(bmaxN.Value.z, result.GetSolidHeightfield().bmax.z)
                        );
                    }
                }
            }

            if (bminN != null && bmaxN != null)
            {
                RcVec3f bmin = bminN.Value;
                RcVec3f bmax = bmaxN.Value;

                camr = (float)(Math.Sqrt(
                                   Sqr(bmax.x - bmin.x) + Sqr(bmax.y - bmin.y) + Sqr(bmax.z - bmin.z))
                               / 2);
                cameraPos.x = (bmax.x + bmin.x) / 2 + camr;
                cameraPos.y = (bmax.y + bmin.y) / 2 + camr;
                cameraPos.z = (bmax.z + bmin.z) / 2 + camr;
                camr *= 5;
                cameraEulers[0] = 45;
                cameraEulers[1] = -45;
            }

            _sample.SetChanged(false);
            toolset.SetSample(_sample);
        }

        if (_messages.TryDequeue(out var msg))
        {
            OnMessage(msg);
        }


        var io = ImGui.GetIO();

        io.DisplaySize = new Vector2(width, height);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = (float)dt;

        _canvas.Update(dt);
        _imgui.Update((float)dt);
    }

    private void OnWindowRender(double dt)
    {
        // Clear the screen
        dd.Clear();
        projectionMatrix = dd.ProjectionMatrix(50f, (float)width / (float)height, 1.0f, camr);
        modelviewMatrix = dd.ViewMatrix(cameraPos, cameraEulers);

        dd.Fog(camr * 0.1f, camr * 1.25f);
        renderer.Render(_sample, settingsView.GetDrawMode());

        IRcTool tool = toolset.GetTool();
        if (tool != null)
        {
            tool.HandleRender(renderer);
        }

        dd.Fog(false);

        _canvas.Draw(dt);
        _mouseOverMenu = _canvas.IsMouseOver();

        _imgui.Render();

        window.SwapBuffers();
    }

    public void SendMessage(IRecastDemoMessage message)
    {
        _messages.Enqueue(message);
    }

    private void OnMessage(IRecastDemoMessage message)
    {
        if (message is GeomLoadBeganEvent args)
        {
            OnGeomLoadBegan(args);
        }
        else if (message is NavMeshBuildBeganEvent args2)
        {
            OnNavMeshBuildBegan(args2);
        }
        else if (message is NavMeshSaveBeganEvent args3)
        {
            OnNavMeshSaveBegan(args3);
        }
        else if (message is NavMeshLoadBeganEvent args4)
        {
            OnNavMeshLoadBegan(args4);
        }
        else if (message is RaycastEvent args5)
        {
            OnRaycast(args5);
        }
    }

    private void OnGeomLoadBegan(GeomLoadBeganEvent args)
    {
        var geom = LoadInputMesh(args.FilePath);

        _sample.Update(geom, ImmutableArray<RecastBuilderResult>.Empty, null);
    }

    private void OnNavMeshBuildBegan(NavMeshBuildBeganEvent args)
    {
        if (null == _sample.GetInputGeom())
        {
            Logger.Information($"not found source geom");
            return;
        }

        var settings = _sample.GetSettings();
        var partitioning = settings.partitioning;
        var cellSize = settings.cellSize;
        var cellHeight = settings.cellHeight;
        var agentHeight = settings.agentHeight;
        var agentRadius = settings.agentRadius;
        var agentMaxClimb = settings.agentMaxClimb;
        var agentMaxSlope = settings.agentMaxSlope;
        var regionMinSize = settings.minRegionSize;
        var regionMergeSize = settings.mergedRegionSize;
        var edgeMaxLen = settings.edgeMaxLen;
        var edgeMaxError = settings.edgeMaxError;
        var vertsPerPoly = settings.vertsPerPoly;
        var detailSampleDist = settings.detailSampleDist;
        var detailSampleMaxError = settings.detailSampleMaxError;
        var filterLowHangingObstacles = settings.filterLowHangingObstacles;
        var filterLedgeSpans = settings.filterLedgeSpans;
        var filterWalkableLowHeightSpans = settings.filterWalkableLowHeightSpans;
        var tileSize = settings.tileSize;

        long t = RcFrequency.Ticks;

        Logger.Information($"build");

        NavMeshBuildResult buildResult;
        if (settings.tiled)
        {
            buildResult = tileNavMeshBuilder.Build(
                _sample.GetInputGeom(),
                partitioning,
                cellSize,
                cellHeight,
                agentHeight,
                agentRadius,
                agentMaxClimb,
                agentMaxSlope,
                regionMinSize,
                regionMergeSize,
                edgeMaxLen,
                edgeMaxError,
                vertsPerPoly,
                detailSampleDist,
                detailSampleMaxError,
                filterLowHangingObstacles,
                filterLedgeSpans,
                filterWalkableLowHeightSpans,
                tileSize
            );
        }
        else
        {
            buildResult = soloNavMeshBuilder.Build(
                _sample.GetInputGeom(),
                partitioning,
                cellSize,
                cellHeight,
                agentHeight,
                agentRadius,
                agentMaxClimb,
                agentMaxSlope,
                regionMinSize,
                regionMergeSize,
                edgeMaxLen,
                edgeMaxError,
                vertsPerPoly,
                detailSampleDist,
                detailSampleMaxError,
                filterLowHangingObstacles,
                filterLedgeSpans,
                filterWalkableLowHeightSpans
            );
        }

        _sample.Update(_sample.GetInputGeom(), buildResult.RecastBuilderResults, buildResult.NavMesh);
        _sample.SetChanged(false);
        settingsView.SetBuildTime((RcFrequency.Ticks - t) / TimeSpan.TicksPerMillisecond);
        //settingsUI.SetBuildTelemetry(buildResult.Item1.Select(x => x.GetTelemetry()).ToList());
        toolset.SetSample(_sample);

        Logger.Information($"build times");
        Logger.Information($"-----------------------------------------");
        var telemetries = buildResult.RecastBuilderResults
            .Select(x => x.GetTelemetry())
            .SelectMany(x => x.ToList())
            .GroupBy(x => x.Key)
            .ToImmutableSortedDictionary(x => x.Key, x => x.Sum(y => y.Millis));

        foreach (var (key, millis) in telemetries)
        {
            Logger.Information($"{key}: {millis} ms");
        }
    }

    private void OnNavMeshSaveBegan(NavMeshSaveBeganEvent args)
    {
        var navMesh = _sample.GetNavMesh();
        if (null == navMesh)
        {
            Logger.Error("navmesh is null");
            return;
        }

        DateTime now = DateTime.Now;
        string ymdhms = $"{now:yyyyMMdd_HHmmss}";
        var filename = Path.GetFileNameWithoutExtension(_lastGeomFileName);
        var navmeshFilePath = $"{filename}_{ymdhms}.navmesh";

        using var fs = new FileStream(navmeshFilePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        var writer = new DtMeshSetWriter();
        writer.Write(bw, navMesh, RcByteOrder.LITTLE_ENDIAN, true);
        Logger.Information($"saved navmesh - {navmeshFilePath}");
    }

    private void OnNavMeshLoadBegan(NavMeshLoadBeganEvent args)
    {
        if (string.IsNullOrEmpty(args.FilePath))
        {
            Logger.Error("file path is empty");
            return;
        }

        if (!File.Exists(args.FilePath))
        {
            Logger.Error($"not found navmesh file - {args.FilePath}");
            return;
        }

        try
        {
            using FileStream fs = new FileStream(args.FilePath, FileMode.Open, FileAccess.Read);
            LoadNavMesh(fs, args.FilePath);
        }
        catch (Exception e)
        {
            Logger.Error(e, "");
        }
    }

    private void OnRaycast(RaycastEvent args)
    {
        var rayStart = args.Start;
        var rayEnd = args.End;

        // Hit test mesh.
        DemoInputGeomProvider inputGeom = _sample.GetInputGeom();
        if (_sample == null)
            return;

        float? hit = null;
        if (inputGeom != null)
        {
            hit = inputGeom.RaycastMesh(rayStart, rayEnd);
        }

        if (!hit.HasValue && _sample.GetNavMesh() != null)
        {
            hit = NavMeshRaycast.Raycast(_sample.GetNavMesh(), rayStart, rayEnd);
        }

        if (!hit.HasValue && _sample.GetRecastResults() != null)
        {
            hit = PolyMeshRaycast.Raycast(_sample.GetRecastResults(), rayStart, rayEnd);
        }

        RcVec3f rayDir = RcVec3f.Of(rayEnd.x - rayStart.x, rayEnd.y - rayStart.y, rayEnd.z - rayStart.z);
        IRcTool rayTool = toolset.GetTool();
        rayDir.Normalize();
        if (rayTool != null)
        {
            Logger.Information($"click ray - tool({rayTool.GetTool().GetName()}) rayStart({rayStart.x:0.#},{rayStart.y:0.#},{rayStart.z:0.#}) pos({rayDir.x:0.#},{rayDir.y:0.#},{rayDir.z:0.#}) shift({processHitTestShift})");
            rayTool.HandleClickRay(rayStart, rayDir, processHitTestShift);
        }

        if (hit.HasValue)
        {
            float hitTime = hit.Value;
            if (0 != (_modState & KeyModState.Control))
            {
                // Marker
                markerPositionSet = true;
                markerPosition.x = rayStart.x + (rayEnd.x - rayStart.x) * hitTime;
                markerPosition.y = rayStart.y + (rayEnd.y - rayStart.y) * hitTime;
                markerPosition.z = rayStart.z + (rayEnd.z - rayStart.z) * hitTime;
            }
            else
            {
                RcVec3f pos = new RcVec3f();
                pos.x = rayStart.x + (rayEnd.x - rayStart.x) * hitTime;
                pos.y = rayStart.y + (rayEnd.y - rayStart.y) * hitTime;
                pos.z = rayStart.z + (rayEnd.z - rayStart.z) * hitTime;
                if (rayTool != null)
                {
                    Logger.Information($"click - tool({rayTool.GetTool().GetName()}) rayStart({rayStart.x:0.#},{rayStart.y:0.#},{rayStart.z:0.#}) pos({pos.x:0.#},{pos.y:0.#},{pos.z:0.#}) shift({processHitTestShift})");
                    rayTool.HandleClick(rayStart, pos, processHitTestShift);
                }
            }
        }
        else
        {
            if (0 != (_modState & KeyModState.Control))
            {
                // Marker
                markerPositionSet = false;
            }
        }
    }
}